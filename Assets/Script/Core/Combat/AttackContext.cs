using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一次攻击的全部输入。<c>readonly struct</c> + <c>in</c> 传参，调用不产生堆分配。
///
/// <para>
/// <b>为什么朝向由实体给：</b><see cref="Direction"/> 由 driver 填 ——
/// 玩家写输入摇杆/鼠标方向，塔写「指向目标」。SO 因此不需要知道 <c>IInputHandle</c> 的存在，
/// 同一份攻击方式塔和玩家都能用（旧实现里 <c>GunWeapon</c> 的瞄准逻辑和塔的索敌逻辑
/// 分别长在两个不相干的类里，正是它们无法共用的原因）。
/// </para>
///
/// <para>
/// <b>为什么目标列表是 <c>List&lt;T&gt;</c> 而不是 <c>IReadOnlyList&lt;T&gt;</c>：</b>
/// 它是 driver 维护的实例状态，不能放 SO；用具体类型是为了避免遍历时装箱枚举器
/// （见 CLAUDE.md 性能红线）。
/// </para>
///
/// <para>
/// <b>为什么不持有 driver 引用：</b>结构体只承载「这一次攻击的数据」，
/// 对 driver 的反向引用会让它难以在联机/回放场景里独立传递（那时没有本地 driver 实例）。
/// </para>
/// </summary>
public readonly struct AttackContext
{
    /// <summary>发起攻击的实体（武器实例 / 塔）。攻击数值从它的 StatModel 读取。</summary>
    public readonly EntityBehaviour Self;

    /// <summary>
    /// 攻击的**归属者**：武器实例的宿主（玩家 / 塔）；没有宿主时等于 <see cref="Self"/>。
    ///
    /// <para>
    /// 与 <see cref="Self"/> 分开的原因：武器有自己的一套数值（读 <see cref="Self"/>），
    /// 但"谁打死的"必须落在宿主身上 —— 击杀统计与塔的投入账本都按宿主结算。
    /// 以前两者共用 <see cref="Self"/>，于是"武器打死敌人算谁的"要顺着层级往上猜。
    /// </para>
    /// </summary>
    public readonly EntityBehaviour Host;

    /// <summary>发射点。driver 未配置 muzzle 时即实体自身 Transform。</summary>
    public readonly Transform Origin;

    /// <summary>朝向。零向量表示「没有有效朝向」，攻击方应自行决定是否空转。</summary>
    public readonly Vector2 Direction;

    /// <summary>主目标（范围内最先进入的存活目标），可能为 null。</summary>
    public readonly BaseHealthController Target;

    /// <summary>
    /// 范围内的目标列表，由 <see cref="AttackDriver"/> 维护。
    /// 只做遍历，**不要**在这里增删 —— 它不是本结构体的所有物。
    /// </summary>
    public readonly List<BaseHealthController> TargetsInRange;

    /// <summary>
    /// 本攻击用的 prefab，来自发起攻击的那个 <see cref="AttackDriver"/> 的攻击资源槽
    /// （一个实体只有一个攻击方式，见那里的类注释）。
    /// 句柄由 <see cref="AttackDriver"/> 持有并归还（SO 不碰资源）。
    /// 未配置或尚未加载完成时为 null，攻击方直接 return false 即可 ——
    /// 这与旧实现 <c>Teto.OnOperate</c> 里 <c>if (_bulletPrefab == null) return;</c> 的行为一致。
    /// </summary>
    public readonly GameObject Prefab;

    /// <summary>
    /// 生成/回收攻击实体的入口（见 <see cref="IAttackSpawner"/>）。
    /// 攻击方式**必须**通过它创建投射物/召唤物，不要自己 <c>Instantiate</c> ——
    /// 单机走对象池、联机走网络生成，差异由实现方承担。
    /// </summary>
    public readonly IAttackSpawner Spawner;

    public AttackContext(EntityBehaviour self, EntityBehaviour host, Transform origin, Vector2 direction,
                         BaseHealthController target, List<BaseHealthController> targetsInRange,
                         GameObject prefab, IAttackSpawner spawner)
    {
        Self = self;
        Host = host != null ? host : self;
        Origin = origin;
        Direction = direction;
        Target = target;
        TargetsInRange = targetsInRange;
        Prefab = prefab;
        Spawner = spawner;
    }
}
