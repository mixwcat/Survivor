using UnityEngine;

/// <summary>
/// 玩家基础配置数据
/// </summary>
[CreateAssetMenu(fileName = "PlayerData", menuName = "Game/Data/Player Data")]
public class PlayerDataSO : BaseEntityDataSO
{
    [Header("玩家专属属性")]
    public float PickRange = 1f;
    public float UnbeatableTime = 0.2f;
    public float AttackInterval = 0f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.PlayerPickRange, PickRange);
        model.SetBaseValue(StatType.PlayerUnbeatableTime, UnbeatableTime);
        model.SetBaseValue(StatType.AttackInterval, AttackInterval);
    }
}
