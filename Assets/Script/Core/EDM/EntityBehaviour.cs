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
    [Tooltip("优先使用 Inspector 配置；为空时尝试从 EntityDataRegistry 自动查找")]
    [SerializeField] protected BaseEntityDataSO _entityData;

    [Tooltip("在 EntityDataRegistry 中注册的标识，如 Player / Enemy / Tower_Teto / Weapon_FireBall")]
    [SerializeField] protected string _registryId;

    public EntityStatModel StatModel { get; private set; }
    public BaseEntityDataSO EntityData => _entityData;

    protected virtual void Awake()
    {
        StatModel = new EntityStatModel();

        // 如果 Inspector 没配 _entityData，尝试从 Registry 自动查找
        if (_entityData == null && !string.IsNullOrEmpty(_registryId))
        {
            _entityData = SOManager.Instance?.entityDataRegistry?.GetData(_registryId);
        }

        _entityData?.FillStatModel(StatModel);
    }

    /// <summary>
    /// 获取最终数值（快捷方法）
    /// </summary>
    public float GetStat(StatType type)
    {
        if (StatModel == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 StatModel，返回默认值 1");
            return 1f; // 返回默认值，避免崩溃
        }
        else if (_entityData == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 EntityData，无法获取 " + type + "，返回默认值 1");
            return 1f; // 返回默认值，避免崩溃
        }
        else if (!StatModel.HasStat(type))
        {
            Debug.LogWarning(gameObject.name + " 缺少Type： " + type + "，返回默认值 1");
            return 1f; // 返回默认值，避免崩溃
        }
        return StatModel.GetStat(type);
    }

    /// <summary>
    /// 运行时替换 DataSO（用于动态配置）
    /// </summary>
    public void SetEntityData(BaseEntityDataSO data)
    {
        _entityData = data;
        StatModel = new EntityStatModel();
        _entityData?.FillStatModel(StatModel);
    }

    /// <summary>
    /// 编辑器模式下自动填充 Registry Id（可选）
    /// </summary>
#if UNITY_EDITOR
    void OnValidate()
    {
        if (string.IsNullOrEmpty(_registryId))
            _registryId = gameObject.name;
    }
#endif
}
