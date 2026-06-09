using UnityEngine;

/// <summary>
/// 枪械武器专属配置
/// 继承 WeaponDataSO，添加枪械专属属性
/// </summary>
[CreateAssetMenu(fileName = "GunWeaponData", menuName = "Game/Data/Weapon/Gun Weapon")]
public class GunWeaponDataSO : WeaponDataSO
{
    [Header("枪械专属")]
    public float BulletSpeed = 20f;
    public float BulletHitForce = 20f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.BulletSpeed, BulletSpeed);
        model.SetBaseValue(StatType.BulletHitForce, BulletHitForce);
        model.SetBaseValue(StatType.BaseDamage, Damage);
    }
}
