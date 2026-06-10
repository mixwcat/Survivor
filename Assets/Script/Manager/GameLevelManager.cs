using System.Collections.Generic;
using System.ComponentModel.Design;
using UnityEngine;

/// <summary>
/// 关卡/游戏状态管理器
/// 负责全局游戏状态（时间、波次、暂停、敌人注册、游戏结束）。
/// 实现 IGameLevelManager 接口，支持通过 ServiceLocator 替换为联机实现。
/// </summary>
public class GameLevelManager : MonoBehaviour, IGameLevelManager
{
    [Header("单例模式")]
    private static GameLevelManager instance;
    public static GameLevelManager Instance => instance;

    /// <summary>
    /// 兼容层：优先从 ServiceLocator 获取 IGameLevelManager，回退到 Instance。
    /// 重构期间所有调用方统一改用此属性，便于后续无痛切换为联机实现。
    /// </summary>
    public static IGameLevelManager Service
    {
        get
        {
            if (ServiceLocator.TryGet(out IGameLevelManager svc))
                return svc;
            return instance;
        }
    }

    [Header("Enemy管理")]
    private List<EnemyController> enemies = new List<EnemyController>();

    [Header("关卡信息统计")]
    [SerializeField] private float _levelTime = 0f;  // 记录关卡经过的时间
    [SerializeField] private bool _isGameActive = true;
    [SerializeField] private int _currentWave; // 当前波次
    [SerializeField] private bool _isGameOver;

    [Header("输入管理")]
    private IInputHandle _inputHandle;

    // ---- IGameLevelManager 事件 ----
    public event System.Action<float> OnGameOver;
    public event System.Action<float> OnGameTimeUpdate;

    // ---- IGameLevelManager 属性 ----
    public float LevelTime => _levelTime;
    public int CurrentWave { get => _currentWave; set => _currentWave = value; }
    public bool IsGameActive => _isGameActive;
    public bool IsGameOver => _isGameOver;

    private void Awake()
    {
        instance = this;
        _isGameOver = false;

        UIManager.Instance.ShowPanel<GamePanel>();
        _inputHandle = InputHandleFactory.GetInput("local");

        if (_inputHandle == null)
        {
            Debug.LogError("GameLevelManager: Failed to create IInputHandle!");
        }

        // 注册到 ServiceLocator，使 Service 属性能正确返回接口
        ServiceLocator.Register<IGameLevelManager>(this);
    }

    private void Start()
    {
        UIManager.Instance.ShowPanel<ChooseWeaponPanel>();
    }

    void Update()
    {
        if (_isGameActive)
        {
            _levelTime += Time.deltaTime;
            UpdateGameTimeUI();
        }
    }

    private void OnDestroy()
    {
        ServiceLocator.Unregister<IGameLevelManager>();
        if (instance == this)
            instance = null;
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
        UIManager.Instance.GetPanel<GamePanel>()?.UpdateTime(_levelTime);
        OnGameTimeUpdate?.Invoke(_levelTime);
    }
    private void ShowSettingPanel()
    {
        if (_isGameActive)
        {
            UIManager.Instance.ShowPanel<PausePanel>();
        }
    }


    public void GameOver(object param = null)
    {
        _isGameActive = false;
        _isGameOver = true;
        OnGameOver?.Invoke(_levelTime);

        UIManager.Instance.ShowPanel<DeadPanel>().SetSurvivalTime((int)_levelTime);
        UIManager.Instance.HidePanel<GamePanel>();
    }

    void OnEnable()
    {
        EventCenter.Subscribe(PlayerEnum.OnPlayerDead, OnPlayerDeadEvent);

        if (_inputHandle != null)
        {
            _inputHandle.OnEscape += ShowSettingPanel;
        }
    }
    void OnDisable()
    {
        EventCenter.Unsubscribe(PlayerEnum.OnPlayerDead, OnPlayerDeadEvent);

        if (_inputHandle != null)
        {
            _inputHandle.OnEscape -= ShowSettingPanel;
        }
    }

    /// <summary>
    /// 玩家死亡事件回调，转发到 GameOver
    /// </summary>
    private void OnPlayerDeadEvent(object param)
    {
        GameOver(param);
    }
}
