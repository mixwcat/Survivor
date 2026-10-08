using System;
using UnityEngine;

/// <summary>
/// 推车控制器 —— 沿**路径**自动行驶、停车、耐久耗尽后停摆与恢复。
///
/// <para>
/// <b>它只报告状态，不做决策</b>：到达节点、耐久耗尽、恢复完成都只是事件，
/// 由 <c>StageDirector</c> 决定"接下来进入哪个阶段"。推车自己不切场景、不发金币、不弹 UI ——
/// 否则"谁有权结束这一局"会散落到多个组件里，而结束逻辑必须幂等且唯一。
/// </para>
///
/// <para>
/// <b>路径从哪来：</b>关卡数据（<see cref="CartRouteSO"/>）由场景里的
/// <see cref="CartRouteSource"/> 在运行时注入（<see cref="SetRoute"/>）——
/// 推车 prefab 上的 <see cref="Route"/> **留空**，因为"每关走哪条路"是每关不同的数据。
/// 场景里怎么编辑路径见 <c>Docs/CartRouteAuthoring.md</c>。
/// </para>
///
/// <para>
/// <b>进度是"沿路径的弧长"，不是 x 位移</b>（灰盒时期是后者，路径一拐弯就失效）。
/// 弧长 <c>_distance</c> 是权威进度：位置由它算出来，所以到点判定与画面上看到的位置永远一致。
/// 移动走 <see cref="Rigidbody2D.MovePosition"/> —— 这是对"不要直接写 <c>transform.position</c>"
/// 的**有意例外**：Kinematic 刚体的正规移动方式就是它，仍在物理步进内；
/// 而"每帧给 linearVelocity 一个切线方向"会让实际位置与弧长互相漂移，到点判定就得反过来猜。
/// </para>
///
/// <para>
/// <b>耐久只有三个写入点</b>：<see cref="Repair"/>（玩家修理）、
/// <see cref="ExitDisabled"/>（停摆结束按比例恢复）、以及 <see cref="CartHealthController"/> 自己的
/// 受伤路径。除它们之外不要再从外部直接改血量 —— 那是"同一件事两个入口"的开始。
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CartController : EntityBehaviour
{
    /// <summary>
    /// 路线的节点序号：1 = 充能点一，2 = 充能点二，3 = 终点。
    /// 数值由 <see cref="CartNodeKind"/> 派生（**枚举是协议，不要重排编号**），
    /// <c>StageDirector</c> 按这些整数分支。
    /// </summary>
    public const int NodeCharge1 = (int)CartNodeKind.Charge1;
    public const int NodeCharge2 = (int)CartNodeKind.Charge2;
    public const int NodeDestination = (int)CartNodeKind.Destination;

    [Header("路径")]
    [Tooltip("关卡路径资产。**由场景里的 CartRouteSource 在运行时注入** —— " +
             "不要接在 prefab 上：路径是每关不同的数据")]
    public CartRouteSO Route;

    [Header("朝向")]
    [Tooltip("车头跟着路径切线转。俯视图不需要转向时关掉")]
    [SerializeField] private bool _faceAlongPath = true;

    [Tooltip("贴图朝向偏移（度）：贴图朝 +X 填 0，朝 +Y 填 90")]
    [SerializeField] private float _facingAngleOffset;

    private Rigidbody2D _rb;
    private CartHealthController _health;

    /// <summary>
    /// 本副本是否推进权威进度。联机时**只有服务端**跑 <see cref="Update"/>，
    /// 客户端的位置来自 <see cref="CartNetworkSync"/> 广播的状态（见 <see cref="NetworkAuthority"/>）。
    /// </summary>
    private NetworkAuthority _authority;

    /// <summary>由 <see cref="Route"/> 构造的运行时几何（累计弧长在这里，不落盘）。</summary>
    private CartPath _path;

    /// <summary>沿路径已行驶的弧长 —— **权威进度**。</summary>
    private float _distance;

    private int _nextNodeIndex;
    private bool _registered;
    private bool _warnedMoveRefused;
    private bool _pathErrorLogged;
    private bool _hasFaced;
    private float _lastFacingAngle;

    /// <summary>是否正在行驶。</summary>
    public bool IsMoving { get; private set; }

    /// <summary>耐久是否已耗尽（停摆中）。</summary>
    public bool IsDisabled { get; private set; }

    /// <summary>已沿路径推进的距离（弧长，不是 x 位移）。</summary>
    public float TravelledDistance => _distance;

    /// <summary>路径总长（没有路径时为 0）。</summary>
    public float RouteLength => _path != null ? _path.Length : 0f;

    /// <summary>
    /// 耐久比例（0..1），供结算与 HUD 使用。
    /// 血量本身归 <see cref="CartHealthController"/> 管 —— 这里只是转发与
    /// <see cref="Repair"/> 这一个语义化入口，免得"谁能改耐久"出现第二个入口。
    /// </summary>
    public float HealthNormalized => _health != null ? _health.HealthNormalized : 0f;

    /// <summary>到达路线节点（参数：<see cref="NodeCharge1"/> 等）。</summary>
    public event Action<int> ReachedNode;

    /// <summary>停摆状态变化（参数：是否停摆）。</summary>
    public event Action<bool> DisabledChanged;

    protected override void Awake()
    {
        base.Awake();

        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.gravityScale = 0f;

        _health = GetComponent<CartHealthController>();
        _authority = new NetworkAuthority(gameObject);
        // 路径不在这里读：它由 CartRouteSource 在 Start 注入（Awake 顺序不确定，见 SetRoute）
    }

    private void OnEnable()
    {
        SyncTargetRegistration();
    }

    private void OnDisable()
    {
        SyncTargetRegistration();
    }

    /// <summary>
    /// 注入路径（由场景里的 <see cref="CartRouteSource"/> 调用）。
    ///
    /// <para>
    /// 可以在任何时候调用：内部只重建几何、把进度归零，并把车**吸附到路径起点**
    /// （起点不一致会告警 —— 那是"车不在路径上"这种静默几何错误的唯一线索）。
    /// 不赌 Awake/Start 顺序：推车这边用 <see cref="EnsurePath"/> 懒检查。
    /// </para>
    /// </summary>
    public void SetRoute(CartRouteSO route)
    {
        Route = route;
        _path = route != null ? new CartPath(route) : null;
        _distance = 0f;
        _nextNodeIndex = 0;
        _pathErrorLogged = false;

        if (_path == null) return;

        if (_path.ValidationIssue != null)
        {
            Debug.LogError($"[{nameof(CartController)}] 路径「{route.name}」不可用：{_path.ValidationIssue}。" +
                           "车不会移动，请修正路径后重新烘焙。", this);
        }

        if (_path.WarningIssue != null)
        {
            Debug.LogWarning($"[{nameof(CartController)}] 路径「{route.name}」：{_path.WarningIssue}。", this);
        }

        SnapToRouteStart();
    }

    /// <summary>
    /// 把车吸附到路径起点。位置本来就是作者摆的，所以偏差通常很小；
    /// 偏差大说明"车与路径不是同一套坐标"（例如路径被移动过而没重新烘焙），必须说出来。
    /// </summary>
    private void SnapToRouteStart()
    {
        Vector2 start = _path.Evaluate(0f);
        Vector2 current = transform.position;
        float offset = Vector2.Distance(current, start);

        if (offset > 0.01f)
        {
            Debug.LogWarning($"[{nameof(CartController)}] 车不在路径起点上（偏差 {offset:F2}），已吸附到 {start}。" +
                             "若这不是预期的，请检查路径是否被移动过而没重新烘焙。", this);
        }

        if (_rb != null) _rb.position = start;
        else transform.position = start;
    }

    private void Update()
    {
        if (!EnsurePath()) return;

        // 联机时只有服务端推进进度。客户端也推进的话，两端会各自演化 ——
        // 而它们**碰巧**在大部分时间里一致（同一条路径、同一速度、同一时刻起步），
        // 所以这种错位往往要等到一次停摆/修理之后才暴露出来
        if (!_authority.IsAuthority) return;

        if (IsDisabled) return;

        if (IsMoving) Move();

        // 停车期间也检查：车可能被阶段逻辑停在半路，恢复行驶时不该漏掉已经越过的节点
        CheckReachedNode();
    }

    /// <summary>
    /// 客户端应用服务端广播的状态。
    ///
    /// <para>
    /// <b>刻意不触发 <see cref="ReachedNode"/></b>：到点该不该继续走是阶段决策，
    /// 归服务端的 <c>StageDirector</c>。客户端跟着广播走就行 —— 在客户端也发一遍事件，
    /// 会让"这辆车为什么停了"变成两个来源。
    /// </para>
    /// </summary>
    public void ApplyNetworkState(float distance, bool isMoving, bool isDisabled)
    {
        // 服务端不应用自己发出的状态（它是权威，只往前走，不回退）
        if (_authority.IsAuthority) return;

        // 路径还没注入（CartRouteSource 的 Start 还没跑）：这一帧丢掉，服务端 15Hz 一直在发
        if (!EnsurePath()) return;

        bool disabledChanged = IsDisabled != isDisabled;

        _distance = Mathf.Clamp(distance, 0f, _path.Length);
        IsMoving = isMoving;
        IsDisabled = isDisabled;

        ApplyTransformAtDistance();

        if (disabledChanged) DisabledChanged?.Invoke(IsDisabled);
    }

    /// <summary>
    /// 路径就绪检查（**懒检查**，不依赖初始化顺序）。
    /// 没有路径时车一动不动，所以这件事必须喊出来 —— 静默的"车不走"最难归因。
    /// </summary>
    private bool EnsurePath()
    {
        if (_path != null && _path.IsValid) return true;
        if (_pathErrorLogged) return false;

        _pathErrorLogged = true;

        if (Route == null)
        {
            Debug.LogError($"[{nameof(CartController)}] 没有注入路径：场景里应当有一个 " +
                           $"{nameof(CartRouteSource)} 指向本车。车不会移动。", this);
        }
        else if (_path == null || _path.ValidationIssue != null)
        {
            Debug.LogError($"[{nameof(CartController)}] 路径「{Route.name}」不可用：" +
                           $"{(_path != null ? _path.ValidationIssue : "未知")}。车不会移动。", this);
        }

        return false;
    }

    private void Move()
    {
        // 车速只有一个来源：数值系统（CartDataSO.MoveSpeed → StatModel）。
        // 曾经有一个 _moveSpeedOverride 字段，prefab 上留了 0.1 而数值是 1.5 ——
        // 结果是车速只有配置的 1/15，且所有车速升级静默失效
        float speed = GetStat(StatType.MoveSpeed);

        _distance = Mathf.Min(_distance + speed * Time.deltaTime, _path.Length);
        ApplyTransformAtDistance();
    }

    /// <summary>
    /// 把车摆到 <see cref="_distance"/> 对应的位置并对齐路径切线。
    /// 服务端推进（<see cref="Move"/>）与客户端应用广播（<see cref="ApplyNetworkState"/>）共用，
    /// 免得两条路径的摆位逻辑各写一份。
    /// </summary>
    private void ApplyTransformAtDistance()
    {
        Vector2 next = _path.Evaluate(_distance);
        if (_rb != null) _rb.MovePosition(next);
        else transform.position = next;

        FaceAlongPath();
    }

    /// <summary>车头对齐路径切线。朝向没变就不写 Transform（见 CLAUDE.md 性能红线）。</summary>
    private void FaceAlongPath()
    {
        if (!_faceAlongPath) return;

        Vector2 direction = _path.Tangent(_distance);
        if (direction.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + _facingAngleOffset;

        if (_hasFaced && Mathf.Abs(Mathf.DeltaAngle(_lastFacingAngle, angle)) < 0.1f) return;

        _hasFaced = true;
        _lastFacingAngle = angle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void CheckReachedNode()
    {
        if (_nextNodeIndex >= _path.NodeCount) return;
        if (_distance < _path.NodeDistanceAt(_nextNodeIndex)) return;

        CartNodeKind kind = _path.NodeKindAt(_nextNodeIndex);
        _nextNodeIndex++;

        // 到点先停：是否继续由 StageDirector 决定（充能点要打 Boss，终点要结算）
        StopMoving();
        ReachedNode?.Invoke((int)kind);
    }

    // ── 由 StageDirector 驱动 ──

    /// <summary>
    /// 开始行驶。停摆中被拒绝时返回 false 并**告警一次** ——
    /// 以前这里是静默 return，于是"调用方以为车动了"这种失败完全不可观测。
    /// </summary>
    public bool StartMoving()
    {
        if (IsDisabled)
        {
            if (!_warnedMoveRefused)
            {
                _warnedMoveRefused = true;
                Debug.LogWarning($"[{nameof(CartController)}] 停摆中收到 StartMoving，已拒绝。" +
                                 "停摆要先恢复（ExitDisabled / Repair）才能行驶。", this);
            }

            return false;
        }

        IsMoving = true;
        return true;
    }

    /// <summary>停车（保留位置与耐久）。</summary>
    public void StopMoving()
    {
        IsMoving = false;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 修理：**玩家修理的唯一入口**。
    /// <list type="bullet">
    /// <item>停摆中 → 退出停摆并恢复到「当前耐久 + 这一笔」（<see cref="ExitDisabled"/> 内部按比例恢复）；</item>
    /// <item>未停摆 → 直接加血。</item>
    /// </list>
    ///
    /// <para>
    /// 以前这两条路分别写在交互物里（一条走 <c>ExitDisabled</c>、一条 <c>GetComponent</c> 后
    /// 直接 <c>Heal</c>），于是同一个动作在停摆/非停摆下反馈不一致，
    /// 而且"耐久只有 <see cref="CartHealthController"/> 一个入口"的注释与实现相反。
    /// </para>
    /// </summary>
    public void Repair(float ratio)
    {
        if (_health == null) return;

        if (IsDisabled)
        {
            ExitDisabled(HealthNormalized + ratio);
            return;
        }

        _health.Heal(_health.MaxHealth * ratio);
    }

    /// <summary>
    /// 耐久耗尽：停摆并进入停摆状态。
    /// 由 <see cref="CartHealthController"/> 在血量归零时调用 —— 推车**不销毁**，
    /// 销毁会让"把物资运到下一个哨站"这件事无从继续，也会让结算拿不到耐久数据。
    /// </summary>
    public void EnterDisabled()
    {
        if (IsDisabled) return;

        IsDisabled = true;
        StopMoving();

        // 耐久为 0 → 退出敌人目标表（判据在 SyncTargetRegistration 里统一）
        SyncTargetRegistration();

        DisabledChanged?.Invoke(true);
    }

    /// <summary>停摆结束：恢复到指定比例的耐久并允许重新行驶（是否立刻开动仍由阶段决定）。</summary>
    public void ExitDisabled(float recoverRatio)
    {
        if (!IsDisabled) return;

        IsDisabled = false;

        // 与 EnterDisabled 的注销成对（判据在 SyncTargetRegistration 里统一）
        SyncTargetRegistration();

        _health?.RecoverTo(recoverRatio);
        DisabledChanged?.Invoke(false);
    }

    /// <summary>
    /// 注册 / 注销敌人目标的**唯一入口**：判据是「启用中 且 未停摆」。
    ///
    /// <para>
    /// 以前 <c>OnEnable</c> 无条件注册、<c>EnterDisabled</c>/<c>ExitDisabled</c> 各自注册注销 ——
    /// 三处入口各自成立，于是"停摆中的车被禁用再启用"会重新变成可攻击目标
    /// （停摆期间退出目标表的设计静默失效）。
    /// </para>
    /// </summary>
    private void SyncTargetRegistration()
    {
        bool shouldRegister = isActiveAndEnabled && !IsDisabled;
        if (shouldRegister == _registered) return;

        _registered = shouldRegister;

        if (shouldRegister) EnemyTargetRegistry.Service?.Register(transform);
        else EnemyTargetRegistry.Service?.Unregister(transform);
    }
}
