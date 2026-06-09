using UnityEngine;

/// <summary>
/// 防御塔基础配置数据
/// 适用于 Teto / Rin 等攻击型塔
/// Luo 治疗塔请使用 LuoTowerDataSO
/// </summary>
[CreateAssetMenu(fileName = "TowerData", menuName = "Game/Data/Tower/Base Tower")]
public class TowerDataSO : BaseEntityDataSO
{
    [Header("塔专属属性")]
    public float AttackRange = 2f;
    public float AttackInterval = 2f;
    public float HitForce = 0f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.TowerAttackRange, AttackRange);
        model.SetBaseValue(StatType.AttackInterval, AttackInterval);
        model.SetBaseValue(StatType.TowerHitForce, HitForce);
    }
}
