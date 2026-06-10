using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家经验/等级控制器
/// 负责管理单个玩家的等级、经验值与技能点。
/// 核心设计：状态变更与表现（UI/音效）分离，状态操作集中，表现通过事件订阅处理。
/// </summary>
public class ExperienceLevController : MonoBehaviour, IExperienceController
{
    private static ExperienceLevController _instance;
    public static ExperienceLevController Instance => _instance;

    /// <summary>
    /// 兼容层：优先从 ServiceLocator 获取 IExperienceController，回退到 Instance。
    /// </summary>
    public static IExperienceController Service
    {
        get
        {
            if (ServiceLocator.TryGet(out IExperienceController svc))
                return svc;
            return _instance;
        }
    }

    [Header("等级")]
    public int currentLevel;
    public int maxLevel;
    public List<int> expTable;  // 每个等级所需经验值
    public int currentExp;
    public int levelPoint;

    // ---- IExperienceController 事件 ----
    public event System.Action<int> OnLevelUp;
    public event System.Action<int> OnExpChanged;
    public event System.Action<int> OnPointsChanged;
    /// <summary>技能点不足时触发（由外部订阅处理提示表现）</summary>
    public event System.Action OnInsufficientPoints;

    // ---- IExperienceController 属性 ----
    public int CurrentLevel => currentLevel;
    public int CurrentExp => currentExp;
    public int AvailablePoints => levelPoint;
    public int ExpToNextLevel
    {
        get
        {
            if (expTable == null || currentLevel >= expTable.Count) return int.MaxValue;
            return expTable[currentLevel];
        }
    }

    private void Awake()
    {
        if (_instance == null)
            _instance = this;

        ServiceLocator.Register<IExperienceController>(this);

        // 订阅默认表现（音效、提示）。联机模式下可替换为网络同步表现。
        SubscribeDefaultPresentation();
    }

    private void OnDestroy()
    {
        UnsubscribeDefaultPresentation();
        ServiceLocator.Unregister<IExperienceController>();
        if (_instance == this)
            _instance = null;
    }

    private void Start()
    {
        FillExpTable();
        SyncUI();
    }


    #region 公共 API —— 纯状态操作

    /// <summary>增加经验值。可能触发多次升级。</summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        currentExp += amount;
        ProcessLevelUps();
        OnExpChanged?.Invoke(currentExp);
        SyncUI();
    }

    /// <summary>
    /// 尝试消耗指定数量的技能点。
    /// 返回 true 表示扣除成功；点数不足时返回 false 并触发 OnInsufficientPoints 事件。
    /// </summary>
    public bool CanUseLevelPoint(int amount)
    {
        if (amount <= 0) return true;
        if (levelPoint < amount)
        {
            OnInsufficientPoints?.Invoke();
            return false;
        }

        levelPoint -= amount;
        OnPointsChanged?.Invoke(levelPoint);
        SyncUI();
        return true;
    }

    /// <summary>增加技能点</summary>
    public void AddLevelPoint(int amount)
    {
        if (amount <= 0) return;

        levelPoint += amount;
        OnPointsChanged?.Invoke(levelPoint);
        SyncUI();
    }

    #endregion


    #region 私有核心逻辑

    /// <summary>
    /// 处理升级逻辑。支持一次获得大量经验时连续升级。
    /// </summary>
    private void ProcessLevelUps()
    {
        if (currentLevel >= maxLevel) return;

        while (currentLevel < maxLevel && currentExp >= expTable[currentLevel])
        {
            currentExp -= expTable[currentLevel];
            currentLevel++;
            levelPoint++;

            OnLevelUp?.Invoke(currentLevel);
            OnPointsChanged?.Invoke(levelPoint);
        }
    }

    /// <summary>补齐经验表到 maxLevel 长度</summary>
    private void FillExpTable()
    {
        if (expTable == null) expTable = new List<int>();
        while (expTable.Count < maxLevel)
        {
            int next = expTable.Count > 0 ? expTable[expTable.Count - 1] + 1 : 1;
            expTable.Add(next);
        }
    }

    /// <summary>同步状态到 UI 面板</summary>
    private void SyncUI()
    {
        var gamePanel = UIManager.Instance.GetPanel<GamePanel>();
        if (gamePanel == null) return;

        gamePanel.UpdateExp(currentExp, expTable[currentLevel], currentLevel);
        gamePanel.UpdateLevelPoint(levelPoint);
    }

    #endregion


    #region 默认表现订阅（可在外部替换）

    private void SubscribeDefaultPresentation()
    {
        OnLevelUp += HandleLevelUpSound;
        OnInsufficientPoints += HandleInsufficientPointsHint;
    }

    private void UnsubscribeDefaultPresentation()
    {
        OnLevelUp -= HandleLevelUpSound;
        OnInsufficientPoints -= HandleInsufficientPointsHint;
    }

    private void HandleLevelUpSound(int newLevel)
    {
        BKMusic.Instance?.PlaySound(ResourceEnum.PlayerLevelUP);
    }

    private void HandleInsufficientPointsHint()
    {
        UIManager.Instance?.ShowPanel<TipsPanel>();
    }

    #endregion
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
    public void ReturnToPool(ExpSpriteController expSprite)
    {
        expSprite.gameObject.SetActive(false);
        expSpritePool.Add(expSprite);
    }
}
