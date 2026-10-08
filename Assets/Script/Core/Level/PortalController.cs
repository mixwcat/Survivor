using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 传送门 —— 全员进入范围并停留 1 秒后开始 3 秒倒计时，倒计时结束出发。
///
/// <para>
/// <b>ready 判定遍历 <c>AllPlayers</c>，不是只看本地玩家</b>：
/// 单机下两者等价，但联机时"本地玩家进圈"不代表队友也准备好了。
/// 现在就按多玩家写，联机时不用回来改判定 —— 而改判定往往会漏掉某条路径。
/// </para>
///
/// <para>
/// <b>任何玩家离开都要取消并复位</b>：倒计时期间有人跑开，说明还没准备好；
/// 继续倒数会让队伍在人不齐的情况下出发（且出发后无法回头）。
/// </para>
///
/// <para>
/// <b>它有一台显式状态机</b>（<see cref="PortalState"/>），而不是几个布尔量：
/// 倒计时归零后到场景真正切换之间还有若干帧（<c>LoadSceneAsync</c> 是异步的），
/// 旧实现没有"已出发"这个状态，于是每帧都会再调一次 <c>LoadStage()</c>；
/// 而"锁 session"发生在 <c>Depart</c> 里（太晚）——倒计时期间玩家仍能改装备。
/// </para>
///
/// <para>
/// <b>出发判据只有一份</b>：<see cref="IRunSessionService.IsReadyToDepart"/>。
/// 这里不再自己检查角色/关卡/装备 —— 三处各写一份检查，漏掉一处就是"空装备也能出发"。
/// </para>
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PortalController : MonoBehaviour
{
    /// <summary>传送门状态。显式枚举 + 单一迁移入口，不用多个布尔量拼状态。</summary>
    private enum PortalState
    {
        /// <summary>空闲：等待玩家进圈 / 停留计时。</summary>
        Idle = 0,

        /// <summary>倒计时中：session 已锁定，玩家离开则取消并解锁。</summary>
        CountingDown = 1,

        /// <summary>已出发：**终态**，不再处理任何输入、倒计时或取消。</summary>
        Departing = 2,
    }

    [Header("触发条件")]
    [Tooltip("全员进圈后需要停留的秒数")]
    [SerializeField] private float _staySeconds = 1f;

    [Tooltip("停留达成后的倒计时秒数")]
    [SerializeField] private float _countdownSeconds = 3f;

    [Tooltip("没有选关卡时使用的默认关卡 id")]
    [SerializeField] private string _defaultStageId = "stage_01";

    private readonly HashSet<PlayerController> _inside = new HashSet<PlayerController>();

    private PortalState _state = PortalState.Idle;
    private float _stayTimer;
    private float _countdown;

    /// <summary>上次打过的"还不能出发"原因，用于告警去重（否则会逐帧刷日志）。</summary>
    private string _lastBlockReason;

    /// <summary>是否正在倒计时（UI 可订阅它显示提示）。</summary>
    public bool IsCountingDown => _state == PortalState.CountingDown;

    /// <summary>是否已经出发（终态）。</summary>
    public bool IsDeparting => _state == PortalState.Departing;

    /// <summary>
    /// 倒计时剩余秒数（不在倒计时时为 0）。供 UI 显示 —— 只读快照，不暴露状态机本身。
    /// </summary>
    public float CountdownRemaining => _state == PortalState.CountingDown ? _countdown : 0f;

    /// <summary>
    /// 是否所有玩家都在圈内。UI 据此显示"等待所有玩家"。
    /// 玩家还没生成时返回 false（那也算"人不齐"）。
    /// </summary>
    public bool AreAllPlayersInside => AllPlayersInside();

    private void Reset()
    {
        // 挂上组件时自动配好触发器，避免"忘了勾 isTrigger"导致玩家被挡住
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_state == PortalState.Departing) return;

        PlayerController player = ResolvePlayer(other);
        if (player != null) _inside.Add(player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 出发是终态：场景切换途中收到的 exit 不该再走取消路径（那时已无意义）
        if (_state == PortalState.Departing) return;

        PlayerController player = ResolvePlayer(other);
        if (player == null) return;

        _inside.Remove(player);

        // 有人离开就取消倒计时（不是暂停 —— 回来要重新站满停留时间）
        CancelCountdown();
    }

    /// <summary>传送门触发区在 InteractZone 层，只与玩家本体配对 —— 所以不必再筛类型。</summary>
    private static PlayerController ResolvePlayer(Collider2D other)
    {
        return other != null ? other.GetComponentInParent<PlayerController>() : null;
    }

    private void Update()
    {
        // 已出发：立刻停止一切处理。少了这一条，倒计时归零后会逐帧重复发起场景加载。
        if (_state == PortalState.Departing) return;

        if (_state == PortalState.CountingDown)
        {
            TickCountdown();
            return;
        }

        TickStay();
    }

    private void TickStay()
    {
        if (!AllPlayersInside())
        {
            _stayTimer = 0f;
            return;
        }

        _stayTimer += Time.deltaTime;
        if (_stayTimer < _staySeconds) return;

        TryBeginCountdown();
    }

    /// <summary>
    /// 停留达成 → 校验配置 → **先锁定**再开始倒计时。
    ///
    /// <para>
    /// 锁定必须在倒计时**开始**时发生：旧实现锁在 <c>Depart</c> 里，
    /// 于是整个倒计时期间玩家还能换角色换装备，而关卡装配读的是出发那一刻的值 ——
    /// 界面显示与关卡里实际拿到的武器不一致，且只在进关卡后才暴露。
    /// </para>
    /// </summary>
    private void TryBeginCountdown()
    {
        _stayTimer = 0f;

        IRunSessionService session = RunSessionService.Service;
        if (session == null)
        {
            LogBlockedOnce("[PortalController] IRunSessionService 未注册，无法出发。");
            return;
        }

        // 关卡 id 没人填时补默认值。走受控写入：它会检查锁状态，
        // 也保证"补默认关卡"不会绕过 session 的合法性边界。
        if (string.IsNullOrEmpty(session.Current.StageId) &&
            !session.TrySetStage(_defaultStageId, out string stageReason))
        {
            LogBlockedOnce($"[PortalController] 无法设置默认关卡「{_defaultStageId}」：{stageReason}");
            return;
        }

        // 统一的出发校验：角色 / 关卡 / 装备一次判完，失败原因直接可读
        if (!session.IsReadyToDepart(out string reason))
        {
            LogBlockedOnce($"[PortalController] 还不能出发：{reason}");
            return;
        }

        if (!session.TryLockForDeparture(out string lockReason))
        {
            LogBlockedOnce($"[PortalController] 出行配置无法锁定：{lockReason}");
            return;
        }

        _lastBlockReason = null;
        _state = PortalState.CountingDown;
        _countdown = _countdownSeconds;
    }

    private void TickCountdown()
    {
        // 倒计时期间持续复核：有人离开就取消（OnTriggerExit2D 也会调，双保险）
        if (!AllPlayersInside())
        {
            CancelCountdown();
            return;
        }

        _countdown -= Time.deltaTime;
        if (_countdown > 0f) return;

        Depart();
    }

    /// <summary>
    /// 取消倒计时并解锁 session，让玩家能回到大厅重新配置。
    /// 只在倒计时中生效 —— 空闲或已出发时都是空操作（重复取消不该产生副作用）。
    /// </summary>
    private void CancelCountdown()
    {
        if (_state != PortalState.CountingDown) return;

        _state = PortalState.Idle;
        _countdown = 0f;
        _stayTimer = 0f;

        RunSessionService.Service?.Unlock();
    }

    private bool AllPlayersInside()
    {
        IPlayerManager players = PlayerManager.Service;
        if (players == null) return false;

        IReadOnlyList<PlayerController> all = players.AllPlayers;
        if (all.Count == 0) return false;   // 玩家还没生成：不能出发

        for (int i = 0; i < all.Count; i++)
        {
            PlayerController player = all[i];
            if (player == null || !_inside.Contains(player)) return false;
        }

        return true;
    }

    /// <summary>
    /// 出发。**先落到 <see cref="PortalState.Departing"/> 再发起场景加载** ——
    /// 顺序反过来的话，加载完成前的那几帧 <c>Update</c> 仍会走到这里，
    /// 于是同一次进圈产生多次 <c>LoadStage()</c>。
    /// </summary>
    private void Depart()
    {
        if (_state == PortalState.Departing) return;

        _state = PortalState.Departing;
        _countdown = 0f;

        SceneFlow.LoadStage();
    }

    /// <summary>
    /// 同一个原因只提示一次：玩家可能带着空装备站在圈里，
    /// 逐帧一条 <c>LogWarning</c>（还带字符串插值）会变成新的性能问题，
    /// 而且真正有用的第一条会被淹没。
    /// </summary>
    private void LogBlockedOnce(string message)
    {
        if (_lastBlockReason == message) return;

        _lastBlockReason = message;
        Debug.LogWarning(message);
    }
}
