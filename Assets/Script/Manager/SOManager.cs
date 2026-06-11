using System;
using System.Collections.Generic;
using UnityEngine;

public class SOManager : MonoBehaviour
{
    [Header("单例模式")]
    private static SOManager instance;
    public static SOManager Instance => instance;

    [Header("实体配置注册表")]
    [Tooltip("统一管理所有 EntityType → EntitySO 映射，EntityBehaviour 通过 entityType 自动获取")]
    public List<EntitySOEntry> entitySOList = new();
    private Dictionary<EntityType, BaseEntitySO> _entitySOCache;

    [Header("升级")]
    public UpgradeCatalogSO upgradeCatalog;
    private UpgradeSelector _upgradeSelector;
    private LevelUpSO[] _preferPlayerSOs = new LevelUpSO[3];

    [Header("材质")]
    public Material towerHighlightMaterial;


    private void Awake()
    {
        instance = this;
        if (upgradeCatalog != null)
            _upgradeSelector = new UpgradeSelector(upgradeCatalog);
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
    /// 将 List<EntitySOEntry> 转换为 Dictionary 缓存
    /// </summary>
    private void BuildEntitySOCache()
    {
        _entitySOCache = new Dictionary<EntityType, BaseEntitySO>();
        if (entitySOList == null) return;

        foreach (var entry in entitySOList)
        {
            if (entry.entitySO == null) continue;
            _entitySOCache[entry.entityType] = entry.entitySO;
        }
    }

    /// <summary>
    /// 随机获取指定数量的玩家升级SO
    /// 来源包括：玩家通用配置 + 当前已激活武器的 WeaponEntitySO
    /// </summary>
    public LevelUpSO[] GetRandomPlayerLevelUpSOs(int count)
    {
        if (_upgradeSelector == null || upgradeCatalog == null)
        {
            var fallback = new LevelUpSO[count];
            for (int i = 0; i < count; i++)
                fallback[i] = upgradeCatalog?.defaultPlayerUpgrade;
            Debug.LogWarning("UpgradeSelector or UpgradeCatalog is null, returning fallback player upgrades.");
            return fallback;
        }

        var sources = new List<BaseEntitySO>();

        // 玩家通用升级来源
        if (upgradeCatalog.playerUpgradeSource != null)
            sources.Add(upgradeCatalog.playerUpgradeSource);

        // 已激活武器来源：从武器实例的 entityType 反查 WeaponEntitySO
        if (WeaponManager.Instance != null)
        {
            foreach (var weapon in WeaponManager.Instance.weapons)
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
        if (_upgradeSelector == null || upgradeCatalog == null)
        {
            var fallback = new LevelUpSO[count];
            for (int i = 0; i < count; i++)
                fallback[i] = upgradeCatalog?.defaultTowerUpgrade;
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

/// <summary>
/// EntitySO 注册表条目
/// </summary>
[Serializable]
public class EntitySOEntry
{
    [Tooltip("实体类型枚举")]
    public EntityType entityType;

    [Tooltip("对应的 EntitySO asset")]
    public BaseEntitySO entitySO;
}
