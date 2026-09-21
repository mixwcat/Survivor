using UnityEngine;

/// <summary>
/// Luo 治疗塔专属配置
/// 继承 TowerDataSO，添加治疗专属属性
/// </summary>
[CreateAssetMenu(fileName = "LuoTowerData", menuName = "Game/Data/Tower/Luo Tower")]
public class LuoTowerDataSO : TowerDataSO
{
    [Header("Luo 治疗专属")]
    public float HealAmount = 0f;
    public float HealInterval = 0f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.HealAmount, HealAmount);
        model.SetBaseValue(StatType.HealInterval, HealInterval);
        // 注意：Luo 的治疗范围直接使用 TowerAttackRange，不再使用 HealRange
    }
}
