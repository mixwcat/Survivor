using System.Collections.Generic;

/// <summary>
/// 关卡/游戏状态管理接口
/// 抽象全局游戏状态（时间、波次、暂停、敌人生存周期、游戏结束），
/// 为单机模式和联机模式（权威服务器/客户端预测）提供统一访问点。
/// </summary>
public interface IGameLevelManager
{
    /// <summary>关卡累计时间（秒），由权威端维护</summary>
    float LevelTime { get; }

    /// <summary>当前波次，由权威端维护；EnemySpawner 等逻辑可设置</summary>
    int CurrentWave { get; set; }

    /// <summary>游戏是否处于活跃状态（未暂停、未结束）</summary>
    bool IsGameActive { get; }

    /// <summary>游戏是否已结束</summary>
    bool IsGameOver { get; }

    /// <summary>游戏结束事件（参数：存活时间）</summary>
    event System.Action<float> OnGameOver;

    /// <summary>游戏时间更新事件（参数：当前时间）</summary>
    event System.Action<float> OnGameTimeUpdate;

    /// <summary>注册敌人到全局追踪列表</summary>
    void RegisterEnemy(EnemyController enemy);

    /// <summary>从全局追踪列表注销敌人</summary>
    void UnregisterEnemy(EnemyController enemy);

    /// <summary>获取当前存活敌人数量</summary>
    int GetEnemyCount();

    /// <summary>
    /// 暂停游戏。单机模式下立即生效；联机模式下可能为空实现或发送 RPC 协商。
    /// </summary>
    void PauseGame();

    /// <summary>
    /// 恢复游戏。单机模式下立即生效；联机模式下可能为空实现或发送 RPC 协商。
    /// </summary>
    void ResumeGame();

    /// <summary>
    /// 触发游戏结束。单机模式下直接执行；联机模式下由服务器权威决定。
    /// </summary>
    /// <param name="param">可选参数，如死亡原因、击杀者等</param>
    void GameOver(object param = null);
}
