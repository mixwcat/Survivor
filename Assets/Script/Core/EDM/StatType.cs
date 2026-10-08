/// <summary>
/// 全局数值类型枚举，覆盖所有实体（玩家/塔/武器/敌人）
/// 新增数值类型时在此添加，并在对应 DataSO 中提供基础值
///
/// ⚠️ <b>必须显式赋值，且已用编号永不重用/重排</b>：
/// <see cref="StatModifierData"/> / <see cref="LevelUpSO"/> 等资产把 StatType 以**整数**序列化，
/// 一旦在中段插入或删除成员，所有既有 .asset 的 TargetStat 会静默错位——
/// 升级会改到别的属性上（曾实际发生：火球升级改掉了枪械的 BulletSpeed），且不报任何错。
/// 删除成员时保留其编号空洞并注明，不要复用。
/// </summary>
public enum StatType
{
    // === 通用 ===
    MaxHealth = 0,
    MoveSpeed = 1,
    Damage = 2,

    // === 玩家 ===
    PlayerPickRange = 3,
    PlayerUnbeatableTime = 4,

    // === 塔 ===
    TowerHitForce = 6,

    // === 武器：旋转火球 ===
    SpinWeaponRotationSpeed = 7,
    SpinWeaponSize = 8,
    SpinWeaponLifeTime = 9,

    // === 武器：枪械 ===
    BulletSpeed = 10,
    BulletHitForce = 11,

    // === 武器：通用 ===
    HitPushForce = 12,
    // 13 曾为 AttackInterval（单位「秒」）—— 语义已迁移到 AttackSpeed（次/秒）。
    // 编号保留空洞，禁止复用：既有升级资产仍以整数 13 落盘，复用会让它们静默指向另一个数值。

    /// <summary>
    /// 武器射程 —— 索敌圈半径 / 治疗范围（单位：世界单位）。
    ///
    /// <para>
    /// <b>编号 5 是历史遗留</b>：它曾叫 <c>TowerAttackRange</c>（只有塔有射程）。
    /// 射程归武器之后改名为 <c>AttackRange</c> 并挪到"武器：通用"这一节，
    /// **但数值 5 不能变** —— 既有升级资产（如 <c>RinHitRange</c>）以整数 5 落盘。
    /// </para>
    /// </summary>
    AttackRange = 5,

    // === Luo ===
    HealAmount = 14,
    // 15 曾为 HealRange，已移除（治疗范围统一用 AttackRange）；编号保留空洞，禁止复用
    // 16 曾为 HealInterval（单位「秒」）—— 语义已迁移到 HealSpeed（次/秒），编号同样保留空洞

    // === 敌人 ===
    ExpReward = 17,

    // === 塔：炮弹（Teto 的第二种远程攻击）===
    // 炮弹**不复用** Damage 的语义：它读自己的 ShellDamage，
    // 这样「子弹伤害低、炮弹伤害高」是两套独立可升级的数值，
    // 而不是「同一份 Damage 乘一个隐藏倍率」——后者只能整条远程线一起升。
    ShellDamage = 18,
    ShellExplosionRadius = 19,

    // === 速率语义（2026-10-02）===
    // 「冷却时间（秒）」一律改成「速率（次/秒）」：数值越大越快，
    // 于是所有升级/装备/buff 的加成方向统一为「加大」。
    // 旧语义下 Add 型升级会把间隔**加大** = 射速变慢，而数据层面无法察觉
    // （「增加攻击频率」实际让射速减半就是这么发生的）。
    AttackSpeed = 20,
    HealSpeed = 21,
}
