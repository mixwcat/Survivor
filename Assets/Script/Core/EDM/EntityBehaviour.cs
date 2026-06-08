using UnityEngine;

/// <summary>
/// 实体基类 MonoBehaviour
/// - 持有 EntityStatModel 运行时数值容器
/// - 从 BaseEntityDataSO 加载基础数值
/// - 提供 GetStat / AddModifier 快捷访问
/// </summary>
public class EntityBehaviour : MonoBehaviour
{
    [Header("EDM 数值配置")]
    [SerializeField] protected BaseEntityDataSO _entityData;

    public EntityStatModel StatModel { get; private set; }
    public BaseEntityDataSO EntityData => _entityData;

    protected virtual void Awake()
    {
        StatModel = new EntityStatModel();
        _entityData?.FillStatModel(StatModel);
    }

    /// <summary>
    /// 获取最终数值（快捷方法）
    /// </summary>
    public float GetStat(StatType type) => StatModel?.GetStat(type) ?? 0f;

    /// <summary>
    /// 运行时替换 DataSO（用于动态配置）
    /// </summary>
    public void SetEntityData(BaseEntityDataSO data)
    {
        _entityData = data;
        StatModel = new EntityStatModel();
        _entityData?.FillStatModel(StatModel);
    }
}
