using System.Collections.Generic;
using System.Threading.Tasks;
using Mirror;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 玩家经验/等级控制器 —— 只管**等级与经验**。
///
/// <para>
/// 升级点已经移到 <see cref="UpgradePointWallet"/>：这个类回答「练到几级了」，
/// 钱包回答「还能买几次升级」。两者由 <see cref="PlayerProgressionController"/> 串起来
/// （升级 → 发点 + 排队一次免费三选一）。
/// </para>
///
/// 核心设计：状态变更与表现（UI/音效）分离，状态操作集中，表现通过事件订阅处理。
/// </summary>
[DefaultExecutionOrder(-120)]
public class ExperienceLevController : MonoBehaviour, IExperienceController
{
    [Header("等级")]
    public int currentLevel;
    public int maxLevel;
    public List<int> expTable;  // 每个等级所需经验值
    public int currentExp;

    // ---- IExperienceController 事件 ----
    public event System.Action<int> OnLevelUp;
    public event System.Action<int> OnExpChanged;

    // ---- IExperienceController 属性 ----
    public int CurrentLevel => currentLevel;
    public int CurrentExp => currentExp;
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
        // 经验控制器按玩家实例化（挂在 Player 上），不注册为全局服务。
        // 订阅默认表现（音效）。联机模式下可替换为网络同步表现。
        SubscribeDefaultPresentation();
    }

    private void OnDestroy()
    {
        UnsubscribeDefaultPresentation();
    }

    private async void Start()
    {
        FillExpTable();
        await ExpSpritePool.Instance.InitializeAsync();
    }


    #region 公共 API —— 纯状态操作

    /// <summary>增加经验值。可能触发多次升级。</summary>
    public void AddExperience(int amount)
    {
        if (amount <= 0) return;

        // 联机时经验**只在服务端累加**，客户端的等级/经验来自同步
        //（见 NetworkPlayerState 的同名 SyncVar）。客户端也加的话两端会各自演化出一套等级，
        // 而"升级三选一"是按本地等级弹的 —— 最后变成两边选项数量都不一样
        if (NetworkBootstrap.IsActive && !NetworkServer.active) return;

        currentExp += amount;
        ProcessLevelUps();
        OnExpChanged?.Invoke(currentExp);
    }

    /// <summary>
    /// 客户端应用服务端同步过来的等级与经验。
    ///
    /// <para>
    /// <b>它必须照常发 <c>OnExpChanged</c> / <c>OnLevelUp</c></b>：
    /// HUD 经验条与"升级三选一"面板都是**事件驱动**的订阅者，
    /// 只改字段不发事件的表现是"等级涨了但面板不弹、经验条不动"。
    /// </para>
    ///
    /// <para>
    /// 与 <see cref="AddExperience"/> 的分工：那个是"结算"（会自己算升级），
    /// 这个是"应用权威结果"（等级由服务端算好了直接抄）。
    /// 两者不能互相调用 —— 客户端再算一遍就会与服务端分叉。
    /// </para>
    /// </summary>
    public void ApplyNetworkProgress(int level, int exp)
    {
        if (level < 0 || exp < 0) return;   // 未初始化（SyncVar 哨兵值）

        bool leveledUp = level > currentLevel;

        currentLevel = level;
        currentExp = exp;

        OnExpChanged?.Invoke(currentExp);

        // 升级才发 OnLevelUp —— 与 ProcessLevelUps 的行为保持一致
        if (leveledUp) OnLevelUp?.Invoke(currentLevel);
    }

    #endregion


    #region 私有核心逻辑

    /// <summary>
    /// 处理升级逻辑。支持一次获得大量经验时连续升级。
    /// 每级只发 <see cref="OnLevelUp"/> —— 发点与排队三选一是订阅者
    /// （<see cref="PlayerProgressionController"/>）的职责。
    /// </summary>
    private void ProcessLevelUps()
    {
        if (currentLevel >= maxLevel) return;

        while (currentLevel < maxLevel && currentExp >= expTable[currentLevel])
        {
            currentExp -= expTable[currentLevel];
            currentLevel++;

            OnLevelUp?.Invoke(currentLevel);
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

    #endregion


    #region 默认表现订阅（可在外部替换）

    private void SubscribeDefaultPresentation()
    {
        OnLevelUp += HandleLevelUpSound;
    }

    private void UnsubscribeDefaultPresentation()
    {
        OnLevelUp -= HandleLevelUpSound;
    }

    private void HandleLevelUpSound(int newLevel)
    {
        AudioService.Service?.PlaySfx(ResourceEnum.PlayerLevelUP);
    }

    #endregion
}


class ExpSpritePool
{
    /// <summary>
    /// 池保留上限。依据：经验球只在「敌人死亡」时产生，且会在 20s 内被拾取或超时回收，
    /// 同时空闲的数量远小于敌人峰值；100 足以覆盖「一波清空」的复用需求，
    /// 又不会让空闲经验球无限囤积（每个都是带 SpriteRenderer + Collider2D 的 GameObject）。
    /// </summary>
    private const int MaxRetained = 100;

    private static ExpSpritePool instance = new ExpSpritePool();
    public static ExpSpritePool Instance => instance;

    private ObjectPool<ExpSpriteController> _pool;

    private IAssetService _assetService;
    private GameObject _expSpritePrefab;
    private AsyncOperationHandle<GameObject> _expSpritePrefabHandle;
    private Task _initTask;
    private bool _warnedNotReady;

    /// <summary>
    /// 重置静态池状态。由 <see cref="GameBootstrap.ResetStatics"/> 在每次进入 Play 前调用：
    /// 编辑器关闭了 Domain Reload，本静态实例会跨 Play 会话存活，
    /// 而 <c>_assetService</c> / 句柄 / prefab 引用都已随上一会话失效。
    /// </summary>
    public static void Reset()
    {
        instance = new ExpSpritePool();
    }

    /// <summary>
    /// 加载经验精灵 prefab（幂等，加载完成后常驻）。
    /// 注意：本类是静态单例，编辑器关闭 Domain Reload 时会跨 Play 会话存活、缓存的 prefab 随之失效，
    /// 因此判据用「prefab 是否仍有效」而不是「初始化任务是否已创建」。
    /// </summary>
    public Task InitializeAsync()
    {
        if (_expSpritePrefab != null)
            return Task.CompletedTask;

        if (_initTask != null && !_initTask.IsCompleted)
            return _initTask;

        _initTask = InitializeInternalAsync();
        return _initTask;
    }

    private async Task InitializeInternalAsync()
    {
        _assetService = AssetService.Service;
        if (_assetService == null)
        {
            Debug.LogError("[ExpSpritePool] IAssetService 未注册，经验精灵无法加载。");
            return;
        }

        _expSpritePrefabHandle = _assetService.LoadAssetAsync<GameObject>(AssetKeys.ExpSprite);
        _expSpritePrefab = await _expSpritePrefabHandle.Task;
    }


    /// <summary>
    /// 在敌人死亡位置掉落一个经验精灵。
    /// </summary>
    /// <param name="expAmount">
    /// 本次掉落的经验值，由敌人的 <c>StatType.ExpReward</c> 决定（<c>EnemyDataSO.ExpReward</c> 可配）。
    /// 缺配时 <c>EntityBehaviour.GetStat</c> 会返回 1 并只告警一次，行为与旧版硬编码 1 一致。
    /// </param>
    public void SpawnExpSprite(Transform enemyTransform, int expAmount)
    {
        ExpSpriteController expSprite = GetFromPool(enemyTransform.position);
        if (expSprite == null) return;

        expSprite.transform.position = enemyTransform.position;
        // 取池成功后才设置：SetExpAmount 必须与本次掉落一一对应，
        // 否则复用的实例会带着上一个敌人的经验值出场
        expSprite.SetExpAmount(expAmount);
    }


    /// <summary>
    /// 从池中获取一个经验精灵对象，没有则创建。
    /// 幂等归还、保留上限、跳过被场景销毁的条目都由 <see cref="ObjectPool{T}"/> 统一处理。
    /// </summary>
    public ExpSpriteController GetFromPool(Vector3 position)
    {
        if (_pool == null)
        {
            if (_expSpritePrefab == null)
            {
                // 这是「每个敌人死亡」都会走的路径：未就绪时只提示一次，
                // 否则会按敌人数量刷 LogError（并静默丢掉经验）
                if (!_warnedNotReady)
                {
                    _warnedNotReady = true;
                    Debug.LogWarning("ExpSpritePool: ExpSprite prefab 尚未加载完成，经验掉落被跳过（只提示一次）。");
                }
                return null;
            }

            _pool = new ObjectPool<ExpSpriteController>(CreateInstance, MaxRetained, "ExpSpritePool");
        }

        return _pool.Get();
    }

    private ExpSpriteController CreateInstance()
    {
        return Object.Instantiate(_expSpritePrefab).GetComponent<ExpSpriteController>();
    }


    /// <summary>归还经验精灵到池中（幂等）。</summary>
    public void ReturnToPool(ExpSpriteController expSprite)
    {
        _pool?.Release(expSprite);
    }
}
