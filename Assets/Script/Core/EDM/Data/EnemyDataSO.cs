using UnityEngine;

/// <summary>
/// 敌人基础配置数据。
///
/// 三个数值都有实际读取方：<c>EnemyController</c> 读 MoveSpeed，
/// <c>EnemyHealthController</c> 通过 <c>BaseHealthController.Damage</c> 读 Damage（接触伤害），
/// <c>EnemyHealthController.Die</c> 读 ExpReward 决定掉落多少经验。
/// </summary>
[CreateAssetMenu(fileName = "EnemyData", menuName = "Game/Data/Enemy Data")]
public class EnemyDataSO : BaseActorDataSO
{
    [Header("敌人专属属性")]
    public float MoveSpeed = 1f;
    public float Damage = 10f;
    public float ExpReward = 1f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.MoveSpeed, MoveSpeed);
        model.SetBaseValue(StatType.Damage, Damage);
        model.SetBaseValue(StatType.ExpReward, ExpReward);
    }
}
