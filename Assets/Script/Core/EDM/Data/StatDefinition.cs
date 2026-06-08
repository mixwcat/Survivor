using System;

/// <summary>
/// DataSO 中单条数值定义 — 标识一个 StatType 的基础值
/// </summary>
[Serializable]
public struct StatDefinition
{
    public StatType Type;
    public float BaseValue;

    public StatDefinition(StatType type, float baseValue)
    {
        Type = type;
        BaseValue = baseValue;
    }
}
