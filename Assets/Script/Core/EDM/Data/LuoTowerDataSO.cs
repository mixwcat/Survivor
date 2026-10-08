using UnityEngine;

/// <summary>
/// Luo 治疗塔专属配置数据。
///
/// 与攻击塔 <see cref="TowerDataSO"/> **平级**（都挂在 <see cref="BaseTowerDataSO"/> 下）：
/// Luo 不造成伤害、没有子弹，所以这里看不到 Damage / AttackSpeed / HitForce / BulletSpeed。
/// 它读的是 HealAmount / HealSpeed；治疗范围与其它塔一样来自**武器**的
/// <c>WeaponDataSO.AttackRange</c>（即 <c>StatType.AttackRange</c>）。
/// </summary>
[CreateAssetMenu(fileName = "LuoTowerData", menuName = "Game/Data/Tower/Luo Tower")]
public class LuoTowerDataSO : BaseTowerDataSO
{
    [Header("Luo 治疗专属")]
    public float HealAmount = 0f;
    [Tooltip("治疗速率（次/秒）：1 = 每秒一次。数值越大越快")]
    public float HealSpeed = 0f;

    public override void FillStatModel(EntityStatModel model)
    {
        base.FillStatModel(model);
        model.SetBaseValue(StatType.HealAmount, HealAmount);
        model.SetBaseValue(StatType.HealSpeed, HealSpeed);
        // 注意：Luo 的治疗范围用的是武器自己的 AttackRange（由基类 WeaponDataSO 填入），
        // 塔身上不再持有射程；已删除的 StatType.HealRange（编号 15）保留空洞，禁止复用。
    }
}
