using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 升级随机选择器
/// 纯逻辑类，负责从 BaseEntitySO.upgrades 中合并、随机抽取
/// 无 MonoBehaviour 依赖，可独立测试
/// </summary>
public class UpgradeSelector
{
    private readonly UpgradeCatalogSO _catalog;

    public UpgradeSelector(UpgradeCatalogSO catalog)
    {
        _catalog = catalog;
    }

    /// <summary>
    /// 随机获取指定数量的玩家升级SO
    /// 合并传入的 sources 中各实体的 upgrades 后随机抽取
    /// </summary>
    public LevelUpSO[] GetRandomPlayerUpgrades(List<BaseEntitySO> sources, int count)
    {
        var selectedSOs = new List<LevelUpSO>();

        // 合并各实体 SO 的 upgrades
        var copyList = new List<LevelUpSO>();
        if (sources != null)
        {
            foreach (var entity in sources)
            {
                if (entity == null || entity.upgrades == null) continue;
                copyList.AddRange(entity.upgrades);
            }
        }

        if (copyList.Count == 0)
        {
            for (int i = 0; i < count; i++)
                selectedSOs.Add(_catalog.defaultPlayerUpgrade);
            return selectedSOs.ToArray();
        }

        for (int i = 0; i < count; i++)
        {
            if (copyList.Count == 0)
            {
                selectedSOs.Add(_catalog.defaultPlayerUpgrade);
                break;
            }
            int index = Random.Range(0, copyList.Count);
            selectedSOs.Add(copyList[index]);
            copyList.RemoveAt(index);
        }
        return selectedSOs.ToArray();
    }

    /// <summary>
    /// 随机获取指定数量的塔升级SO
    /// 直接从 towerEntity.upgrades 中抽取
    /// </summary>
    public LevelUpSO[] GetRandomTowerUpgrades(BaseEntitySO towerEntity, int count)
    {
        var selectedSOs = new List<LevelUpSO>();
        var copyList = new List<LevelUpSO>();

        if (towerEntity != null && towerEntity.upgrades != null)
            copyList.AddRange(towerEntity.upgrades);

        for (int i = 0; i < count; i++)
        {
            if (copyList.Count == 0)
            {
                copyList.Add(_catalog.defaultTowerUpgrade);
            }
            int index = Random.Range(0, copyList.Count);
            selectedSOs.Add(copyList[index]);
            copyList.RemoveAt(index);
        }
        return selectedSOs.ToArray();
    }
}
