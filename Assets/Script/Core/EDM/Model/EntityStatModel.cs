using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 运行时数值容器 — 每个实体一个实例
/// 持有基础值 + 修饰符列表，最终值在 GetStat() 时实时聚合
/// </summary>
public class EntityStatModel
{
    private readonly Dictionary<StatType, float> _baseValues = new();
    private readonly Dictionary<StatType, List<StatModifier>> _modifiers = new();

    /// <summary>
    /// 某个数值发生变化时触发，参数为变更的 StatType
    /// 实体可订阅此事件更新碰撞体/UI 等
    /// </summary>
    public event Action<StatType> OnStatChanged;

    #region 初始化

    /// <summary>
    /// 用 DataSO 提供的基础值列表初始化
    /// </summary>
    public void SetBaseValue(StatType type, float value)
    {
        _baseValues[type] = value;
    }

    /// <summary>
    /// 批量设置基础值（从 DataSO 调用）
    /// </summary>
    public void InitializeFromDefinitions(IEnumerable<StatDefinition> definitions)
    {
        foreach (var def in definitions)
        {
            _baseValues[def.Type] = def.BaseValue;
        }
    }

    #endregion

    #region 查询

    /// <summary>
    /// 获取指定数值的最终值（基础值 + 所有修饰符聚合）
    /// </summary>
    public float GetStat(StatType type)
    {
        if (!_baseValues.ContainsKey(type))
            return 0f;

        float baseVal = _baseValues[type];

        // 检查是否有 Override 修饰符（最高优先级）
        if (_modifiers.TryGetValue(type, out var mods))
        {
            var overrideMod = mods.FirstOrDefault(m => m.ModifierType == EModifierType.Override);
            if (overrideMod != null)
                return overrideMod.Value;

            // 加法聚合
            float addSum = mods.Where(m => m.ModifierType == EModifierType.Add).Sum(m => m.Value);
            // 乘法聚合
            float multiplySum = mods.Where(m => m.ModifierType == EModifierType.Multiply).Sum(m => m.Value);

            return (baseVal + addSum) * (1f + multiplySum);
        }

        return baseVal;
    }

    public bool HasStat(StatType type)
    {
        return _baseValues.ContainsKey(type);
    }

    /// <summary>
    /// 获取基础值（不含修饰符）
    /// </summary>
    public float GetBaseStat(StatType type)
    {
        return _baseValues.TryGetValue(type, out var val) ? val : 0f;
    }

    #endregion

    #region 修饰符管理

    /// <summary>
    /// 添加一个运行时修饰符，立即生效
    /// </summary>
    public void AddModifier(StatModifier modifier)
    {
        if (!_modifiers.ContainsKey(modifier.TargetStat))
            _modifiers[modifier.TargetStat] = new List<StatModifier>();

        _modifiers[modifier.TargetStat].Add(modifier);
        OnStatChanged?.Invoke(modifier.TargetStat);
    }

    /// <summary>
    /// 批量添加修饰符
    /// </summary>
    public void AddModifiers(IEnumerable<StatModifier> modifiers)
    {
        foreach (var mod in modifiers)
        {
            AddModifier(mod);
        }
    }

    /// <summary>
    /// 移除某个来源的所有修饰符（如卸载装备/移除 buff）
    /// </summary>
    public void RemoveModifiersFromSource(object source)
    {
        var typesToNotify = new HashSet<StatType>();

        foreach (var kvp in _modifiers)
        {
            int removed = kvp.Value.RemoveAll(m => m.Source == source);
            if (removed > 0)
                typesToNotify.Add(kvp.Key);
        }

        foreach (var type in typesToNotify)
        {
            OnStatChanged?.Invoke(type);
        }
    }

    #endregion
}
