using UnityEngine;

/// <summary>
/// 枪械武器专属配置数据。
///
/// Damage 是本类自己的字段（不再是基类共有的）：武器伤害只被武器自己读取，
/// 与玩家身上的数值无关。字段名必须保持 <c>Damage</c> —— 既有 .asset 是按字段名反序列化的。
/// </summary>
[CreateAssetMenu(fileName = "GunWeaponData", menuName = "Game/Data/Weapon/Gun Weapon")]
public class GunWeaponDataSO : WeaponDataSO
{
    [Header("枪械专属")]
    public float Damage = 10f;
    public float BulletSpeed = 20f;
    public float BulletHitForce = 20f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.Damage, Damage);
        model.SetBaseValue(StatType.BulletSpeed, BulletSpeed);
        model.SetBaseValue(StatType.BulletHitForce, BulletHitForce);
    }
}
