using System.Collections.Generic;
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
    private LevelUpSO[] preferPlayerSOs = new LevelUpSO[3];

    [Header("塔SO列表")]
    public List<LevelUpSO> towerLevelUpSOs = new List<LevelUpSO>();


    /// <summary>
    /// 随机获取指定数量的升级SO
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public LevelUpSO[] GetRandomPlayerLevelUpSOs(int count)
    {
        List<LevelUpSO> selectedSOs = new List<LevelUpSO>();
        List<LevelUpSO> copyList = new List<LevelUpSO>(commonLevelUpSOs);

        // 判断当前武器类型，选择对应的升级SO列表
        if (WeaponManager.Instance.weapons[0] is SpinWeapon)
        {
            copyList.AddRange(fireBallLevelUpSOs);
        }
        else if (WeaponManager.Instance.weapons[0] is GunWeapon)
        {
            copyList.AddRange(shootGunLevelUpSOs);
        }

        // 随机选择指定数量的升级SO
        for (int i = 0; i < count; i++)
        {
            if (copyList.Count == 0) break;
            int index = Random.Range(0, copyList.Count);
            selectedSOs.Add(copyList[index]);
            Debug.Log("index" + index + "的名称: " + copyList[index].name);
            copyList.RemoveAt(index);
        }
        return selectedSOs.ToArray();
    }
    public LevelUpSO[] GetRandomTowerLevelUpSOs(int count)
    {
        List<LevelUpSO> selectedSOs = new List<LevelUpSO>();
        List<LevelUpSO> copyList = new List<LevelUpSO>(towerLevelUpSOs);

        // 随机选择指定数量的升级SO
        for (int i = 0; i < count; i++)
        {
            if (copyList.Count == 0) break;
            int index = Random.Range(0, copyList.Count);
            selectedSOs.Add(copyList[index]);
            copyList.RemoveAt(index);
        }
        return selectedSOs.ToArray();
    }


    /// <summary>
    /// 存储玩家未使用的升级SO
    /// </summary>
    /// <param name="so"></param>
    public void StorePreferSOs(LevelUpSO[] so)
    {
        preferPlayerSOs = so;
    }
    public LevelUpSO[] GetPreferSOs()
    {
        return preferPlayerSOs;
    }
}
