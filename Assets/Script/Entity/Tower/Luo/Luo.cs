using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Luo 塔 — 治疗
/// 所有数值属性从 StatModel 读取
/// 治疗范围直接使用 TowerAttackRange
/// </summary>
public class Luo : BaseTower
{
    private Animator _anim;
    private readonly List<BaseTower> _towersInRange = new List<BaseTower>();

    protected override void Start()
    {
        base.Start();
        // 根 Animator（Luo.controller）无参数；Heal 触发器在子物体 Bao 的 Animator 上
        _anim = FindAnimatorWithParameter("Heal");
    }

    private Animator FindAnimatorWithParameter(string parameter)
    {
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == parameter) return animator;
            }
        }
        return null;
    }

    protected override float GetOperateInterval()
    {
        return GetStat(StatType.HealInterval);
    }

    protected override void OnOperate()
    {
        if (_towersInRange.Count == 0) return;

        if (_anim != null) _anim.SetTrigger("Heal");
        AudioService.Service?.PlaySfx(ResourceEnum.Heal);

        float healAmount = GetStat(StatType.HealAmount);
        ForEachValidTarget(_towersInRange, t =>
        {
            if (t == this) return;
            t.GetComponent<BaseHealthController>()?.Heal(healAmount);
        });
    }

    protected override void CleanupCustomTargets()
    {
        RemoveNullTargets(_towersInRange);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryAddTarget(other, "Tower", _towersInRange);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        TryRemoveTarget(other, "Tower", _towersInRange);
    }
}
