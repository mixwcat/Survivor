using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 升级总目录 SO
/// 声明哪些实体参与玩家/塔升级池，自身不再持有 List&lt;LevelUpSO&gt;
/// 场景中 SOManager 只需引用此一个 SO
/// </summary>
[CreateAssetMenu(fileName = "UpgradeCatalog", menuName = "Game/Config/Upgrade Catalog")]
public class UpgradeCatalogSO : ScriptableObject
{
    [Header("玩家通用升级来源")]
    [Tooltip("玩家通用升级来自此实体的 upgrades")]
    public BaseEntitySO playerUpgradeSource;

    [Header("武器升级来源")]
    [Tooltip("所有可能的武器升级来源；运行时只取已激活武器的 upgrades")]
    public List<BaseEntitySO> weaponUpgradeSources = new();

    [Header("默认升级")]
    public LevelUpSO defaultPlayerUpgrade;
    public LevelUpSO defaultTowerUpgrade;
}
