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
    public List<LevelUpSO> levelUpSOs = new List<LevelUpSO>();


    /// <summary>
    /// 随机获取指定数量的升级SO
    /// </summary>
    /// <param name="count"></param>
    /// <returns></returns>
    public LevelUpSO[] GetRandomLevelUpSOs(int count)
    {
        List<LevelUpSO> selectedSOs = new List<LevelUpSO>();
        List<LevelUpSO> copyList = new List<LevelUpSO>(levelUpSOs);
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
