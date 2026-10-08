using UnityEngine;

/// <summary>
/// 塔用武器的数值 data —— 塔的武器是**独立实体**，数值来自这一份，而不是塔的 <c>TowerDataSO</c>。
///
/// <para>
/// <b>为什么单独一份：</b>旧模型里塔的"第二把武器"只是 <c>AttackDriver._extraAttacks</c>
/// 里的一个攻击方式 SO —— 没有自己的 prefab、没有自己的数值、没有自己的升级项，
/// 于是"给这把武器升级"根本无处可写。武器独立之后，每把武器各自持有
/// <c>WeaponEntitySO</c> → 本 data → 自己的 StatModel → 自己的升级项。
/// </para>
///
/// <para>
/// 字段与 <c>TowerDataSO</c> 的攻击部分**刻意同名同义**（Damage / HitForce / BulletSpeed /
/// ShellDamage / ShellExplosionRadius），这样把塔的数值搬过来时是逐项对照，
/// 而不是"猜哪个字段对应哪个 StatType"。
/// </para>
///
/// <para>
/// <b>射程（<c>AttackRange</c>）不在本类</b>：它属于**所有武器**，已提到基类
/// <see cref="WeaponDataSO"/>（同名移动，既有资产的数值不受影响）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "TowerWeaponData", menuName = "Game/Data/Weapon/Tower Weapon")]
public class TowerWeaponDataSO : WeaponDataSO
{
    [Header("攻击数值")]
    public float Damage = 10f;
    public float HitForce = 0f;
    public float BulletSpeed = 8f;

    [Header("溅射（炮弹类武器才用）")]
    public float ShellDamage = 40f;
    public float ShellExplosionRadius = 3f;

    [Header("治疗（治疗类武器才用）")]
    public float HealAmount = 0f;
    public float HealSpeed = 1f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);   // AttackSpeed + AttackRange

        model.SetBaseValue(StatType.Damage, Damage);
        model.SetBaseValue(StatType.TowerHitForce, HitForce);
        model.SetBaseValue(StatType.BulletSpeed, BulletSpeed);
        model.SetBaseValue(StatType.ShellDamage, ShellDamage);
        model.SetBaseValue(StatType.ShellExplosionRadius, ShellExplosionRadius);
        model.SetBaseValue(StatType.HealAmount, HealAmount);
        model.SetBaseValue(StatType.HealSpeed, HealSpeed);
    }
}
