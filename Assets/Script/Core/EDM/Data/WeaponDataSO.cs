using UnityEngine;

/// <summary>
/// 武器基础配置数据
/// 适用于 SpinWeapon / GunWeapon
/// </summary>
[CreateAssetMenu(fileName = "WeaponData", menuName = "EDM/Weapon Data")]
public class WeaponDataSO : BaseEntityDataSO
{
    [Header("通用武器属性")]
    public float AttackInterval = 1f;

    [Header("旋转火球 (SpinWeapon)")]
    public float RotationSpeed = 360f;
    public float BulletSize = 1f;
    public float FireBallLifeTime = 4f;

    [Header("枪械 (GunWeapon)")]
    public float BulletSpeed = 20f;
    public float BulletDamage = 8f;
    public float BulletHitForce = 20f;

    public override void FillStatModel(EntityStatModel model)
    {
        // 武器不继承通用 Damage/MoveSpeed，只设置自己的字段
        model.SetBaseValue(StatType.TowerAttackInterval, AttackInterval);
        model.SetBaseValue(StatType.FireBallRotationSpeed, RotationSpeed);
        model.SetBaseValue(StatType.FireBallSize, BulletSize);
        model.SetBaseValue(StatType.FireBallLifeTime, FireBallLifeTime);
        model.SetBaseValue(StatType.BulletSpeed, BulletSpeed);
        model.SetBaseValue(StatType.BulletDamage, BulletDamage);
        model.SetBaseValue(StatType.BulletHitForce, BulletHitForce);
    }
}
