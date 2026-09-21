using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-140)]
public class SOManager : ManagerSingleton<SOManager>, ISOManager
{
    [Header("实体配置目录")]
    [Tooltip("统一管理所有 EntityType → EntitySO 映射，EntityBehaviour 通过 entityType 自动获取")]
    public EntityCatalogSO entityCatalog;
    private Dictionary<EntityType, BaseEntitySO> _entitySOCache;

    [Header("升级")]
    private UpgradeSelector _upgradeSelector;
    private LevelUpSO[] _preferPlayerSOs = new LevelUpSO[3];

    [Header("材质")]
    public Material towerHighlightMaterial;

    /// <summary>ISOManager：塔高亮材质</summary>
    public Material TowerHighlightMaterial => towerHighlightMaterial;

    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static ISOManager Service =>
        ServiceLocator.TryGet<ISOManager>(out var svc) ? svc : null;


    protected override void OnSingletonAwake()
    {
        if (entityCatalog != null)
            _upgradeSelector = new UpgradeSelector(entityCatalog);

        ServiceLocator.Register<ISOManager>(this);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        ServiceLocator.Unregister<ISOManager>();
    }

    /// <summary>
    /// 根据 EntityType 获取对应的 EntitySO
    /// 首次调用时构建 Dictionary 缓存，后续为 O(1) 查找
    /// </summary>
    public BaseEntitySO GetEntitySO(EntityType entityType)
    {
        if (_entitySOCache == null)
            BuildEntitySOCache();

        _entitySOCache.TryGetValue(entityType, out var so);
        return so;
    }

    /// <summary>
    /// 将 EntityCatalogSO 中所有列表合并为 Dictionary 缓存
    /// 重复 EntityType 会输出警告，后写入的覆盖先写入的
    /// </summary>
    private void BuildEntitySOCache()
    {
        _entitySOCache = new Dictionary<EntityType, BaseEntitySO>();
        if (entityCatalog == null)
        {
            Debug.LogWarning("[SOManager] entityCatalog 未赋值，无法构建 EntitySO 缓存。");
            return;
        }

        var duplicates = new List<string>();

        void Add(BaseEntitySO so, string listName)
        {
            if (so == null) return;
            if (_entitySOCache.ContainsKey(so.entityType))
            {
                duplicates.Add($"{listName}: {so.name} ({so.entityType})");
            }
            _entitySOCache[so.entityType] = so;
        }

        Add(entityCatalog.playerEntity, nameof(entityCatalog.playerEntity));
        foreach (var so in entityCatalog.weaponEntities) Add(so, nameof(entityCatalog.weaponEntities));
        foreach (var so in entityCatalog.towerEntities) Add(so, nameof(entityCatalog.towerEntities));
        foreach (var so in entityCatalog.enemyEntities) Add(so, nameof(entityCatalog.enemyEntities));

        if (duplicates.Count > 0)
        {
            Debug.LogWarning($"[SOManager] EntityCatalogSO 中存在重复的 EntityType，后出现的已覆盖先出现的：\n{string.Join("\n", duplicates)}");
        }
    }

    /// <summary>
    /// 随机获取指定数量的玩家升级SO
    /// 来源包括：玩家通用配置 + 当前已激活武器的 WeaponEntitySO
    /// </summary>
    public LevelUpSO[] GetRandomPlayerLevelUpSOs(int count)
    {
        if (_upgradeSelector == null || entityCatalog == null)
        {
            var fallback = new LevelUpSO[count];
            for (int i = 0; i < count; i++)
                fallback[i] = entityCatalog?.defaultPlayerUpgrade;
            Debug.LogWarning("UpgradeSelector or EntityCatalog is null, returning fallback player upgrades.");
            return fallback;
        }

        var sources = new List<BaseEntitySO>();

        // 玩家通用升级来源
        if (entityCatalog.playerEntity != null)
            sources.Add(entityCatalog.playerEntity);

        // 已激活武器来源：取本地玩家的武器（按玩家实例化）
        PlayerController local = PlayerManager.Service?.LocalPlayer;
        if (local?.Weapons != null)
        {
            foreach (var weapon in local.Weapons.Weapons)
            {
                if (weapon?.EntityConfig is WeaponEntitySO weaponSO)
                    sources.Add(weaponSO);
            }
        }

        return _upgradeSelector.GetRandomPlayerUpgrades(sources, count);
    }

    /// <summary>
    /// 随机获取指定数量的塔升级SO
    /// </summary>
    public LevelUpSO[] GetRandomTowerLevelUpSOs(int count, BaseTower towerType)
    {
        if (_upgradeSelector == null || entityCatalog == null)
        {
            var fallback = new LevelUpSO[count];
            for (int i = 0; i < count; i++)
                fallback[i] = entityCatalog?.defaultTowerUpgrade;
            return fallback;
        }

        return _upgradeSelector.GetRandomTowerUpgrades(towerType?.EntityConfig, count);
    }

    /// <summary>
    /// 存储玩家未使用的升级SO
    /// </summary>
    public void StorePreferSOs(LevelUpSO[] so)
    {
        _preferPlayerSOs = so;
    }

    public LevelUpSO[] GetPreferSOs()
    {
        return _preferPlayerSOs;
    }
}
