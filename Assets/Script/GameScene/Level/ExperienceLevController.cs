using System.Collections.Generic;
using UnityEngine;

public class ExperienceLevController : MonoBehaviour
{
    private static ExperienceLevController _instance;
    public static ExperienceLevController Instance => _instance;

    [Header("等级")]
    public int currentLevel;
    public int maxLevel;
    public List<int> expTable;  // 每个等级所需经验值
    public int currentExp;
    public int levelPoint;

    private void Awake()
    {
        if (_instance == null)
            _instance = this;
    }

    private void Start()
    {
        // 初始化UI
        UIManager.Instance.GetPanel<GamePanel>().UpdateLevelPoint(levelPoint);
        // 初始化经验表
        while (expTable.Count < maxLevel)
        {
            expTable.Add(expTable[expTable.Count - 1] + 1);
        }
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
            // 等级增加
            currentExp -= expTable[currentLevel];
            currentLevel++;
            // 增加等级点数
            levelPoint++;
            // 升级音效
            BKMusic.Instance.PlaySound(ResourceEnum.PlayerLevelUP);

            // 更新GamePanel等级点数
            UIManager.Instance.GetPanel<GamePanel>().UpdateLevelPoint(levelPoint);
        }
    }


    /// <summary>
    /// 减少等级点数
    /// </summary>
    /// <param name="amount"></param>
    public bool CanUseLevelPoint(int amount)
    {
        if (levelPoint < amount)
        {
            UIManager.Instance.ShowPanel<TipsPanel>();
            return false;
        }

        levelPoint -= amount;
        // 更新GamePanel等级点数
        UIManager.Instance.GetPanel<GamePanel>().UpdateLevelPoint(levelPoint);

        return true;
    }


    /// <summary>
    /// 增加等级点数
    /// </summary>
    /// <param name="amount"></param>
    public void AddLevelPoint(int amount)
    {
        levelPoint += amount;
        // 更新GamePanel等级点数
        UIManager.Instance.GetPanel<GamePanel>().UpdateLevelPoint(levelPoint);
    }
}

class ExpSpritePool
{
    private static ExpSpritePool instance = new ExpSpritePool();
    public static ExpSpritePool Instance => instance;
    private List<ExpSpriteController> expSpritePool = new List<ExpSpriteController>();
    private ExpSpriteController expSpriteToSpawn;


    public void SpawnExpSprite(Transform enemyTransform)
    {
        ExpSpriteController expSprite = GetFromPool(enemyTransform.position);
        expSprite.transform.position = enemyTransform.position;
    }


    /// <summary>
    /// 从池中获取一个经验精灵对象，没有则创建
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public ExpSpriteController GetFromPool(Vector3 position)
    {
        expSpriteToSpawn = null;

        if (expSpritePool.Count == 0)
        {
            // 池中没有，创建一个新的
            ExpSpriteController expSpriteObj = Object.Instantiate(Resources.Load<GameObject>("Prefabs/ExpSprite")).GetComponent<ExpSpriteController>();
            // 赋值
            expSpriteToSpawn = expSpriteObj;
        }
        else
        {
            if (expSpritePool[0] == null)
            {
                expSpritePool.RemoveAt(0);
                return GetFromPool(position);
            }
            // 从池中取出一个
            expSpriteToSpawn = expSpritePool[0];
            expSpritePool.RemoveAt(0);
            expSpriteToSpawn.gameObject.SetActive(true);
        }
        return expSpriteToSpawn;
    }


    /// <summary>
    /// 归还经验精灵对象到池中
    /// </summary>
    /// <param name="expSprite"></param>
    public void ReturnToPool(ExpSpriteController expSprite)
    {
        expSprite.gameObject.SetActive(false);
        expSpritePool.Add(expSprite);
    }
}