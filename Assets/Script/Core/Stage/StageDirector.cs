using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡阶段。显式枚举 + 单一迁移入口，不用多个布尔值拼状态。
///
/// <para>
/// <b>2026-10 简化</b>：以前有 <c>TravellingToCharge1 / Charge1Boss / TravellingToCharge2 / …</c>
/// 六个行进阶段，因为"走到哪个充能点、Boss 打完没有"要由阶段自己记。
/// 现在这些信息归**生成表**（节点规则 + <c>EnemySpawner.NodeCleared</c>），
/// 阶段只需要回答"在走还是在等"。阶段没有被序列化到任何资产上，所以改编号是安全的。
/// </para>
/// </summary>
public enum StagePhase
{
    Initializing = 0,

    /// <summary>行驶中（段规则生效）。</summary>
    Travelling = 1,

    /// <summary>停在充能点等清场（节点规则生效；清完由生成器报告）。</summary>
    WaitingAtNode = 2,

    Victory = 3,
    Defeat = 4,
}

/// <summary>
/// 关卡导演 —— 阶段推进与胜负的**唯一权威**。
///
/// <para>
/// <b>为什么不能继续堆在 <c>GameLevelManager</c> 里：</b>它已经在管计时、暂停、敌人注册与失败 UI；
/// 再塞进"行驶 / 停车 / Boss / 恢复 / 胜利"会让任何一处改动都可能影响其它状态，
/// 而且联机时"谁有权结束这一局"会变得说不清。这里只保留阶段与胜负，
/// <c>GameLevelManager</c> 缩回通用运行职责。
/// </para>
///
/// <para>
/// <b>只有它能进入 Victory/Defeat 并触发结算</b>，且 <see cref="FinishRun"/> 幂等 ——
/// Boss 死亡、抵达终点、全员阵亡可能在同一帧触发，重复结算会重复发金币。
/// </para>
///
/// <para>
/// <b>推车耐久耗尽不是失败</b>（P0 定案 D-05）：它转入停摆，固定秒数后恢复部分耐久。
/// 否则一局会因为一次失误永久卡死 —— 既打不过去，也不结束。
/// </para>
/// </summary>
public class StageDirector : MonoBehaviour
{
    [Header("关卡对象")]
    [Tooltip("本关的推车。字段是 public：场景配置组件的引用本就该在 Inspector 里配，" +
             "而且脚本化接线时直接赋值比走 SerializedObject 可靠（后者对场景对象的对象引用不落盘）")]
    public CartController Cart;

    [Tooltip("敌人生成器；留空则不控制生成压力")]
    public EnemySpawner Spawner;

    [Header("推车停摆规则")]
    [Tooltip("耐久耗尽后停摆的秒数")]
    [SerializeField] private float _disabledSeconds = 8f;

    [Tooltip("停摆结束后恢复的耐久比例")]
    [Range(0f, 1f)]
    [SerializeField] private float _recoverRatio = 0.2f;

    /// <summary>当前阶段。</summary>
    public StagePhase Phase { get; private set; } = StagePhase.Initializing;

    /// <summary>本局是否已结束（结算只发生一次）。</summary>
    public bool IsFinished => _finished;

    /// <summary>阶段变化（UI 订阅它；生成器不再需要 —— 它自己从推车推导状态）。</summary>
    public event Action<StagePhase> PhaseChanged;

    /// <summary>本局结束，携带冻结结果。</summary>
    public event Action<RunResult> RunFinished;

    private bool _finished;
    private bool _hadPlayers;
    private float _disabledTimer;

    /// <summary>
    /// 本副本是否推进阶段与判胜负。联机时**只有服务端**（见 <see cref="NetworkAuthority"/>）：
    /// 客户端跟着推车的状态广播走，不自己判定。
    /// </summary>
    private NetworkAuthority _authority;

