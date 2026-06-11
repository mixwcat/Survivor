using UnityEngine;

/// <summary>
/// 旋转火球武器专属配置
/// 继承 WeaponDataSO，添加火球专属属性
/// </summary>
[CreateAssetMenu(fileName = "SpinWeaponData", menuName = "Game/Data/Weapon/Spin Weapon")]
public class SpinWeaponDataSO : WeaponDataSO
{
    [Header("旋转武器专属")]
    public float RotationSpeed = 360f;
    public float Size = 1f;
    public float LifeTime = 4f;
    public float HitPushForce = 5f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.SpinWeaponRotationSpeed, RotationSpeed);
        model.SetBaseValue(StatType.SpinWeaponSize, Size);
        model.SetBaseValue(StatType.SpinWeaponLifeTime, LifeTime);
        model.SetBaseValue(StatType.Damage, Damage);
        model.SetBaseValue(StatType.HitPushForce, HitPushForce);
    }
}
