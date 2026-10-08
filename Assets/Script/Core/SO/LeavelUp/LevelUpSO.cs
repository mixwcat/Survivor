using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 升级选项 SO —— 纯数据：显示信息 + 数值修改 + 一次性回血。
///
/// 使用方式：
///   levelUpSO.ApplyTo(targetEntity);
///   -> 应用 StatModifier → 回血 → 完成
///
/// 注意：本 SO 上**不放运行时委托**（如 UnityAction）。SO 是常驻资产，
/// 编辑器关闭 Domain Reload 时它跨 Play 会话存活，委托会一直持有已销毁对象的伪 null 闭包。
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

    [Header("升级上限")]
    [Tooltip("同一目标最多可以应用几次。0 = 不限次（默认，保持旧行为）。\n" +
             "上限按**目标实例**计数（武器实例自己记，见 IUpgradeStateHolder）：\n" +
             "两个玩家各带一把同名武器，各自独立计数。")]
    public int maxLevel;

    [Header("一次性回血效果")]
    [Tooltip("满血恢复（设置 CurrentHealth = MaxHealth）")]
    public bool fullHeal;
    [Tooltip("额外恢复血量（如 Teto recover: bonusHeal=60）")]
    public float bonusHeal;

    /// <summary>
    /// 本升级的效果是否**不体现在数值上**（只由 <see cref="OnApplied"/> 实现，如"改装切换武器"）。
    ///
    /// <para>
    /// 默认 false。体检（<c>EntitySOValidator</c>）用 `statModifiers 为空且无回血` 判"空升级"，
    /// 那对纯数值升级是对的，但会把"效果是换形态"的升级误报成空 ——
    /// 子类重写为 true 即可豁免。**重写它就意味着"我确实有效果，只是不写数值"**，
    /// 不要拿它掩盖一个真的忘了填数值的升级。
    /// </para>
    /// </summary>
    public virtual bool HasCustomEffect => false;

    /// <summary>上限的规范化值：<c>&lt;= 0</c> 视为不限次（返回 0，调用方据此跳过上限判定）。</summary>
    public int MaxLevel => maxLevel > 0 ? maxLevel : 0;

    /// <summary>
    /// 此项在目标身上已经应用了几次（目标不记录状态时返回 0）。
    /// 供面板显示「Lv 2/3」与上限判定使用。
    /// </summary>
    public int GetAppliedLevel(EntityBehaviour target)
    {
        return target is IUpgradeStateHolder holder ? holder.GetUpgradeLevel(this) : 0;
    }

    /// <summary>
    /// 将此升级应用到目标实体
    /// 0. 上限与可用性检查（未通过则**什么都不做**）
    /// 1. 记一次应用（<see cref="IUpgradeStateHolder.TryRecordUpgrade"/>）
    /// 2. 应用 StatModifier 到 StatModel
    /// 3. 满血恢复（如果有）
    /// 4. 额外回血（如果有）
    /// 5. 子类扩展效果（<see cref="OnApplied"/>）
    /// </summary>
    /// <returns>是否真的应用了。false = 目标缺失 / 已达上限 / 被 <see cref="IsAvailable"/> 拒绝。</returns>
    public bool ApplyTo(EntityBehaviour entity)
    {
        if (entity == null) return false;

        // 先筛可用性（含上限），再记账 —— 顺序反了会把被拒绝的购买也计进等级
        if (!IsAvailable(entity)) return false;

        // 目标记录升级次数；不记录状态的实体（玩家）保持旧行为：可重复应用
        if (entity is IUpgradeStateHolder holder && !holder.TryRecordUpgrade(this)) return false;

        if (entity.StatModel != null)
        {
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

        OnApplied(entity);
        return true;
    }

    /// <summary>
    /// 这一项现在是否还该出现在选项列表里。默认：未达 <see cref="MaxLevel"/> 就为 true。
    ///
    /// <para>
    /// 子类用它表达"一次性选择"：例如塔的"改装为溅射炮弹"买过之后就不该再出现 ——
    /// 否则玩家会重复花点买同一件事。<b>子类重写时若不调 base，上限判定会被跳过</b>，
    /// 所以一次性项的语义（买过即消失）与上限是两条独立的闸。
    /// </para>
    ///
    /// <para>
    /// <b>目标为 null 时必须返回 true</b>：目标缺失（武器实例已销毁等）由
    /// <c>UpgradeSelector</c> 统一判为无效项并告警，不要在这里悄悄把选项吞掉。
    /// </para>
    /// </summary>
    public virtual bool IsAvailable(EntityBehaviour target)
    {
        if (MaxLevel <= 0) return true;
        if (target == null) return true;

        return GetAppliedLevel(target) < MaxLevel;
    }

    /// <summary>
    /// 子类扩展点：数值修改与回血之后执行，用于**非数值**效果（如改装武器形态）。
    /// 默认什么都不做。
    /// </summary>
    protected virtual void OnApplied(EntityBehaviour entity) { }
}
