using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Android 平台输入实现（触屏+虚拟摇杆）
/// 适配器模式：将 Joystick Pack 插件适配到 IInputHandle 接口
/// </summary>
public class MobileInputHandle : IInputHandle
{
    private readonly Joystick _moveJoystick;
    private readonly Joystick _attackJoystick;

    public MobileInputHandle(Joystick moveJoystick, Joystick attackJoystick)
    {
        _moveJoystick = moveJoystick;
        _attackJoystick = attackJoystick;
    }

    // 移动输入：左摇杆方向
    public Vector2 MoveInput => _moveJoystick != null ? _moveJoystick.Direction : Vector2.zero;

    /// <summary>
    /// 瞄准方向 = 攻击摇杆方向。<see cref="worldOrigin"/> 用不上（摇杆给的就是方向本身）。
    /// 摇杆回中时返回 false，调用方保持上一次朝向 —— 这条判断原先写在武器里，
    /// 现在收敛到输入层（与 PC 的「鼠标始终有效」形成一致的契约）。
    /// </summary>
    public bool TryGetAimDirection(Vector2 worldOrigin, out Vector2 worldDirection)
    {
        Vector2 raw = _attackJoystick != null ? _attackJoystick.Direction : Vector2.zero;

        if (raw.sqrMagnitude < 0.01f)
        {
            worldDirection = Vector2.zero;
            return false;
        }

        worldDirection = raw.normalized;
        return true;
    }

    // 世界触控：过滤 UI 区域的触摸（摇杆等）
    public bool TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp)
    {
        screenPos = Vector2.zero;
        isDown = false;
        isUp = false;

        if (Input.touchCount == 0)
            return false;

        // 查找第一个不在 UI 上的触摸点
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            // 过滤掉在 UI 上的触摸（摇杆等）
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                continue;

            screenPos = touch.position;
            isDown = touch.phase == TouchPhase.Began;
            isUp = touch.phase == TouchPhase.Ended;
            return true;
        }

        // 所有触摸都在 UI 上
        return false;
    }

    // 取消输入：Android 无物理按键，返回 false
    public bool HasCancelInput => false;

    /// <summary>
    /// 触屏没有槽位切换键：HUD 的槽位按钮**直接调** <c>PlayerWeaponController.SwitchToSlot</c>
    /// （按钮是 UI 事件，不是输入设备）。这里恒返回 -1，保持接口契约完整。
    /// </summary>
    public int ConsumeSlotSwitchRequest() => -1;

    /// <summary>
    /// 摇杆是否仍然存在。场景切换会销毁摇杆，此时本句柄必须由工厂重建，
    /// 否则 <see cref="MoveInput"/> 会永远返回零（玩家无法移动，且不会有任何报错）。
    /// </summary>
    public bool IsAlive => _moveJoystick != null && _attackJoystick != null;

    // 交互事件：Android 需要 UI 按钮触发（暂不实现，保持空）
    public event System.Action OnInteract;

    /// <summary>
    /// 触屏是否正被按住 —— 由世界空间提示 UI 在触摸按下/抬起时写入。
    ///
    /// <para>
    /// 输入层读不到"手指还按着屏幕"（那是 UI 事件），所以由**唯一的世界触控入口**
    /// （<c>InteractionPromptView</c>）上报。这样"按住若干秒"的交互在触屏上是真正的按住，
    /// 而不是"站在范围内就自动进行" —— 后者会让玩家路过推车时被定身。
    /// </para>
    /// </summary>
    public static bool TouchHeld { get; set; }

    /// <summary>触屏按住状态（见 <see cref="TouchHeld"/>）。</summary>
    public bool InteractHeld => TouchHeld;

    // 返回事件：Android 需要 UI 按钮触发（暂不实现，保持空）
    public event System.Action OnEscape;

    /// <summary>触发交互事件（供 UI 按钮调用）</summary>
    public void TriggerInteract()
    {
        OnInteract?.Invoke();
    }

    /// <summary>触发返回事件（供 UI 按钮调用）</summary>
    public void TriggerEscape()
    {
        OnEscape?.Invoke();
    }
}
