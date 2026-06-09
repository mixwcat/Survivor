using UnityEngine;

/// <summary>
/// 武器基础数据 SO（抽象基类）
/// 只保留所有武器共用的通用属性
/// 每种武器请使用对应的子类创建 asset：SpinWeaponDataSO / GunWeaponDataSO
/// </summary>
public abstract class WeaponDataSO : BaseEntityDataSO
{
    [Header("通用武器属性")]
    public float AttackInterval = 1f;

    [Header("投射物 Prefab")]
    [Tooltip("为空则回退到 Resources.Load")]
    public GameObject projectilePrefab;

    public override void FillStatModel(EntityStatModel model)
    {
        // 武器不设 BaseDamage / BaseMoveSpeed，只设自己的字段
        model.SetBaseValue(StatType.AttackInterval, AttackInterval);
    }
}
