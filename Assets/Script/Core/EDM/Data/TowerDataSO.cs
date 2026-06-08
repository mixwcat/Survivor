using UnityEngine;

/// <summary>
/// 防御塔基础配置数据
/// 适用于 Teto / Rin / Luo 等所有塔类型
/// </summary>
[CreateAssetMenu(fileName = "TowerData", menuName = "EDM/Tower Data")]
public class TowerDataSO : BaseEntityDataSO
{
    [Header("塔专属属性")]
    public float AttackRange = 2f;
    public float AttackInterval = 2f;
    public float HitForce = 0f;

    [Header("Luo 专属（非治疗塔可保留默认值）")]
    public float HealAmount = 0f;
    public float HealInterval = 0f;
    public float HealRange = 0f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.TowerAttackRange, AttackRange);
        model.SetBaseValue(StatType.TowerAttackInterval, AttackInterval);
        model.SetBaseValue(StatType.TowerHitForce, HitForce);
        model.SetBaseValue(StatType.HealAmount, HealAmount);
        model.SetBaseValue(StatType.HealInterval, HealInterval);
        model.SetBaseValue(StatType.HealRange, HealRange);
    }
}
