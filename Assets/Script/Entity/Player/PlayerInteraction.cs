using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家交互系统 —— 实现 <see cref="IInteractor"/>：维护附近可交互物、选出当前目标、处理交互输入。
///
/// <para>
/// <b>选择策略：优先级降序 → 距离升序（<c>sqrMagnitude</c>）→ 注册序。</b>
/// 旧实现取"最后进入范围的那个"，结果取决于物理回调顺序：两座塔的范围重叠时选中谁不确定，
/// 大厅里交互物挨着时也一样。距离比较用平方距离，不开方。
/// </para>
///
/// <para>
/// <b>不可用的目标不参与选择</b>（<see cref="IInteractable.CanInteract"/> 为 false 的直接跳过）：
/// 枪手站在塔边不该看到"按 E 管理防御塔"的提示，更不该按下去才在日志里被拒绝。
/// </para>
///
/// <para>
/// <b>按玩家隔离：</b>候选列表与当前目标都是本玩家自己的状态；领域校验用
/// <see cref="Player"/>，不读全局 <c>LocalPlayer</c>。
/// </para>
/// </summary>
public class PlayerInteraction : MonoBehaviour, IInteractor
{
    [SerializeField]
    [Tooltip("输入标识：local=本地，network_X=远程玩家（联机用）")]
    private string _inputHandleId = InputHandleFactory.LocalId;

    private IInputHandle _inputHandle;
    private PlayerController _player;

    private readonly List<IInteractable> _nearby = new List<IInteractable>();
    private IInteractable _current;

    /// <summary>当前交互目标（只读访问）。</summary>
    public IInteractable Current => _current;

    /// <summary>当前目标变化（参数可能为 null）。</summary>
    public event Action<IInteractable> CurrentChanged;

    /// <inheritdoc />
    public Transform Origin => transform;

    /// <inheritdoc />
    public PlayerController Player => _player != null ? _player : (_player = GetComponent<PlayerController>());

    /// <summary>
    /// 是否本地玩家。只有本地玩家订阅交互输入 —— 联机时所有玩家共用 <c>local</c> 句柄的话，
    /// 按一次 E 会让每个玩家实例都执行一遍交互。
    ///
    /// <para>
    /// 判据是**两条**：输入 id 是 <c>local</c>（单机/本地玩家），且本实例确实由本机拥有
    /// （<see cref="LocalPlayerGuard"/>）。只看前者的旧判据在联机下失效 ——
    /// <c>_inputHandleId</c> 是序列化字段，每个副本都是 <c>"local"</c>。
    /// </para>
    /// </summary>
    public bool IsLocal => _guard.IsLocal && _inputHandleId == InputHandleFactory.LocalId;

    /// <summary>本实例是不是本机玩家（见 <see cref="LocalPlayerGuard"/>）。</summary>
    private LocalPlayerGuard _guard;

    /// <summary>已订阅的角色控制器（未订阅时为 null）。</summary>
    private PlayerRoleController _role;
    private bool _roleSubscribed;

    private void Awake()
    {
        _player = GetComponent<PlayerController>();
        _guard = new LocalPlayerGuard(gameObject);

        // 联机对象的 Awake 跑在 isLocalPlayer 赋值之前 —— 那时 IsLocal 还是 false，
        // 接输入要等 NetworkPlayerState.OnStartLocalPlayer 回调 OnBecameLocalPlayer
        if (!IsLocal) return;

        SubscribeInput();
    }

    /// <summary>
    /// 由 <see cref="PlayerController.NotifyBecameLocalPlayer"/> 调用：网络对象确认"本机拥有"之后补接输入。
    /// 单机路径永远走不到这里（Awake 里就已经接上了）。
    /// </summary>
    internal void OnBecameLocalPlayer()
    {
        if (_inputHandle != null) return;
        if (_inputHandleId != InputHandleFactory.LocalId) return;

        SubscribeInput();

        // OnEnable 早于本回调（SetActive 先跑），那时 _inputHandle 还是 null、没订阅上
        if (_inputHandle != null && isActiveAndEnabled)
        {
            _inputHandle.OnInteract -= HandleInteractInput;   // 幂等：先退再订，避免重复
            _inputHandle.OnInteract += HandleInteractInput;
        }
    }

    private void SubscribeInput()
    {
        _inputHandle = InputHandleFactory.GetInput(_inputHandleId);

        if (_inputHandle == null)
        {
            Debug.LogError("[PlayerInteraction] 创建 IInputHandle 失败！检查 InputHandleFactory 的日志。");
        }
    }

    private void OnEnable()
    {
        if (_inputHandle != null)
            _inputHandle.OnInteract += HandleInteractInput;

        EnsureRoleSubscription();
    }

    private void OnDisable()
    {
        if (_inputHandle != null)
            _inputHandle.OnInteract -= HandleInteractInput;

        UnsubscribeRole();

        // 玩家离场（禁用/销毁/切场景）时清空候选并取消选中：
        // 留着的条目会让交互物以为"还有人在范围内"，提示也不会收起
        ClearNearby();
    }

    private void OnDestroy()
    {
        // 与 SubscribeInput 的 GetInput 成对。只有真的拿到过句柄才归还，
        // 否则会减掉别人的引用计数
        if (_inputHandle == null) return;

        InputHandleFactory.ReleaseInput(_inputHandleId);
        _inputHandle = null;
    }

