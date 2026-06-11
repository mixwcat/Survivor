/// <summary>
/// 实体类型枚举
/// 用于统一标识玩家、敌人、塔、武器等实体类型
/// 替代 string id，提供编译期类型安全和更好的性能
/// </summary>
public enum EntityType
{
    Player,
    Enemy,

    TowerTeto,
    TowerRin,
    TowerLuo,

    WeaponFireBall,
    WeaponGun,
}
