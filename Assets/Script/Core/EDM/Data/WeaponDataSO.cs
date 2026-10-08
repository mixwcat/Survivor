using UnityEngine;

/// <summary>
/// 武器基础数据 SO（抽象基类）—— 只放所有武器共用的数值。
/// 每种武器请使用对应的子类创建 asset：<see cref="SpinWeaponDataSO"/> / <see cref="GunWeaponDataSO"/>。
///
/// <para>
/// 武器**不继承** <see cref="BaseActorDataSO"/>：它没有血量，也就没有 MaxHealth，
/// 不会被敌人的接触伤害结算（<c>BaseHealthController</c> 不挂在武器上）。
/// </para>
/// <para>
/// 投射物 prefab 不放这里：它由 <c>AttackDriver</c> 用 <c>AssetReferenceGameObject</c> 持有，
/// 并负责 Addressables 句柄的获取与归还（<c>WeaponDataSO</c> 里曾经存在的
/// <c>projectilePrefab</c> 字段没有任何读取方，属于「配了没用」）。
/// </para>
/// </summary>
public abstract class WeaponDataSO : BaseEntityDataSO
{
    [Header("通用武器属性")]
    [Tooltip("攻击速率（次/秒）：1 = 每秒一发，0.2 = 每 5 秒一发。数值越大越快")]
    public float AttackSpeed = 1f;

    [Tooltip("射程（世界单位）：塔武器的索敌圈半径 / 治疗范围就是它。\n" +
             "玩家武器目前不吃这个值（它们的攻击靠输入方向或发射点），但射程属于**武器**，" +
             "所以字段放在共用层，需要范围指示器时不必再搬家。")]
    public float AttackRange = 2f;

    public override void FillStatModel(EntityStatModel model)
    {
        // 直接继承 BaseEntityDataSO（无共用数值），因此这里没有 base 调用可做。
        model.SetBaseValue(StatType.AttackSpeed, AttackSpeed);
        model.SetBaseValue(StatType.AttackRange, AttackRange);
    }
}
