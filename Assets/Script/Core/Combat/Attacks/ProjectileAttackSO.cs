using UnityEngine;

/// <summary>
/// 投射物攻击 —— 在发射点生成一个 prefab，交给 prefab 上的 <see cref="BulletController"/>
/// 自行飞行与结算命中。
///
/// <para>
/// 伤害 / 速度 / 力度读的是**发起攻击的实体自己的** StatModel，
/// 具体读哪三个数值由本 SO 的 <see cref="StatType"/> 字段指定：
/// 攻击塔用 <c>Damage / BulletSpeed / TowerHitForce</c>，枪械用
/// <c>Damage / BulletSpeed / BulletHitForce</c> —— 数值本身留在各自的 DataSO 里，
/// 所以同一份攻击方式可以挂到不同实体上，而升级照旧按 StatType 生效。
/// </para>
///
/// <para>
/// ⚠️ 改这几个 <see cref="StatType"/> 字段**不会报错**，但会让升级打偏：
/// <c>LevelUpSO</c> 是以整数落盘指向具体数值的。改完请核对对应的升级资产。
/// </para>
///
/// <para>
/// 子弹自身的朝向差异（Teto 的子弹需要额外旋转 90°）由 prefab 上的
/// <see cref="BulletController"/> 子类在自己的 <c>Init</c> 里处理 ——
/// <c>GetComponent&lt;BulletController&gt;()</c> 拿到的是子类实例，虚方法会正确派发，
/// 因此本 SO 不需要任何「要不要旋转」的开关。
/// </para>
///
/// <para>
/// <b>外观（缩放/贴图/爆炸表现）全部归 prefab</b>：炮弹有自己的 prefab
/// （<c>Bullet_Shell</c> / <c>TetoShell</c>，挂 <see cref="ShellController"/>），
/// 缩放直接烘在 prefab 的 Transform 上。本 SO 只管**数值**；「是不是溅射」由子类
/// <see cref="SplashProjectileAttackSO"/> 表达（本类即单体）。
/// 曾经有一个 <c>Scale</c> 字段，拆开 prefab 之后它就成了第二个缩放来源，已删除。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "AttackProjectile", menuName = "Game/Attack/Projectile")]
public class ProjectileAttackSO : AttackMethodSO
{
    [Header("数值来源（读发起攻击的实体自己的 StatModel）")]
    public StatType DamageStat = StatType.Damage;
    public StatType SpeedStat = StatType.BulletSpeed;
    public StatType ForceStat = StatType.BulletHitForce;

    /// <summary>投射物靠 <see cref="AttackContext.Direction"/> 决定飞行朝向 —— 需要瞄准输入。</summary>
    public override bool RequiresAimInput => true;

    /// <summary>
    /// 没有投射物 prefab 就没有任何东西可生成 —— 漏配必须由 <see cref="AttackDriver"/>
    /// 在启动时报红错，而不是表现成"武器永不开火"。
    /// </summary>
    public override bool RequiresPrefab => true;

    /// <summary>
    /// 本攻击方式读取的数值。单体不读溅射半径，所以清单里没有它 ——
    /// 要求所有投射物武器都提供 <c>ShellExplosionRadius</c> 会让枪的启动校验误报。
    /// <para>
    /// <c>AttackSpeed</c> 是基类 <see cref="GetInterval"/> 读的，本类没重写它，所以要列进来。
    /// </para>
    /// </summary>
    public override StatType[] RequiredStats =>
        new[] { DamageStat, SpeedStat, ForceStat, StatType.AttackSpeed };

    /// <summary>单体：溅射半径恒为 0（见 <see cref="Fire"/>）。</summary>
    public override bool Execute(in AttackContext ctx) => Fire(ctx, 0f);

    /// <summary>
    /// 生成投射物并灌入本次参数 —— **单体与溅射共用的那一份实现**（子类只决定半径从哪来）。
    ///
    /// <para>
    /// <paramref name="splashRadius"/> <c>&lt;= 0</c> = 单体，<c>&gt; 0</c> = 命中点溅射
    /// （判定在 <see cref="BulletController.OnHit"/>，实现只有那一份）。
    /// 子类不要复制本方法的流程：句柄、生成方（池/联机）、以及"prefab 没挂
    /// <see cref="BulletController"/> 时必须回收"这几个分支都在这里收口。
    /// </para>
    /// </summary>
    protected bool Fire(in AttackContext ctx, float splashRadius)
    {
        // 资源未配置或尚未加载完成：空转一次，不报错。
        GameObject prefab = ctx.Prefab;
        if (prefab == null) return false;

        // 没有有效朝向就不发射（塔在目标消失的那一帧会走到这里）
        if (ctx.Direction.sqrMagnitude < 0.0001f) return false;

        int damage = (int)ctx.Self.GetStat(DamageStat);
        int hitForce = (int)ctx.Self.GetStat(ForceStat);
        float speed = ctx.Self.GetStat(SpeedStat);

        // 生成走 ctx.Spawner：单机是对象池，联机是网络生成 —— 策略类不需要知道差别。
        GameObject instance = ctx.Spawner?.Spawn(prefab, ctx.Origin.position, Quaternion.identity);
        if (instance == null) return false;

        BulletController bullet = instance.GetComponent<BulletController>();
        if (bullet == null)
        {
            // prefab 没挂 BulletController。旧实现在这里是直接 NRE，
            // 而且生成出来的子弹永远没人销毁（没有 BulletController 就没人归还）。
            Debug.LogError($"[{nameof(ProjectileAttackSO)}] {prefab.name} 未挂 BulletController，" +
                           "无法初始化。请检查 driver 上的攻击资源引用。");
            ctx.Spawner.Despawn(instance);
            return false;
        }

        // 归属传 Host（武器打死敌人算宿主的）；数值已经在上面按 Self 读完了
        bullet.Init(damage, hitForce, speed, ctx.Direction, splashRadius, ctx.Host, ctx.Spawner);
        return true;
    }
}
