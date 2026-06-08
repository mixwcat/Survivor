/// <summary>
/// 运行时数值修饰符，由 StatModel 持有
/// Source 标记来源（如某个 LevelUpSO），方便统一移除
/// </summary>
public class StatModifier
{
    public StatType TargetStat { get; }
    public float Value { get; }
    public EModifierType ModifierType { get; }
    public object Source { get; }

    public StatModifier(StatType targetStat, float value, EModifierType modifierType, object source)
    {
        TargetStat = targetStat;
        Value = value;
        ModifierType = modifierType;
        Source = source;
    }
}
