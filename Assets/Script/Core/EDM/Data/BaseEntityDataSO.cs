using UnityEngine;

/// <summary>
/// 实体数据 SO 基类 —— 只放**所有实体真正共有**的东西，目前是「没有数值」。
///
/// 这里刻意**不**放 MaxHealth / MoveSpeed / Damage：它们只对部分类别成立
/// （武器没有血量、塔不移动、Luo 不造成伤害）。放在基类会让每个类别都在 Inspector 里
/// 看到一批用不上、改了也没效果的字段 —— 这是测试反馈里最直接的困扰来源。
///
/// 数值一律由下面两支持有：
/// <list type="bullet">
/// <item>有血量、会被打的实体（玩家 / 敌人 / 塔）→ <see cref="BaseActorDataSO"/></item>
/// <item>武器 → <see cref="WeaponDataSO"/></item>
/// </list>
/// </summary>
public abstract class BaseEntityDataSO : ScriptableObject
{
    /// <summary>
    /// 把本 SO 的数值灌进运行时 StatModel。
    /// <para>
    /// 每层只写自己那一层的字段，子类必须 override 并**先调用 <c>base.FillStatModel(model)</c>**，
    /// 否则上层（如 <see cref="BaseActorDataSO"/> 的 MaxHealth）不会被填 ——
    /// 后果是所有数值静默退化成 1（<see cref="EntityBehaviour.GetStat"/> 的兜底值）。
    /// </para>
    /// <para>基类本身没有数值可填，保留这个空实现是为了让各层的 <c>base</c> 调用链不断。</para>
    /// </summary>
    public virtual void FillStatModel(EntityStatModel model) { }
}
