using UnityEngine;

/// <summary>
/// 塔数据基类 —— 攻击塔（Teto / Rin）与治疗塔（Luo）共用。
///
/// <para>
/// <b>为什么 Luo 不继承 TowerDataSO：</b>它不会造成伤害、也没有子弹，
/// 继承攻击塔会让它在 Inspector 里看到 Damage / AttackInterval / HitForce / BulletSpeed 四个
/// 改了完全没效果的字段。两者平级挂在 BaseTowerDataSO 下，各自只暴露自己的字段。
/// </para>
/// <para>
/// <b>这里没有攻击范围</b>：射程属于**武器**（<c>WeaponDataSO.AttackRange</c> →
/// <c>StatType.AttackRange</c>），因为索敌圈长在武器上（每把武器自带检测圈，
/// 由 <c>TowerWeaponRangeSync</c> 按武器自己的数值写半径）。塔身上原先那份射程已删除 ——
/// 两份值并存时，改错一个不会报错，只会"升了射程没效果"。
/// </para>
/// </summary>
public abstract class BaseTowerDataSO : BaseActorDataSO
{
    [Header("塔购买消耗")]
    [Tooltip("购买/放置消耗的技能点，由 ChooseTowerPanel 直接读取，不进 StatModel（不参与升级计算）")]
    public int Cost = 1;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
    }
}
