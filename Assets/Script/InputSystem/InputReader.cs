using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "Game/Input Reader")]
public class InputReader : ScriptableObject, InputSystem_Actions.IPlayerActions
{
    // 事件
    public event System.Action EPressEvent;
    public event System.Action EscapePressEvent;

    // 移动直接暴露值，供 Polling
    public Vector2 MoveInput { get; private set; }
    public Vector2 AttackInput { get; private set; }

    /// <summary>
    /// E 键是否**正被按住**（轮询）。
    ///
    /// <para>
    /// <see cref="EPressEvent"/> 只在按下的那一帧发一次，表达不了"还按着" ——
    /// "按住 E 若干秒"这类持续交互必须轮询按键状态。
    /// </para>
    /// </summary>
    public bool InteractHeld => _inputActions != null && _inputActions.Player.EPress.IsPressed();

    /// <summary>
    /// 待处理的武器槽切换请求（-1 = 无）。由 <see cref="ConsumeSlotSwitchRequest"/> 取走并清空。
    ///
    /// <para>
    /// <b>为什么是"事件写入 + 轮询取走"：</b>按键在 Input System 的更新点触发回调，
    /// 而消费者（<c>PlayerController.Update</c>）按自己的节奏取用 —— 两者不是同一个时刻。
    /// 中间存一个待处理值，既不会漏按（回调一定跑过），也不会重复触发（取走即清空）。
    /// </para>
    /// </summary>
    private int _pendingSlotSwitch = -1;

    /// <summary>
    /// 取出并清空槽位切换请求（-1 = 本帧没有请求）。语义见 <see cref="IInputHandle.ConsumeSlotSwitchRequest"/>。
    /// </summary>
    public int ConsumeSlotSwitchRequest()
    {
        int request = _pendingSlotSwitch;
        _pendingSlotSwitch = -1;
        return request;
    }

    private InputSystem_Actions _inputActions;

    private void OnEnable()
    {
        if (_inputActions == null)
        {
            _inputActions = new InputSystem_Actions();
            _inputActions.Player.SetCallbacks(this); // 自动绑定接口方法
        }
        _inputActions.Enable();
    }

    private void OnDisable()
    {
        if (_inputActions != null)
            _inputActions.Disable();

        // 丢掉还没被取走的请求。**防御性**：当前没有代码切换 map（SwitchToUIMap 还没人调），
        // 但一旦接上"开面板就切 UI map"，面板期间按下的数字键就会留在待处理槽里，
        // 回到游戏时突然切一次武器 —— 那是"我明明没按"的表现。
        _pendingSlotSwitch = -1;
    }

    private void OnDestroy()
    {
        // InputActionAsset 实现了 IDisposable：不释放会在每次新建 InputReader 时留下原生资源
        // （编辑器关闭 Domain Reload 时同一会话内反复进出 Play 会更明显）
        if (_inputActions != null)
        {
            _inputActions.Disable();
            _inputActions.Dispose();
            _inputActions = null;
        }
    }


    #region 接口实现
    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }
    public void SetMoveInput(Vector2 input)
    {
        MoveInput = input;
    }

    public void OnEPress(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
            EPressEvent?.Invoke();
    }

    public void OnEscapePress(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
            EscapePressEvent?.Invoke();
    }

    /// <summary>武器槽 1（数字键 <c>1</c>）。见 <see cref="QueueSlotSwitch"/>。</summary>
    public void OnAlpha1(InputAction.CallbackContext context) => QueueSlotSwitch(context, 0);

    /// <summary>武器槽 2（数字键 <c>2</c>）。见 <see cref="QueueSlotSwitch"/>。</summary>
    public void OnAlpha2(InputAction.CallbackContext context) => QueueSlotSwitch(context, 1);

    /// <summary>
    /// 武器槽 3（数字键 <c>3</c>）。
    ///
    /// <para>
    /// 当前没有角色能带 3 把（枪手 2 / 工程师 1，HUD 也只有两个槽位按钮），
    /// 所以这个请求会被 <c>SwitchToSlot</c> 的越界判定拒绝、什么都不发生 ——
    /// 留着它是为了"武器槽位数据化之后不用再回来加按键"。
    /// </para>
    /// </summary>
    public void OnAlpha3(InputAction.CallbackContext context) => QueueSlotSwitch(context, 2);

    /// <summary>
    /// 记录一次槽位切换请求。
    ///
    /// <para>
    /// <b>只认 <see cref="InputActionPhase.Performed"/>：</b>生成的包装类把
    /// <c>started</c>/<c>performed</c>/<c>canceled</c> 都接到了同一个回调上，
    /// 不过滤就会一次按键记三遍（虽然值相同，但语义上"按下"只该发生一次）。
    /// 与 <see cref="OnEPress"/> 同一约定。
    /// </para>
    ///
    /// <para>
    /// <b>请求里的 0/1/2 是已装备槽序号</b>，不是候选武器下标 ——
    /// 映射由 <c>IWeaponManager.SwitchToSlot</c> 负责（见那里的说明）。
    /// </para>
    /// </summary>
    private void QueueSlotSwitch(InputAction.CallbackContext context, int equippedIndex)
    {
        if (context.phase != InputActionPhase.Performed) return;

        _pendingSlotSwitch = equippedIndex;
    }
    #endregion


    #region 输入管理
    public void EnableKey()
    {
        // _inputActions.Enable();  允许所有输入
    }
    public void DisableKey()
    {
        //_inputActions.Player.EPress.Disable();  禁用E键输入
    }
    #endregion


    #region Map切换
    public void SwitchToPlayerMap()
    {
        _inputActions.Player.Enable();
        _inputActions.UI.Disable();
    }
    public void SwitchToUIMap()
    {
        _inputActions.UI.Enable();
        _inputActions.Player.Disable();
    }
    #endregion
}