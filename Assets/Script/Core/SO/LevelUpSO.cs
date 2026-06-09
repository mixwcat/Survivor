using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 升级选项 SO
/// - statModifiers：数据驱动的数值修改（策划填表即可，不需写代码）
/// - fullHeal / bonusHeal：回血效果（数据驱动）
/// - onApplyEffect：仅用于武器选择（ChooseWeaponPanel），不涉及数值修改
///
/// 使用方式：
///   simpleUpgradeSO.ApplyTo(targetEntity);
///   -> 应用 StatModifier → 回血 → 完成
/// </summary>
[CreateAssetMenu(fileName = "LevelUpSO", menuName = "Game/Selection/Stat Modifier")]
public class LevelUpSO : ScriptableObject
{
    [Header("UI 显示")]
    public string levelUpText;
    public int cost = 1;
    public Sprite levelUpSprite;

    [Header("数值修改")]
    public List<StatModifierData> statModifiers = new();

    [Header("一次性回血效果")]
    [Tooltip("满血恢复（设置 CurrentHealth = MaxHealth）")]
    public bool fullHeal;
    [Tooltip("额外恢复血量（如 Teto recover: bonusHeal=60）")]
    public float bonusHeal;

    [Header("目标标签")]
    [Tooltip("用于 SOManager 升级池过滤，如 FireBall / Gun / Universal")]
    public List<string> targetTags = new();

    /// <summary>
    /// 武器选择回调（仅 ChooseWeaponPanel 使用，不涉及数值修改）
    /// </summary>
    public UnityAction onApplyEffect;

    /// <summary>
    /// 触发武器选择回调
    /// </summary>
    public void RaiseEvent()
    {
        onApplyEffect?.Invoke();
    }

    /// <summary>
    /// 将此升级应用到目标实体
    /// 1. 应用 StatModifier 到 StatModel
    /// 2. 满血恢复（如果有）
    /// 3. 额外回血（如果有）
    /// </summary>
    public void ApplyTo(EntityBehaviour entity)
    {
        if (entity?.StatModel == null) return;

        foreach (var modData in statModifiers)
        {
            var modifier = new StatModifier(modData.TargetStat, modData.Value, modData.ModifierType, this);
            entity.StatModel.AddModifier(modifier);
        }

        if (fullHeal)
        {
            entity.GetComponent<BaseHealthController>()?.FullHeal();
        }

        if (bonusHeal > 0f)
        {
            entity.GetComponent<BaseHealthController>()?.Heal(bonusHeal);
        }
    }
}
