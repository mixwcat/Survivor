using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实体基础数据 SO 基类
/// 策划在此文件中配置实体的基础数值，子类添加专属字段
/// </summary>
public abstract class BaseEntityDataSO : ScriptableObject
{
    [Header("基本信息")]
    public string EntityName;

    [Header("共用基础属性")]
    public float MaxHealth = 100f;
    public float MoveSpeed = 0f;
    public float Damage = 10f;

    /// <summary>
    /// 将本 SO 中所有基础数值填充到运行时 StatModel 中
    /// 子类应 override 此方法，先调用 base.FillStatModel(model) 再添加专属字段
    /// </summary>
    public virtual void FillStatModel(EntityStatModel model)
    {
        model.SetBaseValue(StatType.MaxHealth, MaxHealth);
        model.SetBaseValue(StatType.MoveSpeed, MoveSpeed);
        model.SetBaseValue(StatType.Damage, Damage);
    }
}
