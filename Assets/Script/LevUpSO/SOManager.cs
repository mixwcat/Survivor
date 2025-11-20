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

    [Header("等级提升SO列表")]
    public List<LevelUpSO> fireBallLevelUpSOs = new List<LevelUpSO>();
    public List<LevelUpSO> shootGunLevelUpSOs = new List<LevelUpSO>();


    /// <summary>
    /// 随机获取指定数量的升级SO
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public LevelUpSO[] GetRandomLevelUpSOs(int count)
    {
        List<LevelUpSO> selectedSOs = new List<LevelUpSO>();
        List<LevelUpSO> copyList = new List<LevelUpSO>();

        // 判断当前武器类型，选择对应的升级SO列表
        if (WeaponManager.Instance.weapons[0] is SpinWeapon)
        {
            copyList = new List<LevelUpSO>(fireBallLevelUpSOs);
        }
        else if (WeaponManager.Instance.weapons[0] is GunWeapon)
        {
            copyList = new List<LevelUpSO>(shootGunLevelUpSOs);
        }

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
}
