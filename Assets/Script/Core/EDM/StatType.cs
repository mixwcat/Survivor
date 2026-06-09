/// <summary>
/// 全局数值类型枚举，覆盖所有实体（玩家/塔/武器/敌人）
/// 新增数值类型时在此添加，并在对应 DataSO 中提供基础值
/// </summary>
public enum StatType
{
    // === 通用 ===
    BaseMaxHealth,
    BaseMoveSpeed,
    BaseDamage,

    // === 玩家 ===
    PlayerPickRange,
    PlayerUnbeatableTime,

    // === 塔 ===
    TowerAttackRange,
    TowerHitForce,

    // === 武器：旋转火球 ===
    SpinWeaponRotationSpeed,
    SpinWeaponSize,
    SpinWeaponLifeTime,


    // === 武器：枪械 ===
    BulletSpeed,
    BulletHitForce,

    // === 武器：通用 ===
    HitPushForce,
    AttackInterval,

    // === Luo ===
    HealAmount,
    HealRange,
    HealInterval,

    // === 敌人 ===
    ExpReward,
}