    private void Start()
    {
        _authority = new NetworkAuthority(gameObject);

        // 两端都要装：服务端用它广播状态与结算，客户端用它接收。
        // 运行时 AddComponent 而不是场景接线 —— 见 StageNetworkSync 的类注释
        EnsureNetworkSync();

        // 联机时阶段与胜负只在服务端推进。客户端也跑的话，它会自己判"抵达终点＝胜利"、
        // 自己弹结算面板 —— 而服务端也会弹一次，且两边的结算数值来自各自的统计
        if (!_authority.IsAuthority) return;

        if (Cart == null)
        {
            Debug.LogError("[StageDirector] 没有配置推车，关卡无法开始。");
            return;
        }

        // 注入随机源与推车。
        // 种子来自 RunSession（联机时由服务端下发）—— 各端必须用同一个种子，
        // 否则同样的操作会刷出不同的敌人；锚点用推车而不是玩家，压力才会来自路线方向。
        // 传的是 CartController 而不是它的 Transform：生成器要读"已行驶弧长"判断当前在哪一段。
        if (Spawner != null)
        {
            int seed = RunSessionService.Service?.Current.Seed ?? 0;
            Spawner.Configure(new System.Random(seed), Cart);
            Spawner.NodeCleared += OnNodeCleared;
        }
        else
        {
            Debug.LogWarning("[StageDirector] 没有配置敌人生成器，关卡里不会出现小怪。");
        }

        Cart.ReachedNode += OnCartReachedNode;
        Cart.DisabledChanged += OnCartDisabledChanged;

        EnterPhase(StagePhase.Travelling);
        Cart.StartMoving();
    }

    private void OnDestroy()
    {
        if (Cart != null)
        {
            Cart.ReachedNode -= OnCartReachedNode;
            Cart.DisabledChanged -= OnCartDisabledChanged;
        }

        if (Spawner != null) Spawner.NodeCleared -= OnNodeCleared;
    }

    private void Update()
    {
        if (!_authority.IsAuthority) return;
        if (_finished) return;

        TickDisabledRecovery();
        CheckDefeat();
    }

    // ── 阶段推进 ──

    /// <summary>
    /// 推车到达节点。**充能点不再"刷 Boss"** —— 刷什么、刷多少由生成表上该节点的规则决定
    /// （见 <c>Docs/EnemySpawnerDesign.md</c>），这里只把阶段切到"等待"。
    /// 车已经在 <c>CartController.CheckReachedNode</c> 里停下了。
    /// </summary>
    private void OnCartReachedNode(int node)
    {
        if (_finished) return;

        if (node == CartController.NodeDestination)
        {
            // 终点不停留：到点即胜利（"把物资运到哨站"这件事才算完成）
            FinishRun(RunOutcome.Victory);
            return;
        }

        EnterPhase(StagePhase.WaitingAtNode);
    }

    /// <summary>
    /// 生成器报告"这个节点刷完且清完了" → 放行。
    ///
    /// <para>
    /// <b>为什么放行权在这里而不是生成器里：</b>生成器只报告状态（与推车"只报告状态、不做决策"
    /// 同一条规矩），"要不要继续、这局算不算结束"必须只有一个权威。
    /// </para>
    /// </summary>
    private void OnNodeCleared(CartNodeKind kind)
    {
        if (_finished) return;

        if (Phase != StagePhase.WaitingAtNode)
        {
            Debug.LogWarning($"[StageDirector] 收到节点 {kind} 的清空报告，但当前阶段是 {Phase}，已忽略。");
            return;
        }

        EnterPhase(StagePhase.Travelling);
        Cart?.StartMoving();
    }

    private void EnterPhase(StagePhase phase)
    {
        if (Phase == phase) return;

        Phase = phase;
        PhaseChanged?.Invoke(phase);
    }

    // ── 联机：状态同步（见 StageNetworkSync）──

    /// <summary>本 GameObject 上的同步器（由 <see cref="Start"/> 运行时装上，两端都有）。</summary>
    private StageNetworkSync _networkSync;

    private void EnsureNetworkSync()
    {
        if (_networkSync != null) return;

        _networkSync = gameObject.AddComponent<StageNetworkSync>();
        _networkSync.Director = this;
    }

    /// <summary>
    /// 客户端应用服务端广播的阶段。
    ///
    /// <para>
    /// <b>只改状态 + 发事件</b>：阶段的**推进条件**（节点清完、全员阵亡、抵达终点）
    /// 全部留在服务端的 <see cref="Update"/> 里 —— 客户端不做决策，
    /// 否则"这一局为什么结束了"就有了两个来源。
    /// </para>
    /// </summary>
    public void ApplyNetworkPhase(StagePhase phase)
    {
        // 服务端不回放自己发出去的状态
        if (_authority.IsAuthority) return;

        EnterPhase(phase);
    }

