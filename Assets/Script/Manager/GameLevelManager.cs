using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡/游戏状态管理器
/// 负责全局游戏状态（时间、波次、暂停、敌人注册）。
/// 实现 IGameLevelManager 接口，支持通过 ServiceLocator 替换为联机实现。
///
/// <para>
/// <b>它不判胜负、也不弹结束面板</b>：那是 <see cref="StageDirector"/> 的独占职责。
/// 这里曾经有一条 <c>NotifyPlayerDied → GameOver → DeadPanel</c> 的旧路径，
/// 与新的结算链路并存 —— 单人时表现为「DeadPanel 与 RunResultPanel 同时弹出」，
/// 多人时第一个人阵亡就结束全队。结束入口只能有一个，所以整条路径已删除。
/// </para>
/// </summary>
[DefaultExecutionOrder(-120)]
public class GameLevelManager : ManagerSingleton<GameLevelManager>, IGameLevelManager
{
    [Header("Enemy管理")]
    private List<EnemyController> enemies = new List<EnemyController>();

    [Header("关卡信息统计")]
    [SerializeField] private float _levelTime = 0f;  // 记录关卡经过的时间
    [SerializeField] private bool _isGameActive = true;
    [SerializeField] private int _currentWave; // 当前波次

    // ---- IGameLevelManager 事件 ----
    public event System.Action<float> OnGameTimeUpdate;

    // ---- IGameLevelManager 属性 ----
    public float LevelTime => _levelTime;
    public int CurrentWave { get => _currentWave; set => _currentWave = value; }
    public bool IsGameActive => _isGameActive;

    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IGameLevelManager Service =>
        ServiceLocator.TryGet<IGameLevelManager>(out var svc) ? svc : null;

    protected override void OnSingletonAwake()
    {
        // 场景初始化时复位时间缩放：Time.timeScale 是全局状态、跨场景存活，
        // 若上个场景是在暂停状态下退出的，新关卡会整体静止（只有 unscaled 的 UI 还动）。
        // 这里给出一个「新场景默认不暂停」的兜底，不依赖上一局的暂停令牌被逐个释放。
        Time.timeScale = 1f;

        // 注册到 ServiceLocator，使 Service 属性能正确返回接口
        ServiceLocator.Register<IGameLevelManager>(this);
    }

    private async void Start()
    {
        // 全局服务（AssetService/音频/UIService）由 GameBootstrap 统一初始化，
        // 这里只等其就绪后展示关卡内面板。关键服务失败时不推进 ——
        // 继续跑会在这里拿到 null 服务，然后在某个更远的地方抛异常
        if (!await GameBootstrap.TryWaitReadyAsync()) return;

        IUIService ui = UIService.Service;
        if (ui == null)
        {
            Debug.LogError("[GameLevelManager] IUIService 未就绪，关卡内面板无法显示。");
            return;
        }

        // 先订阅再 await：订阅若排在 await 之后，切场景时 OnDestroy 的 -= 会先执行，
        // 委托就会残留在常驻的 UIService 上，之后在别的场景按 ESC 会回调到已销毁的关卡管理器。
        ui.OnEscapeUnhandled += ShowSettingPanel;
        _escapeSubscribed = true;

        // 武器由大厅的武器台决定，PlayerSpawner 已按 RunSession 装配完毕。
        // 这里曾经再弹一次 ChooseWeaponPanel 让玩家在关卡里选一把 —— 与出门配置冲突：
        // 槽位已被占满，面板上点了没反应（EquipAsync 的上限检查拒绝了它）。
        await ui.ShowPanelAsync<GamePanel>();
    }

    private bool _escapeSubscribed;

    /// <summary>计时 UI 的刷新间隔（秒）。时钟只需要秒级精度，没必要每帧刷。</summary>
    private const float TimeUiRefreshInterval = 0.25f;

    private float _timeUiTimer;

    void Update()
    {
        if (!_isGameActive) return;

        _levelTime += Time.deltaTime;

        // 节流：原实现每帧都刷一次 TMP 文本，会触发每帧一次文本网格重建 + 多次字符串分配。
        // 时钟显示只到秒，0.25s 刷新一次在观感上无差别，却把这块开销降到约 1/15。
        _timeUiTimer -= Time.deltaTime;
        if (_timeUiTimer > 0f) return;

        _timeUiTimer = TimeUiRefreshInterval;
        UpdateGameTimeUI();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        // 只在确实订阅过时才退订，避免服务先销毁时静默失败、或对未订阅的委托做无意义操作
        if (_escapeSubscribed)
        {
            IUIService ui = UIService.Service;
            if (ui != null)
                ui.OnEscapeUnhandled -= ShowSettingPanel;

            _escapeSubscribed = false;
        }

        ServiceLocator.UnregisterIfSelf<IGameLevelManager>(this);
    }


    /// <summary>
    /// 注册与注销敌人，获取敌人数量
    /// </summary>
    public void RegisterEnemy(EnemyController enemy)
    {
        enemies.Add(enemy);
    }
    public void UnregisterEnemy(EnemyController enemy)
    {
        enemies.Remove(enemy);
    }
    public int GetEnemyCount()
    {
        return enemies.Count;
    }


    /// <summary>
    /// 暂停持有者集合。
    ///
    /// <para>
    /// <b>暂停是有所有权的</b>：每个持有者拿自己的令牌申请一次，释放时也只释放自己那一次；
    /// 集合空了才真正恢复时间。旧实现是一个布尔量 + 全局 <c>Time.timeScale</c>，
    /// 任何面板都能无条件恢复 —— 两个模态面板重叠时，先关的那个会把时间恢复成 1，
    /// 另一个还显示着的面板就失去了暂停保护。
    /// </para>
    /// </summary>
    private readonly HashSet<object> _pauseHolders = new HashSet<object>();

    /// <summary>当前是否因持有者而暂停。</summary>
    public bool IsPaused => _pauseHolders.Count > 0;

    /// <summary>
    /// 申请暂停（幂等：同一令牌重复申请只算一次）。
    /// 面板不要直接调它 —— 走 <c>BasePanel</c> 的令牌管理，由 <c>UIService</c> 在显示/销毁时统一申请与释放。
    /// </summary>
    public void AcquirePause(object token)
    {
        if (token == null) return;
        if (!_pauseHolders.Add(token)) return;

        ApplyPauseState();
    }

    /// <summary>释放暂停。不是持有者时是空操作（不会误恢复别人持有的暂停）。</summary>
    public void ReleasePause(object token)
    {
        if (token == null) return;
        if (!_pauseHolders.Remove(token)) return;

        ApplyPauseState();
    }

    private void ApplyPauseState()
    {
        bool paused = _pauseHolders.Count > 0;

        _isGameActive = !paused;
        Time.timeScale = paused ? 0f : 1f;
    }

    public void UpdateGameTimeUI()
    {
        UIService.Service?.GetPanel<GamePanel>()?.UpdateTime(_levelTime);
        OnGameTimeUpdate?.Invoke(_levelTime);
    }

    private async void ShowSettingPanel()
    {
        if (!_isGameActive) return;

        IUIService ui = UIService.Service;
        if (ui == null) return;

        await ui.ShowPanelAsync<PausePanel>();
    }
}
