using Mirror;

/// <summary>
/// 关卡状态（服务端 → 全体客户端）：阶段、时钟、波次。
///
/// <para>
/// <b>为什么走消息而不是 <c>SyncVar</c>：</b><c>StageDirector</c> 与 <c>GameLevelManager</c>
/// 都是**场景对象**（没有 <c>NetworkIdentity</c>）。给它们挂上 <c>NetworkIdentity</c> 会让
/// Mirror 把它们当成"场景对象"并在进 Play 时强制禁用，而**单机模式没有服务端**去激活它们 ——
/// 那会直接毁掉单机流程。与推车同一套理由，见 <c>CartStateMessage</c>。
/// </para>
/// </summary>
public struct StageStateMessage : NetworkMessage
{
    /// <summary><c>StagePhase</c> 的底层值（<c>Initializing/Travelling/WaitingAtNode/Victory/Defeat</c>）。</summary>
    public byte Phase;

    /// <summary>关卡已进行时间（秒）。</summary>
    public float LevelTime;

    /// <summary>当前波次。</summary>
    public int CurrentWave;
}

/// <summary>
/// 一局结束的冻结结果（服务端 → 全体客户端，一次性）。
///
/// <para>
/// <b>字段是摊平的，不直接塞 <see cref="RunResult"/>：</b>消息里出现自定义类型要靠 Weaver
/// 自动生成读写器，摊平成基元类型则完全可控；这条消息只在结算时发一次，可读性 > 省几个字段。
/// </para>
///
/// <para>
/// <b>客户端收到后各自写自己的档案</b>：金币与哨站进度是**每台机器一份**的
/// （<c>PlayerProfileService</c>），奖励数值以服务端下发的为准，客户端不重算 ——
/// 重算会让不同玩家的结算数值分叉（各自的击杀统计并不一样）。
/// </para>
/// </summary>
public struct RunResultMessage : NetworkMessage
{
    /// <summary><c>RunOutcome</c> 的底层值。</summary>
    public byte Outcome;

    public string StageId;
    public float ElapsedTime;
    public int TotalKills;
    public float CartHealthNormalized;
    public int RewardCoins;
}
