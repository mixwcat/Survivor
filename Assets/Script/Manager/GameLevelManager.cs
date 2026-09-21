using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡/游戏状态管理器
/// 负责全局游戏状态（时间、波次、暂停、敌人注册、游戏结束）。
/// 实现 IGameLevelManager 接口，支持通过 ServiceLocator 替换为联机实现。
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
    [SerializeField] private bool _isGameOver;

    // ---- IGameLevelManager 事件 ----
    public event System.Action<float> OnGameOver;
    public event System.Action<float> OnGameTimeUpdate;

    // ---- IGameLevelManager 属性 ----
    public float LevelTime => _levelTime;
    public int CurrentWave { get => _currentWave; set => _currentWave = value; }
    public bool IsGameActive => _isGameActive;
    public bool IsGameOver => _isGameOver;

    /// <summary>服务访问入口（未注册时返回 null）。</summary>
    public static IGameLevelManager Service =>
        ServiceLocator.TryGet<IGameLevelManager>(out var svc) ? svc : null;

    protected override void OnSingletonAwake()
    {
        _isGameOver = false;

        // 注册到 ServiceLocator，使 Service 属性能正确返回接口
        ServiceLocator.Register<IGameLevelManager>(this);
    }

    private async void Start()
    {
        // 全局服务（AssetService/UIManager/音频）由 GameBootstrap 统一初始化，
        // 这里只等其就绪后展示关卡内面板。
        await GameBootstrap.Ready;
        UIManager.Service.ShowPanel<GamePanel>();
        UIManager.Service.ShowPanel<ChooseWeaponPanel>();

        // ESC 未被任何面板消费时，打开暂停面板
        if (UIManager.Service != null)
            UIManager.Service.OnEscapeUnhandled += ShowSettingPanel;
    }

    void Update()
    {
        if (_isGameActive)
        {
            _levelTime += Time.deltaTime;
            UpdateGameTimeUI();
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (UIManager.Service != null)
            UIManager.Service.OnEscapeUnhandled -= ShowSettingPanel;

        ServiceLocator.Unregister<IGameLevelManager>();
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
    /// 时间暂停与恢复，游戏暂停
    /// </summary>
    public void PauseGame()
    {
        _isGameActive = false;
        Time.timeScale = 0f;
    }
    public void ResumeGame()
    {
        _isGameActive = true;
        Time.timeScale = 1f;
    }
    public void UpdateGameTimeUI()
    {
        UIManager.Service.GetPanel<GamePanel>()?.UpdateTime(_levelTime);
        OnGameTimeUpdate?.Invoke(_levelTime);
    }
    private void ShowSettingPanel()
    {
        if (_isGameActive)
        {
            UIManager.Service.ShowPanel<PausePanel>();
        }
    }


    public void GameOver(object param = null)
    {
        _isGameActive = false;
        _isGameOver = true;
        OnGameOver?.Invoke(_levelTime);

        UIManager.Service.ShowPanel<DeadPanel>().SetSurvivalTime((int)_levelTime);
        UIManager.Service.HidePanel<GamePanel>();
    }

    /// <summary>
    /// 玩家死亡。单机：直接结束游戏；联机：由权威端决定（客户端应发送请求）。
    /// </summary>
    public void NotifyPlayerDied(PlayerController player)
    {
        GameOver(player);
    }
}
