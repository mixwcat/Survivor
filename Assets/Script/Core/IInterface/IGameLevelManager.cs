using System.Collections.Generic;

/// <summary>
/// 关卡/游戏状态管理接口
/// 抽象全局游戏状态（时间、波次、暂停、敌人生存周期），
/// 为单机模式和联机模式（权威服务器/客户端预测）提供统一访问点。
///
/// <para>
/// <b>这里没有「结束游戏」</b>：胜负由 <see cref="StageDirector"/> 独占判定并触发结算。
/// 接口上再留一个 <c>GameOver</c> 就会形成第二个结束入口 ——
/// 曾经的表现是死亡面板与结算面板同时弹出，且多人局里第一个人阵亡就结束全队。
/// </para>
/// </summary>
public interface IGameLevelManager
{
    /// <summary>关卡累计时间（秒），由权威端维护</summary>
    float LevelTime { get; }

    /// <summary>当前波次，由权威端维护；EnemySpawner 等逻辑可设置</summary>
    int CurrentWave { get; set; }

    /// <summary>游戏是否处于活跃状态（未暂停）</summary>
    bool IsGameActive { get; }

    /// <summary>游戏时间更新事件（参数：当前时间）</summary>
    event System.Action<float> OnGameTimeUpdate;

    /// <summary>注册敌人到全局追踪列表</summary>
    void RegisterEnemy(EnemyController enemy);

    /// <summary>从全局追踪列表注销敌人</summary>
    void UnregisterEnemy(EnemyController enemy);

    /// <summary>获取当前存活敌人数量</summary>
    int GetEnemyCount();

    /// <summary>
    /// 客户端应用服务端广播的时钟与波次（见 <c>StageNetworkSync</c>）。
    /// 服务端调用它是空操作 —— 判据是"本进程是不是服务端"，实现里会挡掉。
    /// </summary>
    void ApplyNetworkClock(float levelTime, int currentWave);

    /// <summary>
    /// 申请暂停。<paramref name="token"/> 是**持有者身份**（面板 / 协调者自己造的令牌对象）。
    ///
    /// <para>
    /// <b>暂停是有所有权的</b>：谁申请谁释放，<see cref="ReleasePause"/> 只释放自己那一次，
    /// 最后一个持有者释放时才把 <c>Time.timeScale</c> 恢复为 1。
    /// </para>
    /// <para>
    /// 旧接口是无参的 <c>PauseGame</c> / <c>ResumeGame</c>，任何面板都能无条件恢复全局时间 ——
    /// 两个模态面板（升级三选一 + 塔管理）因异步加载而重叠时，先关的那个会把时间恢复成 1，
    /// 另一个还显示着的面板就失去了暂停保护（反向时序则会留下 <c>timeScale = 0</c>）。
    /// **不要**再退回布尔式暂停。
    /// </para>
    /// </summary>
    void AcquirePause(object token);

    /// <summary>释放暂停。只有当初申请的那个令牌有效；重复释放是空操作。</summary>
    void ReleasePause(object token);
}
