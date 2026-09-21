using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实体总目录 SO
/// 集中持有所有实体配置（玩家、武器、塔、敌人），作为 SOManager 的唯一配置来源
/// 其中玩家/武器/塔的 upgrades 同时用于升级池；敌人仅提供基础数值配置
/// </summary>
[CreateAssetMenu(fileName = "EntityCatalog", menuName = "Game/Entity/Entity Catalog")]
public class EntityCatalogSO : ScriptableObject
{
    [Header("玩家实体")]
    [Tooltip("玩家配置；其 upgrades 作为玩家通用升级来源")]
    public BaseEntitySO playerEntity;

    [Header("武器实体")]
    [Tooltip("所有武器配置；当前全部作为玩家升级来源")]
    public List<BaseEntitySO> weaponEntities = new();

    [Header("防御塔实体")]
    [Tooltip("所有防御塔配置；用于塔升级与数值加载")]
    public List<BaseEntitySO> towerEntities = new();

    [Header("敌人实体")]
    [Tooltip("所有敌人配置；仅用于数值加载，不参与升级池")]
    public List<BaseEntitySO> enemyEntities = new();

    [Header("默认升级")]
    public LevelUpSO defaultPlayerUpgrade;
    public LevelUpSO defaultTowerUpgrade;

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器校验：检查重复 EntityType 与空引用
    /// </summary>
    void OnValidate()
    {
        var seen = new HashSet<EntityType>();
        var duplicates = new List<string>();

        void Check(BaseEntitySO so, string listName)
        {
            if (so == null) return;
            if (seen.Contains(so.entityType))
            {
                duplicates.Add($"{listName}: {so.name} ({so.entityType})");
            }
            else
            {
                seen.Add(so.entityType);
            }
        }

        Check(playerEntity, nameof(playerEntity));
        foreach (var so in weaponEntities) Check(so, nameof(weaponEntities));
        foreach (var so in towerEntities) Check(so, nameof(towerEntities));
        foreach (var so in enemyEntities) Check(so, nameof(enemyEntities));

        if (duplicates.Count > 0)
        {
            Debug.LogWarning($"[EntityCatalogSO] 发现重复的 EntityType:\n{string.Join("\n", duplicates)}", this);
        }
    }
#endif
}
