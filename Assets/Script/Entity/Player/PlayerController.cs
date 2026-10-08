using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家控制器
/// 继承 EntityBehaviour，所有数值从 StatModel 读取
/// 负责：移动控制、输入系统初始化
/// </summary>
public class PlayerController : EntityBehaviour
{
    [Header("组件")]
    private Rigidbody2D rb;

    [Header("输入系统")]
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = InputHandleFactory.LocalId;
    private IInputHandle _inputHandle;

    /// <summary>本帧采样到的移动输入（Update 写、FixedUpdate 读）。</summary>
    private Vector2 _moveInput;

    /// <summary>
    /// 移动锁（令牌式）：>0 时忽略移动输入。用于"交互期间定身"（如按住 E 修车）。
    /// 用集合而不是布尔，避免两个来源互相解锁（与 <c>GameLevelManager</c> 的暂停令牌同一套约定）。
    /// </summary>
    private readonly HashSet<object> _moveLocks = new HashSet<object>();

    /// <summary>是否被定身（>0 个来源持有移动锁）。</summary>
    public bool IsMovementLocked => _moveLocks.Count > 0;

    /// <summary>申请移动锁（令牌式，重复申请同一来源是空操作）。</summary>
    public void AcquireMoveLock(object source)
    {
        if (source != null) _moveLocks.Add(source);
    }

    /// <summary>释放移动锁（未持有是空操作）。</summary>
    public void ReleaseMoveLock(object source)
    {
        if (source != null) _moveLocks.Remove(source);
    }

    [Header("经验系统")]
    [SerializeField]
    [Tooltip("玩家自身的经验控制器；未拖拽时从同物体自动获取")]
    private ExperienceLevController _experienceController;

    /// <summary>玩家经验控制器（自身组件，按玩家实例化）</summary>
    public IExperienceController ExperienceController => _experienceController;

    [Header("武器系统")]
    private PlayerWeaponController _weapons;

    /// <summary>玩家武器控制器（按玩家实例化）</summary>
    public IWeaponManager Weapons => _weapons;

    [Header("升级系统")]
    private PlayerUpgradeController _upgrades;

    /// <summary>玩家升级选项控制器（按玩家实例化）</summary>
    public IPlayerUpgradeController Upgrades => _upgrades;

    [Header("成长系统")]
    private UpgradePointWallet _wallet;
    private PlayerProgressionController _progression;
    private PlayerRoleController _role;

    /// <summary>升级点钱包（按玩家实例化）—— 塔建造/升级与武器定向升级的通用货币。</summary>
    public IUpgradePointWallet UpgradePoints => _wallet;

    /// <summary>成长进度：升级 → 发升级点 + 排队一次免费三选一。</summary>
    public PlayerProgressionController Progression => _progression;

    /// <summary>本局角色（能力权限的判据）。定义由场景入口按 RunSession 注入。</summary>
    public PlayerRoleController Role => _role;

    protected override void Awake()
    {
        base.Awake();

        if (_experienceController == null)
            _experienceController = GetComponent<ExperienceLevController>();
        if (_weapons == null)
            _weapons = GetComponent<PlayerWeaponController>();
        if (_upgrades == null)
            _upgrades = GetComponent<PlayerUpgradeController>();
        if (_wallet == null)
            _wallet = GetComponent<UpgradePointWallet>();
        if (_progression == null)
            _progression = GetComponent<PlayerProgressionController>();
        if (_role == null)
            _role = GetComponent<PlayerRoleController>();

        // Rigidbody2D 在 Awake 取，**不要**放 Start：玩家是运行时生成的（PlayerSpawner），
        // FixedUpdate 可能先于 Start 跑，那时 rb 还是 null —— 表现成"按了不动"，
        // 而且每个物理帧抛一次 NullReferenceException
        rb = GetComponent<Rigidbody2D>();

        // 通过工厂按 ID 获取输入处理器
        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

        if (_inputHandle == null)
        {
            Debug.LogError("Failed to create IInputHandle! Check InputHandleFactory logs.");
        }
    }

