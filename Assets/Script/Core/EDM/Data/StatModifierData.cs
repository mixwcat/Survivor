using System;

/// <summary>
/// LevelUpSO 中配置的单条数值修改数据
/// 策划在此填写：修改哪个属性、多少数值、何种方式
/// </summary>
[Serializable]
public struct StatModifierData
{
    public StatType TargetStat;
    public float Value;
    public EModifierType ModifierType;
}
