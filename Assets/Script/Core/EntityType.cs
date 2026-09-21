/// <summary>
/// 实体类型枚举
/// 用于统一标识玩家、敌人、塔、武器等实体类型
/// 替代 string id，提供编译期类型安全和更好的性能
/// </summary>
public enum EntityType
{
    Player = 0,

    // 敌人
    EnemyCloud = 1,
    EnemySlime = 2,
    EnemySoil = 3,

    // 塔
    TowerTeto = 4,
    TowerRin = 5,
    TowerLuo = 6,

    // 武器
    WeaponSpin = 7,
    WeaponGun = 8,
}
