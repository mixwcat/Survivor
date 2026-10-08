using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 群体治疗 —— 对范围内的**友军**各治疗一次。这是旧 <c>Luo</c> 的行为。
///
/// <para>
/// <b>「治疗谁」由攻击方式自己决定</b>（<see cref="TargetTag"/> = <c>"Tower"</c>），
/// 而不是由 driver 决定。这样给任意塔换上治疗攻击方式时，索敌会自动改成找友军，
/// 不会出现「治疗技能去治敌人」。
/// </para>
///
/// <para>
/// 治疗范围沿用 <c>StatType.AttackRange</c>（**武器自己的**数值 —— 检测圈长在武器上，
/// 半径由 <c>TowerWeaponRangeSync</c> 写入），所以这里不需要额外的范围数值。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "AttackHealAllies", menuName = "Game/Attack/Heal Allies")]
public class HealAlliesAttackSO : AttackMethodSO
{
    /// <summary>治疗的是友军。旧 <c>Luo.OnTriggerEnter2D</c> 硬编码的也是这个 tag。</summary>
    public override string TargetTag => "Tower";

    [Header("数值来源")]
    public StatType HealStat = StatType.HealAmount;

    /// <summary>治疗节奏走 <c>HealSpeed</c>（次/秒），与旧 <c>Luo.GetOperateInterval</c> 的节奏一致。</summary>
    public override float GetInterval(EntityBehaviour self)
    {
        float speed = self.GetStat(StatType.HealSpeed);
        return speed > 0f ? 1f / speed : float.PositiveInfinity;
    }

    /// <summary>
    /// 治疗量 + 治疗速率。间隔读的是 <c>HealSpeed</c> 而不是 <c>AttackSpeed</c>
    /// （见 <see cref="GetInterval"/>），校验必须按实际读取的数值来列。
    /// </summary>
    public override StatType[] RequiredStats =>
        new[] { HealStat, StatType.HealSpeed };

    public override bool Execute(in AttackContext ctx)
    {
        List<BaseHealthController> targets = ctx.TargetsInRange;
        if (targets == null || targets.Count == 0) return false;

        float healAmount = ctx.Self.GetStat(HealStat);

        bool anyHeal = false;
        for (int i = 0; i < targets.Count; i++)
        {
            BaseHealthController target = targets[i];
            if (target == null) continue;

            // 不治自己（旧 Luo.OnOperate 里的 `if (t == this) return;`）。
            // 比的是**宿主**：Self 是武器实例，它本来就不可治疗
            if (target.gameObject == ctx.Host.gameObject) continue;

            target.Heal(healAmount);
            anyHeal = true;
        }

        return anyHeal;
    }
}
