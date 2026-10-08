using UnityEngine;

/// <summary>
/// 激光（Laser）专属配置数据 —— 只有伤害与攻击速率。
///
/// <para>
/// <b>为什么不复用 <see cref="GunWeaponDataSO"/>：</b>激光没有飞行物，
/// <c>BulletSpeed</c> / <c>BulletHitForce</c> 对它永远是死值。
/// 配了不生效的字段比没有这个字段更糟 —— 调数值的人会以为改它有用。
/// </para>
///
/// <para>
/// 速率读基类的 <c>AttackSpeed</c>（<c>BeamAttackSO.SpeedStat</c> 默认指向它）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "LaserWeaponData", menuName = "Game/Data/Weapon/Laser Weapon")]
public class LaserWeaponDataSO : WeaponDataSO
{
    [Header("激光专属")]
    public float Damage = 6f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.Damage, Damage);
    }
}
