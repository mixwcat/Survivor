using System;
using System.Collections.Generic;

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
    /// 设置单个基础值（由 DataSO 的 FillStatModel 逐条调用）
    /// </summary>
    public void SetBaseValue(StatType type, float value)
    {
        _baseValues[type] = value;
    }

    #endregion

    #region 查询

    /// <summary>
    /// 获取指定数值的最终值（基础值 + 所有修饰符聚合）。
    /// <para>
    /// 性能敏感：实体每帧都会调用（敌人移动速度、武器转速、拾取范围等）。
    /// 因此这里**刻意不用 LINQ**——`FirstOrDefault/Where/Sum` 会为每次调用分配迭代器并装箱
    /// List 枚举器，几十个敌人 × 每帧数次 = 持续 GC 压力（Android 上表现为卡顿）。
    /// 下面用单次遍历替代，零堆分配，语义完全等价。
    /// </para>
    /// </summary>
    public float GetStat(StatType type)
    {
        if (!_baseValues.TryGetValue(type, out float baseVal))
            return 0f;

        if (!_modifiers.TryGetValue(type, out List<StatModifier> mods) || mods.Count == 0)
            return baseVal;

        float addSum = 0f;
        float multiplySum = 0f;

        for (int i = 0; i < mods.Count; i++)
        {
            StatModifier mod = mods[i];

            switch (mod.ModifierType)
            {
                // Override 优先级最高，命中即返回（与原先 FirstOrDefault 取第一个 Override 等价）
                case EModifierType.Override:
                    return mod.Value;

                case EModifierType.Add:
                    addSum += mod.Value;
                    break;

                case EModifierType.Multiply:
                    multiplySum += mod.Value;
                    break;
            }
        }

        return (baseVal + addSum) * (1f + multiplySum);
    }

    public bool HasStat(StatType type)
    {
        return _baseValues.ContainsKey(type);
    }

    /// <summary>
    /// 是否已经有任意基础数值（用于判断 DataSO 是否真的灌进了数值）
    /// </summary>
    public bool HasAnyStat()
    {
        return _baseValues.Count > 0;
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
    /// 移除某个来源的所有修饰符（如卸载装备/移除 buff）。
    ///
    /// <para>
    /// <b>它在敌人池的取出路径上</b>（<c>EnemyController.OnGetFromPool</c> 调两次：波次增强 + 哨站难度），
    /// 而生成压力最高约 20 次/秒 —— 所以这里**不能**有任何堆分配：
    /// 旧实现每次新建一个 <c>HashSet&lt;StatType&gt;</c>，并对每个列表调
    /// <c>RemoveAll(m =&gt; m.Source == source)</c>（每次都要分配一个闭包 + 一个委托）。
    /// 对象池消除了 GameObject 创建尖峰，却在复用入口留下了稳定的小对象分配。
    /// </para>
    /// </summary>
    public void RemoveModifiersFromSource(object source)
    {
        // 正常路径复用缓冲（零分配）。OnStatChanged 的订阅者若又调本方法，
        // 内层退化成一次性列表 —— 否则会把外层还没发完的类型清掉
        List<StatType> changed = _notifying ? new List<StatType>() : _notifyBuffer;
        bool borrowed = !_notifying;

        if (borrowed) _notifying = true;

        try
        {
            // Dictionary 的具体类型 foreach 用的是结构体枚举器，不装箱
            foreach (KeyValuePair<StatType, List<StatModifier>> pair in _modifiers)
            {
                List<StatModifier> mods = pair.Value;

                // 倒序原地删除：不用 RemoveAll(lambda)，那会分配闭包与委托
                int removed = 0;
                for (int i = mods.Count - 1; i >= 0; i--)
                {
                    if (!ReferenceEquals(mods[i].Source, source)) continue;

                    mods.RemoveAt(i);
                    removed++;
                }

                // 每个 StatType 在字典里只出现一次，所以天然不需要 HashSet 去重
                if (removed > 0) changed.Add(pair.Key);
            }

            // 通知统一放在改动之后：回调里读到的数值已经是最终值
            for (int i = 0; i < changed.Count; i++)
                OnStatChanged?.Invoke(changed[i]);
        }
        finally
        {
            if (borrowed)
            {
                _notifyBuffer.Clear();
                _notifying = false;
            }
        }
    }

    /// <summary>变更通知的复用缓冲（见 <see cref="RemoveModifiersFromSource"/>）。</summary>
    private readonly List<StatType> _notifyBuffer = new();

    /// <summary>是否正在发通知 —— 用于识别"订阅者在回调里又改修饰符"的重入。</summary>
    private bool _notifying;

    #endregion
}
