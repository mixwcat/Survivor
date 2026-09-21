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
            {
                _entityConfig = SOManager.Service?.GetEntitySO(entityType);
            }
            return _entityConfig;
        }
    }

    public BaseEntityDataSO EntityData => EntityConfig?.dataRef;

    protected virtual void Awake()
    {
        // InitStatModel();
    }

    void Start()
    {
        // 防止 SOManager 还未完成 Awake 导致初始化失败，在 Start 中重试一次
        if (StatModel == null || !StatModel.HasAnyStat())
            InitStatModel();
    }

    private void InitStatModel()
    {
        // 已经完整初始化：有 StatModel 且里面已有基础数值
        if (StatModel != null && StatModel.HasAnyStat()) return;

        if (SOManager.Service == null)
        {
            Debug.LogWarning(gameObject.name + " 无法获取 SOManager 实例，无法初始化 StatModel");
            return;
        }

        var config = SOManager.Service?.GetEntitySO(entityType);
        _entityConfig = config;

        if (config == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 EntitySO ，无法初始化 StatModel");
            return;
        }

        if (config.dataRef == null)
        {
            Debug.LogWarning(gameObject.name + " 缺少 DataSO，无法初始化 StatModel");
            return;
        }

        StatModel = new EntityStatModel();
        config.dataRef.FillStatModel(StatModel);
    }

    /// <summary>
    /// 获取最终数值（快捷方法）
    /// 若 SOManager 晚于 Awake/Start 就绪，会尝试懒加载 StatModel
    /// </summary>
    public float GetStat(StatType type)
    {
        // 运行时补初始化：处理 SOManager 初始化时序晚于实体的情况
        if (StatModel == null || !StatModel.HasAnyStat())
            InitStatModel();

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
            Debug.LogWarning(gameObject.name + "的" + EntityConfig.dataRef.name + " 缺少Type： " + type + "，返回默认值 1");
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
