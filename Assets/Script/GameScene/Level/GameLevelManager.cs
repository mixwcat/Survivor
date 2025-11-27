using System.Collections.Generic;
using UnityEngine;

public class GameLevelManager : MonoBehaviour
{
    [Header("单例模式")]
    private static GameLevelManager instance;
    public static GameLevelManager Instance => instance;

    [Header("Enemy管理")]
    private List<EnemyController> enemies = new List<EnemyController>();

    [Header("关卡信息统计")]
    public float levelTime = 0f;  // 记录关卡经过的时间
    public bool isGameActive = true;
    public int currentWave; // 当前波次

    private void Awake()
    {
        instance = this;
        UIManager.Instance.ShowPanel<GamePanel>();
    }

    private void Start()
    {
        UIManager.Instance.ShowPanel<ChooseWeaponPanel>();
    }

    void Update()
    {
        if (isGameActive)
        {
            levelTime += Time.deltaTime;
            UpdateGameTimeUI();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isGameActive)
            {
                UIManager.Instance.ShowPanel<PausePanel>();
            }
        }
    }


    /// <summary>
    /// 注册与注销敌人，获取敌人数量
    /// </summary>
    /// <param name="enemy"></param>
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
    /// 时间暂停与恢复
    /// </summary>
    /// <returns></returns>
    public void PauseGame()
    {
        isGameActive = false;
        Time.timeScale = 0f;
    }
    public void ResumeGame()
    {
        isGameActive = true;
        Time.timeScale = 1f;
    }
    public void UpdateGameTimeUI()
    {
        UIManager.Instance.GetPanel<GamePanel>()?.UpdateTime(levelTime);
    }


    public void GameOver(object obj = null)
    {
        isGameActive = false;
        UIManager.Instance.ShowPanel<DeadPanel>().SetSurvivalTime((int)levelTime);
        UIManager.Instance.HidePanel<GamePanel>();
    }
    void OnEnable()
    {
        EventCenter.Subscribe(PlayerEnum.OnPlayerDead, GameOver);
    }
    void OnDisable()
    {
        EventCenter.Unsubscribe(PlayerEnum.OnPlayerDead, GameOver);
    }
}
