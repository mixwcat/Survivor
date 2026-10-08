using UnityEngine;

/// <summary>
/// 大炮（Cannon）专属配置数据 —— 在枪械数值之上补一个**溅射半径**。
///
/// <para>
/// <b>为什么单独一个子类而不是把 SplashRadius 塞进 <see cref="GunWeaponDataSO"/>：</b>
/// 枪没有溅射，那个字段对它永远是死值 —— 而"配了没反应"的字段最容易被误认为承重
/// （CLAUDE.md 里塔的 MoveSpeed、Luo 的 Damage 都是这么留下的）。
/// 数值只放真正读它的那一层。
/// </para>
///
/// <para>
/// 攻击方式（<c>SplashProjectileAttackSO</c>）通过 <c>SplashRadiusStat = ShellExplosionRadius</c>
/// 读它；<c>AttackDriver</c> 启动时会校验这个数值确实由本 DataSO 提供，
/// 漏配不会静默退化成"半径 1 的隐形溅射"。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CannonWeaponData", menuName = "Game/Data/Weapon/Cannon Weapon")]
public class CannonWeaponDataSO : GunWeaponDataSO
{
    [Header("大炮专属")]
    [Tooltip("命中时的溅射半径（世界单位）。<= 0 会退化成单体，与 ProjectileAttackSO 的约定一致")]
    public float SplashRadius = 2.5f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.ShellExplosionRadius, SplashRadius);
    }
}
