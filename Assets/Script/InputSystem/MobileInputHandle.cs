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
    private Touch? _cachedTouch;

    public MobileInputHandle(Joystick moveJoystick, Joystick attackJoystick)
    {
        _moveJoystick = moveJoystick;
        _attackJoystick = attackJoystick;
    }

    /// <summary>
    /// 每帧更新触摸缓存（需要外部调用，建议在 MonoBehaviour.Update 中）
    /// </summary>
    public void UpdateTouchCache()
    {
        if (Input.touchCount > 0)
        {
            _cachedTouch = Input.GetTouch(0);
        }
        else
        {
            _cachedTouch = null;
        }
    }

    // 移动输入：左摇杆方向
    public Vector2 MoveInput => _moveJoystick != null ? _moveJoystick.Direction : Vector2.zero;

    // 攻击方向输入：右摇杆方向
    public Vector2 AttackDirectionInput => _attackJoystick != null ? _attackJoystick.Direction : Vector2.zero;

    // 屏幕指针位置：缓存的触摸点位置
    public Vector2 ScreenPointerPosition => _cachedTouch?.position ?? Vector2.zero;

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

    // 交互事件：Android 需要 UI 按钮触发（暂不实现，保持空）
    public event System.Action OnInteract;

    // 返回事件：Android 需要 UI 按钮触发（暂不实现，保持空）
    public event System.Action OnEscape;

    /// <summary>
    /// 触发交互事件（供 UI 按钮调用）
    /// </summary>
    public void TriggerInteract()
    {
        OnInteract?.Invoke();
    }

    /// <summary>
    /// 触发返回事件（供 UI 按钮调用）
    /// </summary>
    public void TriggerEscape()
    {
        OnEscape?.Invoke();
    }
}
