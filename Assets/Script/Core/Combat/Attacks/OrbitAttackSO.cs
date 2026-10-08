using UnityEngine;

/// <summary>
/// 环绕物攻击 —— 在发射点生成一个物体，并把它挂到发射点下面（旋转火球）。
///
/// <para>
/// <b>「环绕」效果来自武器自身的自转</b>（<c>SpinWeapon.Update</c>）：生成物是发射点的子物体，
/// 武器一转它们就绕圈。本 SO 只负责生成与初始化，不逐帧做任何事。
/// </para>
///
/// <para>
/// 与 <see cref="ProjectileAttackSO"/> 一样，伤害/尺寸/存在时间都由 SO 上的
/// <see cref="StatType"/> 字段指定，读的是**发起攻击的实体自己**的 StatModel。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "AttackOrbit", menuName = "Game/Attack/Orbit")]
public class OrbitAttackSO : AttackMethodSO
{
    [Header("数值来源（读发起攻击的实体自己的 StatModel）")]
    public StatType DamageStat = StatType.Damage;
    public StatType SizeStat = StatType.SpinWeaponSize;
    public StatType LifeTimeStat = StatType.SpinWeaponLifeTime;
    public StatType PushForceStat = StatType.HitPushForce;

    /// <summary>
    /// 实际发射间隔 = 攻击间隔（1 / 速率）+ 生成物存在时间，确保新旧环绕物不重叠。
    /// 与旧 <c>SpinWeapon.GetFireInterval</c> 的「间隔 + 存在时间」这条耦合是故意的，不要「顺手优化」。
    /// </summary>
    public override float GetInterval(EntityBehaviour self)
    {
        float speed = self.GetStat(StatType.AttackSpeed);
        float fireInterval = speed > 0f ? 1f / speed : float.PositiveInfinity;
        return fireInterval + self.GetStat(LifeTimeStat);
    }

    /// <summary>
    /// 环绕物本体就是那个 prefab（生成到发射点下面）—— 漏配同样必须变成启动期红错。
    /// </summary>
    public override bool RequiresPrefab => true;

    /// <summary>
    /// 间隔读的是「1 / 攻击速率 + 存在时间」（见 <see cref="GetInterval"/>），
    /// 所以速率与存在时间都必须由目标实体的 DataSO 提供。
    /// </summary>
    public override StatType[] RequiredStats =>
        new[] { DamageStat, SizeStat, LifeTimeStat, PushForceStat, StatType.AttackSpeed };

    public override bool Execute(in AttackContext ctx)
    {
        GameObject prefab = ctx.Prefab;
        if (prefab == null) return false;
        if (ctx.Origin == null) return false;

        float lifeTime = ctx.Self.GetStat(LifeTimeStat);
        float size = ctx.Self.GetStat(SizeStat);
        int damage = (int)ctx.Self.GetStat(DamageStat);
        float pushForce = ctx.Self.GetStat(PushForceStat);

        // 生成走 ctx.Spawner（parent = 发射点，生成物挂上去跟随武器旋转）。
        // Spawner 对「有父级」的情况用普通 Instantiate：它保持世界坐标与 prefab 的 localScale，
        // 等价于旧实现的 SetParent(pos, false) 之后再赋 position。
        GameObject instance = ctx.Spawner?.Spawn(prefab, ctx.Origin.position, Quaternion.identity, ctx.Origin);
        if (instance == null) return false;

        SpinWeaponController spin = instance.GetComponent<SpinWeaponController>();
        if (spin == null)
        {
            Debug.LogError($"[{nameof(OrbitAttackSO)}] {prefab.name} 未挂 SpinWeaponController，" +
                           "无法初始化。请检查 driver 上的攻击资源引用。");
            ctx.Spawner.Despawn(instance);
            return false;
        }

        spin.Init(lifeTime, size, damage, pushForce, ctx.Host);
        return true;
    }
}
