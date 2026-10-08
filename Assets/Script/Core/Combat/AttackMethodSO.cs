using UnityEngine;

/// <summary>
/// 攻击方式（策略 SO）—— 只描述「一次攻击做什么」，由 <see cref="AttackDriver"/> 按间隔调用。
///
/// <para>
/// 同一个 SO 可以同时给塔和玩家武器用：它不依赖输入系统，朝向由实体通过
/// <see cref="AttackContext.Direction"/> 传入，目标列表由 driver 通过
/// <see cref="AttackContext.TargetsInRange"/> 传入。
/// </para>
///
/// <para>
/// <b>红线一：本类及其子类必须是纯只读的。</b>
/// SO 是常驻资产，不参与场景生命周期，也没有可靠的 <c>OnEnable</c>/<c>OnDisable</c>。
/// 在这里写字段、缓存运行时状态、订阅事件或持有资源句柄，都会在关闭 Domain Reload 的工程里
/// 跨 Play 会话存活并指向已销毁的对象。所有实例状态一律放 <see cref="AttackDriver"/>。
/// </para>
///
/// <para>
/// <b>红线二：攻击方式不持有资源引用。</b>
/// 投射物/召唤物的 prefab 由 driver 用 <c>AssetReferenceGameObject</c> 持有，
/// 并负责 Addressables 句柄的获取与归还（<c>WeaponDataSO</c> 里已经写明「投射物 prefab
/// 不放 SO，统一走 Addressables 加载」，本设计延续该决策）。
/// SO 只从 <see cref="AttackContext.Prefab"/> 取用。
/// </para>
/// </summary>
public abstract class AttackMethodSO : ScriptableObject
{
    /// <summary>
    /// 攻击间隔的下限（秒）。用于兜住「升级把间隔叠加到 0 或负数」的情况。
    ///
    /// <para>
    /// 没有它的话，若干次「缩短间隔」的升级会让 <see cref="AttackDriver"/> 走进
    /// <c>interval &lt;= 0</c> 的分支并**停止攻击** —— 比「射速极快」更糟，也更不像 bug。
    /// 注意它只兜运行时叠加：DataSO 基础值本身 &lt;= 0 属于配置错误，仍由
    /// <see cref="AttackDriver"/> 在启动时报错并停止攻击。
    /// </para>
    /// </summary>
    public const float MinInterval = 0.05f;

    /// <summary>
    /// 范围内哪些实体算目标，由 driver 的 trigger 回调按 tag 过滤。
    /// 默认 <c>"Enemy"</c>；治疗类攻击改成 <c>"Tower"</c>。
    ///
    /// <para>
    /// <b>为什么放在攻击方式上而不是 driver 上：</b>「打谁」是攻击语义的一部分。
    /// 放在 driver 上的话，给塔换上治疗攻击方式后 driver 仍然只找 <c>Enemy</c>，
    /// 治疗技能会去治疗敌人。放在这里，换攻击方式时目标类型自动跟着换。
    /// </para>
    /// </summary>
    public virtual string TargetTag => "Enemy";

    /// <summary>
    /// 本攻击方式是否需要「瞄准方向」输入。默认 false。
    ///
    /// <para>
    /// 投射物为 true（<see cref="AttackContext.Direction"/> 决定子弹朝向）；范围伤害、治疗、
    /// 环绕物都是 false —— 它们不需要方向，或方向由载体自己算。
    /// </para>
    /// <para>
    /// <b>用途：</b>UI 据此决定是否显示攻击摇杆（见 <see cref="IWeaponManager.HasAimWeapon"/>）。
    /// 这样新增一把「需要瞄准的武器」不必改 UI 代码，UI 也不必认识具体的武器类
    /// （原先 UI 直接 `GetWeapon&lt;GunWeapon&gt;()`，换个武器类型就要改界面逻辑）。
    /// </para>
    /// </summary>
    public virtual bool RequiresAimInput => false;