    #region 交互对象管理

    /// <inheritdoc />
    public void Register(IInteractable target)
    {
        if (!InteractorResolver.IsAlive(target)) return;

        // 角色可能比本组件晚就绪（玩家先站到武器台旁边、再在选角面板里选人）
        EnsureRoleSubscription();

        if (!_nearby.Contains(target)) _nearby.Add(target);

        RefreshCurrent();
    }

    /// <inheritdoc />
    public void Unregister(IInteractable target)
    {
        if (!_nearby.Remove(target)) return;

        RefreshCurrent();
    }

    /// <summary>清空候选列表并取消当前选中（玩家离场时调用）。</summary>
    private void ClearNearby()
    {
        _nearby.Clear();

        if (_current == null) return;

        SetCurrent(null);
    }

    /// <summary>
    /// 订阅角色变更：目标"能不能交互"取决于能力位（枪手站塔边不该有提示），
    /// 而角色可能在本组件之后才就绪 —— 进大厅先弹选角面板，玩家完全可能已经站在武器台旁边。
    ///
    /// <para>
    /// 不订阅的话，选完角色后提示要等"走出范围再进来"才出现，
    /// 表现为"站在台子前什么都没发生"，而且不会有任何报错。
    /// </para>
    /// </summary>
    private void EnsureRoleSubscription()
    {
        if (_roleSubscribed) return;

        PlayerController player = Player;
        _role = player != null ? player.Role : null;
        if (_role == null) return;

        _role.RoleChanged += HandleRoleChanged;
        _roleSubscribed = true;
    }

    private void UnsubscribeRole()
    {
        if (!_roleSubscribed) return;

        if (_role != null) _role.RoleChanged -= HandleRoleChanged;

        _roleSubscribed = false;
        _role = null;
    }

    private void HandleRoleChanged(CharacterDefinitionSO definition)
    {
        // 能力位变了 → 之前不可选的目标可能变可选（或反过来）
        RefreshCurrent();
    }

    /// <summary>
    /// 重新选出当前目标：先剔除已销毁的条目，再按"优先级 → 距离 → 注册序"取最优。
    /// 只在注册/注销时重算（事件驱动，零逐帧开销）。
    /// </summary>
    private void RefreshCurrent()
    {
        for (int i = _nearby.Count - 1; i >= 0; i--)
        {
            // Unity 在对方被 Destroy 时不保证补发 OnTriggerExit2D，
            // 残留条目会让后续调用抛 MissingReferenceException（`?.` 对伪 null 无效）
            if (!InteractorResolver.IsAlive(_nearby[i])) _nearby.RemoveAt(i);
        }

        IInteractable best = PickBest();

        if (ReferenceEquals(best, _current)) return;

        SetCurrent(best);
    }

    private IInteractable PickBest()
    {
        IInteractable best = null;
        int bestPriority = int.MinValue;
        float bestSqrDistance = float.MaxValue;

        Vector2 origin = transform.position;

        for (int i = 0; i < _nearby.Count; i++)
        {
            IInteractable candidate = _nearby[i];

            // 不可用的目标不参与选择：靠近塔却没有塔管理能力时，不该显示提示
            if (!candidate.CanInteract(this, out _)) continue;

            Transform anchor = candidate.Anchor;
            if (anchor == null) continue;

            int priority = candidate.Priority;
            float sqrDistance = ((Vector2)anchor.position - origin).sqrMagnitude;

            bool better = best == null
                          || priority > bestPriority
                          || (priority == bestPriority && sqrDistance < bestSqrDistance);

            if (!better) continue;

            best = candidate;
            bestPriority = priority;
            bestSqrDistance = sqrDistance;
        }

        return best;
    }

    private void SetCurrent(IInteractable next)
    {
        IInteractable previous = _current;
        _current = next;

        if (InteractorResolver.IsAlive(previous)) previous.OnDeselected(this);
        if (InteractorResolver.IsAlive(next)) next.OnSelected(this);

        CurrentChanged?.Invoke(_current);
    }

    #endregion

    #region 交互执行

    private void HandleInteractInput()
    {
        TryInteract();
    }

    /// <inheritdoc />
    public bool TryInteract()
    {
        // 只有本地玩家能主动发起交互（远程玩家由网络层驱动，见 IInteractor 注释）
        if (!IsLocal) return false;

        IInteractable target = _current;

        if (!InteractorResolver.IsAlive(target))
        {
            // 目标在选中之后被销毁：重算一次，本次按键不执行
            RefreshCurrent();
            return false;
        }

        if (!target.CanInteract(this, out string reason))
        {
            Debug.Log($"[PlayerInteraction] 交互被拒绝：{reason}");
            RefreshCurrent();
            return false;
        }

        target.Interact(this);
        return true;
    }

    /// <summary>
    /// 交互键是否正被按住。远程玩家（<c>network_X</c>）没有本地句柄 → 恒 false：
    /// 他们的按住状态要由网络同步，本地读不到，宁可判为"没按"也不要误读本地键盘。
    /// </summary>
    public bool IsInteractHeld => _inputHandle != null && _inputHandle.InteractHeld;

    #endregion
}
