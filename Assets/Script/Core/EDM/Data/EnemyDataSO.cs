using UnityEngine;

/// <summary>
/// 敌人基础配置数据
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Data/Enemy Data")]
public class EnemyDataSO : BaseEntityDataSO
{
    [Header("敌人专属属性")]
    public float ExpReward = 1f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.ExpReward, ExpReward);
    }
}
