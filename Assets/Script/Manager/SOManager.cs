using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SOManager : MonoBehaviour
{
    [Header("单例模式")]
    private static SOManager instance;
    public static SOManager Instance => instance;
    private void Awake()
    {
        instance = this;
    }

    [Header("人物SO列表")]
    public List<LevelUpSO> fireBallLevelUpSOs = new List<LevelUpSO>();
    public List<LevelUpSO> shootGunLevelUpSOs = new List<LevelUpSO>();
    public List<LevelUpSO> commonLevelUpSOs = new List<LevelUpSO>();
    public LevelUpSO defaultPlayerSO;
    private LevelUpSO[] preferPlayerSOs = new LevelUpSO[3];

    [Header("塔SO列表")]
    public List<LevelUpSO> tetoLevelUpSOs = new List<LevelUpSO>();
    public List<LevelUpSO> luoLevelUpSOs = new List<LevelUpSO>();
    public List<LevelUpSO> rinLevelUpSOs = new List<LevelUpSO>();
    public LevelUpSO defaultTowerSO;

    [Header("材质")]
    public Material towerHighlightMaterial;

    [Header("实体数据注册表")]
    [Tooltip("统一配置所有 Entity → DataSO 映射，EntityBehaviour 通过 _registryId 自动查找")]
    public EntityDataRegistry entityDataRegistry;


    /// <summary>
    /// 收集当前已激活武器的所有标签
    /// </summary>
    private HashSet<string> GetActiveWeaponTags()
    {
        var tags = new HashSet<string>();
        if (WeaponManager.Instance == null) return tags;

        foreach (var weapon in WeaponManager.Instance.weapons)
        {
            if (weapon == null) continue;
            foreach (var tag in weapon.weaponTags)
            {
                tags.Add(tag);
            }
        }
        return tags;
    }

    /// <summary>
    /// 过滤与当前武器标签匹配的升级SO
    /// 支持 Universal 标签（对所有武器生效）
    /// </summary>
    private List<LevelUpSO> FilterUpgradesByTags(List<LevelUpSO> sourceList)
    {
        var weaponTags = GetActiveWeaponTags();
        if (weaponTags.Count == 0)
            return new List<LevelUpSO>(sourceList);

        return sourceList.Where(so =>
            so.targetTags == null ||
            so.targetTags.Count == 0 ||
            so.targetTags.Contains("Universal") ||
            so.targetTags.Any(tag => weaponTags.Contains(tag))
        ).ToList();
    }

    /// <summary>
    /// 随机获取指定数量的玩家升级SO
    /// 根据当前已激活武器的标签过滤专属升级
    /// </summary>
    public LevelUpSO[] GetRandomPlayerLevelUpSOs(int count)
    {
        List<LevelUpSO> selectedSOs = new List<LevelUpSO>();

        // 合并通用升级和武器专属升级
        List<LevelUpSO> copyList = new List<LevelUpSO>(commonLevelUpSOs);
        copyList.AddRange(fireBallLevelUpSOs);
        copyList.AddRange(shootGunLevelUpSOs);

        // 按标签过滤
        copyList = FilterUpgradesByTags(copyList);

        if (copyList.Count == 0 || WeaponManager.Instance.weapons.Count == 0)
        {
            for (int i = 0; i < count; i++)
            {
                selectedSOs.Add(defaultPlayerSO);
            }
            return selectedSOs.ToArray();
        }

        // 随机选择指定数量的升级SO
        for (int i = 0; i < count; i++)
        {
            if (copyList.Count == 0)
            {
                selectedSOs.Add(defaultPlayerSO);
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
    /// </summary>
    public LevelUpSO[] GetRandomTowerLevelUpSOs(int count, BaseTower towerType)
    {
        List<LevelUpSO> selectedSOs = new List<LevelUpSO>();
        List<LevelUpSO> copyList = new List<LevelUpSO>();
        switch (towerType)
        {
            case Teto:
                copyList.AddRange(tetoLevelUpSOs);
                break;
            case Luo:
                copyList.AddRange(luoLevelUpSOs);
                break;
            case Rin:
                copyList.AddRange(rinLevelUpSOs);
                break;
        }


        // 随机选择指定数量的升级SO
        for (int i = 0; i < count; i++)
        {
            if (copyList.Count == 0)
            {
                copyList.Add(defaultTowerSO);
            }
            int index = Random.Range(0, copyList.Count);
            selectedSOs.Add(copyList[index]);
            copyList.RemoveAt(index);
        }
        return selectedSOs.ToArray();
    }


    /// <summary>
    /// 存储玩家未使用的升级SO
    /// </summary>
    public void StorePreferSOs(LevelUpSO[] so)
    {
        preferPlayerSOs = so;
    }
    public LevelUpSO[] GetPreferSOs()
    {
        return preferPlayerSOs;
    }
}