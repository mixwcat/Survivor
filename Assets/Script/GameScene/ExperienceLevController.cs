using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.PlayerLoop;

public class ExperienceLevController : MonoBehaviour
{
    private static ExperienceLevController _instance;
    public static ExperienceLevController Instance => _instance;

    [Header("Level")]
    public int currentLevel;
    public int maxLevel;
    public List<int> expTable;  // 每个等级所需经验值
    public int currentExp;

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
    }

    private void Start()
    {
        // 初始化经验表
        while (expTable.Count < maxLevel)
        {
            expTable.Add(expTable[expTable.Count - 1] + 2);
        }
    }


    /// <summary>
    /// 生成经验精灵
    /// </summary>
    /// <param name="enemyTransform"></param>
    public void SpawnExpSprite(Transform enemyTransform)
    {
        Instantiate(Resources.Load<GameObject>("Prefabs/ExpSprite"), enemyTransform.position, Quaternion.identity);
    }


    /// <summary>
    /// 增加经验值
    /// </summary>
    /// <param name="amount"></param>
    public void AddExperience(int amount)
    {
        currentExp += amount;
        UpdateLevel();
        UIManager.Instance.GetPanel<GamePanel>().UpdateExp(currentExp, expTable[currentLevel], currentLevel);
    }


    /// <summary>
    /// 更新等级
    /// </summary>
    void UpdateLevel()
    {
        if (currentLevel >= maxLevel) return;

        if (currentExp >= expTable[currentLevel])
        {
            currentExp -= expTable[currentLevel];
            currentLevel++;
        }
    }
}