    /// <summary>
    /// 客户端应用服务端下发的结算结果。
    ///
    /// <para>
    /// 它走的是本机的 <see cref="RunFinished"/>，于是场景里的 <c>RunSettlement</c> 会照常
    /// 写**本机**档案并弹面板 —— 金币与哨站进度本来就是每台机器一份的。
    /// 奖励数值以服务端下发的为准，客户端**不重算**（各自的击杀统计并不一样）。
    /// </para>
    /// </summary>
    public void ApplyNetworkResult(RunResult result)
    {
        if (_authority.IsAuthority) return;
        if (_finished) return;

        _finished = true;

        EnterPhase(result.IsVictory ? StagePhase.Victory : StagePhase.Defeat);
        RunFinished?.Invoke(result);
    }

    // ── 停摆与恢复 ──

    private void OnCartDisabledChanged(bool disabled)
    {
        if (_finished) return;

        if (!disabled)
        {
            _disabledTimer = 0f;

            // **恢复后立刻继续行驶**（若当前是行驶阶段）。
            // 手动修理走的也是这条路：旧实现只在 8 秒自动恢复的 TickDisabledRecovery 里调
            // StartMoving，于是"手动修好了但车不动"，看起来像修理没生效。
            // 停在充能点时**不**恢复行驶 —— 那时该不该走由节点清空（NodeCleared）决定。
            if (Phase == StagePhase.Travelling) Cart.StartMoving();
            return;
        }

        _disabledTimer = _disabledSeconds;

        Debug.Log($"[StageDirector] 推车耐久耗尽，停摆 {_disabledSeconds} 秒。");
    }

    private void TickDisabledRecovery()
    {
        if (Cart == null || !Cart.IsDisabled) return;

        _disabledTimer -= Time.deltaTime;
        if (_disabledTimer > 0f) return;

        // 是否继续行驶由 OnCartDisabledChanged(false) 统一决定（手动修理走同一条路）
        Cart.ExitDisabled(_recoverRatio);
    }

    // ── 胜负 ──

    /// <summary>
    /// 全员阵亡才算失败。**一名玩家死亡不能结束多人局** ——
    /// 单机下两者等价，但按多玩家写，联机时不用回来改判定（改判定往往漏掉某条路径）。
    /// </summary>
    private void CheckDefeat()
    {
        IPlayerManager players = PlayerManager.Service;
        if (players == null) return;

        IReadOnlyList<PlayerController> all = players.AllPlayers;
        if (all.Count > 0)
        {
            _hadPlayers = true;
            return;
        }

        // 玩家还没生成（进场第一帧）不算失败：否则开局瞬间就会被判负
        if (!_hadPlayers) return;

        FinishRun(RunOutcome.Defeat);
    }

    /// <summary>
    /// 结束本局。**幂等**：重复调用不会重复结算。
    ///
    /// <para>
    /// 结果在这里**冻结**（击杀数、耐久、奖励全部算好并抄进 <see cref="RunResult"/>），
    /// 之后结算面板与存档只消费这份快照，不再读可能已销毁的场景对象。
    /// </para>
    /// </summary>
    public void FinishRun(RunOutcome outcome)
    {
        if (!_authority.IsAuthority) return;
        if (_finished) return;
        _finished = true;

        Cart?.StopMoving();

        EnterPhase(outcome == RunOutcome.Victory ? StagePhase.Victory : StagePhase.Defeat);

        int kills = RunStatsTracker.Service != null ? RunStatsTracker.Service.TotalKills : 0;
        float health = Cart != null ? Cart.HealthNormalized : 0f;
        int reward = RewardCalculator.Calculate(outcome, kills, health);

        var result = new RunResult(
            outcome,
            RunSessionService.Service?.Current.StageId,
            GameLevelManager.Service != null ? GameLevelManager.Service.LevelTime : 0f,
            kills,
            health,
            reward);

        RunFinished?.Invoke(result);

        // 广播给客户端：它们各自写自己的档案 + 弹面板（奖励用服务端算好的这一份）。
        // Host 模式下这条消息也会回到自己，但 ApplyNetworkResult 会被权威守卫挡掉 ——
        // 否则本机会结算两次（发两次金币）
        _networkSync?.BroadcastResult(result);
    }
}
