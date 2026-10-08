using Mirror;
using UnityEngine;

/// <summary>
/// 关卡状态的发送与接收（服务端广播阶段/时钟/波次/结算结果）。
///
/// <para>
/// 它和 <see cref="StageDirector"/> 是**同一个 GameObject 上的两个组件**，由
/// <c>StageDirector.Start</c> 在运行时 <c>AddComponent</c> 出来 —— 不走场景接线。
/// 理由是场景里的对象引用在批处理脚本下最容易出问题（见 CLAUDE.md 的批处理约定），
/// 而这里只需要一个"我知道自己是谁"的引用，运行时装配反而更可靠。
/// </para>
///
/// <para>
/// 单机（没有网络会话）时它什么都不做：<c>NetworkServer.active</c> 为 false，一次都不发。
/// </para>
/// </summary>
public class StageNetworkSync : MonoBehaviour
{
    /// <summary>当前场景的同步器。静态消息处理器需要一个入口转发到实例（切场景后实例会换）。</summary>
    public static StageNetworkSync Current { get; private set; }

    /// <summary>
    /// 广播频率。
    /// 2Hz 足够：阶段与波次是**事件性**的（变化时下一次广播就带上），
    /// 时钟只用于 HUD 显示（原本也是 0.25s 刷新一次）。
    /// </summary>
    private const float SendInterval = 0.5f;

    /// <summary>由 <see cref="StageDirector"/> 在运行时赋值。</summary>
    public StageDirector Director;

    private float _sendTimer;

    private void OnEnable()
    {
        if (Current != null && Current != this)
            Debug.LogWarning($"[{nameof(StageNetworkSync)}] 场景中存在多个同步器，以后注册的为准。");

        Current = this;
    }

    private void OnDisable()
    {
        if (Current == this) Current = null;
    }

    private void Update()
    {
        // 只有服务端广播；单机（没有会话）时 NetworkServer.active 为 false，直接不做事
        if (!NetworkServer.active || Director == null) return;

        _sendTimer -= Time.deltaTime;
        if (_sendTimer > 0f) return;

        _sendTimer = SendInterval;

        IGameLevelManager level = GameLevelManager.Service;

        NetworkServer.SendToAll(new StageStateMessage
        {
            Phase = (byte)Director.Phase,
            LevelTime = level != null ? level.LevelTime : 0f,
            CurrentWave = level != null ? level.CurrentWave : 0,
        });
    }

    /// <summary>
    /// 服务端广播本局结果（<c>StageDirector.FinishRun</c> 调用，一次）。
    /// 客户端收到后各自走一遍本机的结算（写档案 + 弹面板）。
    /// </summary>
    public void BroadcastResult(in RunResult result)
    {
        if (!NetworkServer.active) return;

        NetworkServer.SendToAll(new RunResultMessage
        {
            Outcome = (byte)result.Outcome,
            StageId = result.StageId,
            ElapsedTime = result.ElapsedTime,
            TotalKills = result.TotalKills,
            CartHealthNormalized = result.CartHealthNormalized,
            RewardCoins = result.RewardCoins,
        });
    }

    /// <summary>
    /// 注册客户端处理器。
    ///
    /// <para>
    /// ⚠️ 由 <c>SurvivorNetworkManager.OnStartClient</c> 调用，不能放在本组件的 <c>OnEnable</c> 里 ——
    /// 服务端在关卡加载完就开始广播，而客户端的场景对象要到场景加载完才出现，
    /// 晚注册会漏掉开头几条（表现是"进关卡后时钟从 0 开始走"）。
    /// </para>
    /// </summary>
    public static void RegisterClientHandlers()
    {
        NetworkClient.RegisterHandler<StageStateMessage>(OnStageStateReceived);
        NetworkClient.RegisterHandler<RunResultMessage>(OnRunResultReceived);
    }

    private static void OnStageStateReceived(StageStateMessage message)
    {
        StageNetworkSync sync = Current;
        if (sync == null || sync.Director == null) return;

        sync.Director.ApplyNetworkPhase((StagePhase)message.Phase);
        GameLevelManager.Service?.ApplyNetworkClock(message.LevelTime, message.CurrentWave);
    }

    private static void OnRunResultReceived(RunResultMessage message)
    {
        StageNetworkSync sync = Current;
        if (sync == null || sync.Director == null) return;

        sync.Director.ApplyNetworkResult(new RunResult(
            (RunOutcome)message.Outcome,
            message.StageId,
            message.ElapsedTime,
            message.TotalKills,
            message.CartHealthNormalized,
            message.RewardCoins));
    }
}
