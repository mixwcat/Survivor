using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject 配置查询服务（只读）。
/// 配置数据在所有客户端间共享，联机模式下无需替换实现。
/// </summary>
public interface ISOManager
{
    /// <summary>按 EntityType 获取对应的 EntitySO</summary>
    BaseEntitySO GetEntitySO(EntityType entityType);

    /// <summary>塔高亮材质</summary>
    Material TowerHighlightMaterial { get; }

    /// <summary>随机获取玩家升级选项（玩家通用 + 已激活武器）</summary>
    LevelUpSO[] GetRandomPlayerLevelUpSOs(int count);

    /// <summary>随机获取塔升级选项</summary>
    LevelUpSO[] GetRandomTowerLevelUpSOs(int count, BaseTower tower);

    /// <summary>缓存当前展示给玩家的升级选项</summary>
    void StorePreferSOs(LevelUpSO[] so);

    /// <summary>取回缓存的升级选项</summary>
    LevelUpSO[] GetPreferSOs();
}
