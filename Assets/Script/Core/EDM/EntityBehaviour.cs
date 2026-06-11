using UnityEngine;

/// <summary>
/// 实体基类 MonoBehaviour
/// - 持有 EntityStatModel 运行时数值容器
/// - 通过 EntityType enum 从 SOManager 统一获取 EntitySO，再读取 DataSO
/// - 提供 GetStat / AddModifier 快捷访问
/// </summary>
public class EntityBehaviour : MonoBehaviour
{
    [Header("实体类型")]
    [Tooltip("对应 SOManager.entitySORegistry 中的 EntityType")]
    [SerializeField] protected EntityType entityType;

    private BaseEntitySO _entityConfig;

    public EntityStatModel StatModel { get; private set; }

    /// <summary>
    /// 运行时从 SOManager 获取的 EntitySO
    /// </summary>
    public BaseEntitySO EntityConfig
    {
        get
        {
            if (_entityConfig == null)
                _entityConfig = SOManager.Instance?.GetEntitySO(entityType);
            return _entityConfig;
        }
    }

    public BaseEntityDataSO EntityData => EntityConfig?.dataRef;

    protected virtual void Awake()
    {
        InitStatModel();
    }

    void Start()
    {
        // 防止 SOManager 还未完成 Awake 导致初始化失败，在 Start 中重试一次
        if (StatModel == null || EntityConfig == null)
            InitStatModel();
    }

    private void InitStatModel()
    {
        if (StatModel != null) return;

        StatModel = new EntityStatModel();
        _entityConfig = SOManager.Instance?.GetEntitySO(entityType);
        _entityConfig?.dataRef?.FillStatModel(StatModel);

        if (_entityConfig == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 EntitySO ，无法初始化 StatModel");
        }
        else if (_entityConfig.dataRef == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 DataSO，无法初始化 StatModel");
        }
    }

    /// <summary>
    /// 获取最终数值（快捷方法）
    /// </summary>
    public float GetStat(StatType type)
    {
        if (StatModel == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 StatModel，返回默认值 1");
            return 1f;
        }
        else if (EntityConfig?.dataRef == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 DataSO，无法获取 " + type + "，返回默认值 1");
            return 1f;
        }
        else if (!StatModel.HasStat(type))
        {
            Debug.LogWarning(gameObject.name + " 缺少Type： " + type + "，返回默认值 1");
            return 1f;
        }
        return StatModel.GetStat(type);
    }

    /// <summary>
    /// 运行时替换实体类型（用于动态配置）
    /// </summary>
    public void SetEntityType(EntityType type)
    {
        entityType = type;
        _entityConfig = null;
        StatModel = null;
        InitStatModel();
    }
}
