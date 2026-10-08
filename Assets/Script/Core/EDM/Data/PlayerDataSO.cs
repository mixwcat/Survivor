using UnityEngine;

/// <summary>
/// 玩家基础配置数据。
///
/// 只放**玩家自己读**的数值：<c>PlayerController</c> 读 MoveSpeed，
/// <c>ExpSpriteController</c> 读 PlayerPickRange，<c>PlayerHealthController</c> 读 PlayerUnbeatableTime。
///
/// 注意这里**没有** Damage / AttackInterval：武器读的是武器自己的 StatModel
/// （见 <c>BaseWeapon</c>），玩家身上这两个数值无人读取，放上来只会让测试以为改了有用。
/// </summary>
[CreateAssetMenu(fileName = "PlayerData", menuName = "Game/Data/Player Data")]
public class PlayerDataSO : BaseActorDataSO
{
    [Header("玩家专属属性")]
    public float MoveSpeed = 5f;
    public float PickRange = 1f;
    public float UnbeatableTime = 0.2f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.MoveSpeed, MoveSpeed);
        model.SetBaseValue(StatType.PlayerPickRange, PickRange);
        model.SetBaseValue(StatType.PlayerUnbeatableTime, UnbeatableTime);
    }
}