    /// <summary>
    /// 移动死区：输入向量长度低于它按"没有输入"处理。
    ///
    /// <para>
    /// <b>它只是兜底</b>：真正的摇杆死区配在 InputAction 的 <c>StickDeadzone</c> 处理器上。
    /// 这里挡一道是因为下面的 <c>normalized</c> 会把 0.01 的漂移放大成**满速**移动；
    /// 阈值必须小，否则轻推摇杆会被整段吃掉（键盘给的是 ±1，不受影响）。
    /// </para>
    /// </summary>
    private const float MoveDeadZone = 0.05f;

    void FixedUpdate()
    {
        ApplyMove();
    }

    /// <summary>
    /// 逐帧采样移动输入（<c>Update</c>），物理帧消费（<c>FixedUpdate</c>）。
    ///
    /// <para>
    /// <b>为什么不能在 FixedUpdate 里直接读输入：</b>物理帧只有 50Hz，而输入是逐帧采样的 ——
    /// 一次"按下又松开"如果整段落在两个物理帧之间就会被整段丢掉（轻按没反应）；
    /// 松开同样最多要等一个物理帧才生效（松开后还在走一截）。
    /// 采样放在 Update 里，物理帧消费到的永远是最新值。
    /// </para>
    /// </summary>
    private void SampleMoveInput()
    {
        if (_inputHandle == null)
        {
            _moveInput = Vector2.zero;
            return;
        }

        Vector2 raw = _inputHandle.MoveInput;

        _moveInput = raw.sqrMagnitude < MoveDeadZone * MoveDeadZone ? Vector2.zero : raw;
    }

    /// <summary>把采样到的输入写进刚体速度（速度从 StatModel 读取）。</summary>
    private void ApplyMove()
    {
        if (rb == null) return;

        // 定身：速度**清零**而不是只忽略输入 —— 否则会带着旧速度滑走一段
        if (IsMovementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float speed = GetStat(StatType.MoveSpeed);
        rb.linearVelocity = _moveInput.normalized * speed;
    }

    /// <summary>
    /// 逐帧采样移动输入 + 消费武器槽切换请求。
    ///
    /// <para>
    /// <b>移动采样必须在 Update：</b>见 <see cref="SampleMoveInput"/> ——
    /// 物理帧只有 50Hz，在 <c>FixedUpdate</c> 里读输入会漏掉整段落在两个物理帧之间的轻按。
    /// </para>
    ///
    /// <para>
    /// <b>槽位切换同样必须在 Update 而不是物理帧：</b>请求是**一次性**的
    /// （<see cref="IInputHandle.ConsumeSlotSwitchRequest"/> 取走即清空），
    /// 而"待处理的请求"只有一个槽位 —— 物理帧率低于渲染帧率时，两次按键之间若没有取用，
    /// 后一次会覆盖前一次，表现为"按了两下只切了一次"。
    /// </para>
    ///
    /// <para>
    /// 请求里的 <c>0/1</c> 是**已装备槽序号**（第 1/2 把带出门的武器），由
    /// <see cref="IWeaponManager.SwitchToSlot"/> 负责映射；不要在这里改写成候选下标。
    /// </para>
    /// </summary>
    private void Update()
    {
        SampleMoveInput();

        if (_inputHandle == null) return;

        int request = _inputHandle.ConsumeSlotSwitchRequest();
        if (request >= 0) _weapons?.SwitchToSlot(request);
    }

    private void OnEnable()
    {
        PlayerManager.Service?.Register(this);

        // 敌人目标表：与 PlayerManager 分开是因为两者的读者不同 ——
        // 前者回答"有哪些玩家"（HUD、升级面板、传送门），后者回答"怪该打谁"。
        EnemyTargetRegistry.Service?.Register(transform);
    }

    private void OnDisable()
    {
        PlayerManager.Service?.Unregister(this);
        EnemyTargetRegistry.Service?.Unregister(transform);
    }

    private void OnDestroy()
    {
        // 与 Awake 的 GetInput 成对，避免共享句柄的引用计数只增不减
        InputHandleFactory.ReleaseInput(_inputHandleId);
        _inputHandle = null;
    }
}