    /// <summary>
    /// 本攻击方式会读取的**全部数值**（<see cref="Execute"/> 与 <see cref="GetInterval"/> 里读到的每一个）。
    ///
    /// <para>
    /// <b>用途：</b><see cref="AttackDriver"/> 在初始化时据此校验目标实体的 DataSO 是否覆盖这些数值。
    /// 缺一个的后果是静默的 —— <c>EntityBehaviour.GetStat</c> 返回 <c>1f</c> 且只告警一次
    /// （很容易淹没在其它日志里），表现是「伤害是 1」「间隔是 1 秒」这类查不出原因的错误。
    /// 校验把它变成启动期的一条红错，并一次列出全部缺失项。
    /// </para>
    /// <para>
    /// <b>新增攻击方式时必须列出全部数值</b>，包括换用别的间隔数值的情况
    /// （<see cref="HealAlliesAttackSO"/> 读 <c>HealInterval</c> 而不是 <c>AttackInterval</c>）。
    /// <b>需要"可选数值"时请拆子类，不要用 bool 开关改变本清单</b> ——
    /// 那会让"翻一个开关就换一套校验"变得不可读，而且开关为 false 时那份配置是死值
    /// （投射物的溅射半径就是这么从 <c>SplashOnHit</c> 改成
    /// <see cref="SplashProjectileAttackSO"/> 这个子类的）。
    /// </para>
    /// <para>
    /// 刻意**不做缓存**：每次访问返回新数组，而调用点只有实体初始化一次；
    /// 换来的是 SO 上不出现任何可以被误认为「运行时状态」的字段（见红线一）。
    /// </para>
    /// </summary>
    public abstract StatType[] RequiredStats { get; }

    /// <summary>
    /// 本攻击方式**是否依赖** <see cref="AttackContext.Prefab"/>（投射物 / 环绕物这类要生成实体的）。
    ///
    /// <para>
    /// <b>用途：</b><see cref="AttackDriver"/> 在启动时据此把「需要资源却没配」变成一条红错。
    /// 漏配的后果是**完全静默**的：<see cref="Execute"/> 拿到 null 只能空转
    /// （<c>ProjectileAttackSO.Fire</c> 的 <c>if (prefab == null) return false;</c>），
    /// 表现是"武器永不开火、控制台一条日志都没有" —— 比数值配错更难查，
    /// 因为数值至少还有 <see cref="RequiredStats"/> 兜着。
    /// </para>
    ///
    /// <para>
    /// <b>默认 false</b>：范围伤害 / 治疗 / 光束都是"这一帧结算完就没有实体"，
    /// 它们留空是正常配置，不该报错。需要资源的方式**必须**覆写为 true。
    /// </para>
    /// </summary>
    public virtual bool RequiresPrefab => false;

    /// <summary>
    /// 本次攻击的间隔（**秒**，供 <see cref="AttackDriver"/> 计时）。
    ///
    /// <para>
    /// 数值本体是**速率**（<see cref="StatType.AttackSpeed"/>，次/秒），这里换算成秒：
    /// 速率越大越快，所以所有升级/装备的加成方向统一为「加大」。
    /// 曾经直接用「间隔秒数」时，`Add` 型升级会把间隔**加大** = 射速变慢 ——
    /// `ShootGunInterval` 的「增加攻击频率」实际让射速减半就是这么来的，
    /// 而数据层面无法察觉（`StatType` 不带「越大越好 / 越小越好」的语义）。
    /// </para>
    /// <para>
    /// 速率 &lt;= 0（漏配或配成 0）返回 <see cref="float.PositiveInfinity"/> = 永不攻击：
    /// 配置错误不该表现成「疯狂攻击」，由 <see cref="AttackDriver"/> 在启动时报错。
    /// </para>
    /// </summary>
    public virtual float GetInterval(EntityBehaviour self)
    {
        float speed = self.GetStat(StatType.AttackSpeed);
        return speed > 0f ? 1f / speed : float.PositiveInfinity;
    }

    /// <summary>
    /// 执行一次攻击。<b>禁止</b>在此读写本 SO 的字段 —— 见类注释「红线一」。
    /// </summary>
    /// <returns>
    /// 本次是否**真的打出去了**（有目标 / 资源就绪）。
    /// 返回 false 时 <see cref="AttackDriver"/> 不发 <c>OnPerformed</c> ——
    /// 这样「范围内没敌人」的塔不会对着空气播攻击动画（旧 Rin/Luo 也是先判空再播的）。
    /// </returns>
    public abstract bool Execute(in AttackContext ctx);
}
