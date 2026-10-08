using UnityEngine;

/// <summary>
/// 推车数值配置。
///
/// <para>
/// 复用 <see cref="StatType.MoveSpeed"/> 而不是新增"车速"数值：已有数值的语义完全一致
/// （单位/秒的移动速度），新增一个只会让"改哪个生效"变成需要读代码才能回答的问题
/// （见清单 P9：能复用现有数值时不新增）。
/// </para>
///
/// <para>
/// <b>恢复参数不在这里</b>：停摆秒数与恢复比例是**关卡规则**而不是实体属性，
/// 归 <c>StageDirector</c>（场景里可覆盖）。这里曾经有一对同名字段，
/// 全仓没有读取者 —— 策划改了没效果，而 CHANGELOG 与实际值也对不上。
/// 规则只有一处来源，才不会出现"两个数到底哪个生效"。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "CartData", menuName = "Game/Data/Cart Data")]
public class CartDataSO : BaseActorDataSO
{
    [Header("推车专属属性")]
    [Tooltip("行驶速度（单位/秒）。注意推车 prefab 上**不要**再留速度覆盖字段")]
    public float MoveSpeed = 1.5f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.MoveSpeed, MoveSpeed);
    }
}
