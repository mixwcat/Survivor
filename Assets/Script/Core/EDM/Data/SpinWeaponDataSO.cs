using UnityEngine;

/// <summary>
/// 旋转火球武器专属配置数据。
///
/// Damage 是本类自己的字段（不再是基类共有的）：武器伤害只被武器自己读取。
/// 字段名必须保持 <c>Damage</c> —— 既有 .asset 是按字段名反序列化的。
/// </summary>
[CreateAssetMenu(fileName = "SpinWeaponData", menuName = "Game/Data/Weapon/Spin Weapon")]
public class SpinWeaponDataSO : WeaponDataSO
{
    [Header("旋转武器专属")]
    public float Damage = 5f;
    public float RotationSpeed = 360f;
    public float Size = 1f;
    public float LifeTime = 4f;
    public float HitPushForce = 5f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.Damage, Damage);
        model.SetBaseValue(StatType.SpinWeaponRotationSpeed, RotationSpeed);
        model.SetBaseValue(StatType.SpinWeaponSize, Size);
        model.SetBaseValue(StatType.SpinWeaponLifeTime, LifeTime);
        model.SetBaseValue(StatType.HitPushForce, HitPushForce);
    }
}
