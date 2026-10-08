using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 范围伤害 —— 对索敌范围内的**全部**目标各结算一次伤害，没有投射物。
/// 这是旧 <c>Rin</c> 的行为（范围内全体 + 攻击动画）。
///
/// <para>
/// 遍历的是 driver 拍的**快照**（见 <c>AttackDriver.BuildContext</c>）：
/// 打死一个目标会让它回对象池、碰撞体失效，塔上随即收到 <c>OnTriggerExit2D</c>
/// 去改原列表 —— 直接按索引遍历原列表会越界（旧 <c>BaseTower.ForEachValidTarget</c>
/// 当年就是踩了这个坑才改成快照的）。
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "AttackAreaDamage", menuName = "Game/Attack/Area Damage")]
public class AreaDamageAttackSO : AttackMethodSO
{
    [Header("数值来源（读发起攻击的实体自己的 StatModel）")]
    public StatType DamageStat = StatType.Damage;
    [Tooltip("击退力度。Rin 的 TowerHitForce 是 0，所以默认等价于旧行为的「不击退」")]
    public StatType ForceStat = StatType.TowerHitForce;

    /// <summary>
    /// 间隔走基类的 <c>AttackSpeed</c>（本类不重写 <see cref="GetInterval"/>），
    /// 所以它也必须由目标实体的 DataSO 提供。
    /// </summary>
    public override StatType[] RequiredStats =>
        new[] { DamageStat, ForceStat, StatType.AttackSpeed };

    public override bool Execute(in AttackContext ctx)
    {
        List<BaseHealthController> targets = ctx.TargetsInRange;
        if (targets == null || targets.Count == 0) return false;

        float damage = ctx.Self.GetStat(DamageStat);
        float hitForce = ctx.Self.GetStat(ForceStat);

        bool anyHit = false;
        for (int i = 0; i < targets.Count; i++)
        {
            BaseHealthController target = targets[i];
            if (target == null) continue;

            // 归属用 Host（武器打死敌人算宿主的），数值用 Self（武器自己的 StatModel）
            target.TakeDamage(new DamageInfo(damage, hitForce, ctx.Host, DamageSource.Area));
            anyHit = true;
        }

        return anyHit;
    }
}
