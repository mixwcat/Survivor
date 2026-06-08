using UnityEngine;

/// <summary>
/// PC 平台输入实现（Windows 鼠标+键盘）
/// 适配器模式：将 InputReader（新版 Input System）适配到 IInputHandle 接口
/// </summary>
public class PCInputHandle : IInputHandle
{
    private readonly InputReader _inputReader;

    public PCInputHandle(InputReader inputReader)
    {
        _inputReader = inputReader;
    }

    // 移动输入：代理到 InputReader（WASD/方向键）
    public Vector2 MoveInput => _inputReader.MoveInput;

    // 攻击方向输入：Windows 不使用（由 ScreenPointerPosition 计算）
    public Vector2 AttackDirectionInput => Vector2.zero;

    // 屏幕指针位置：鼠标位置
    public Vector2 ScreenPointerPosition => Input.mousePosition;

    // 世界触控：鼠标始终有效，无需过滤 UI
    public bool TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp)
    {
        screenPos = Input.mousePosition;
        isDown = Input.GetMouseButtonDown(0);
        isUp = Input.GetMouseButtonUp(0);
        return true;  // 鼠标始终有效
    }

    // 取消输入：鼠标右键
    public bool HasCancelInput => Input.GetMouseButtonDown(1);

    // 交互事件：代理到 InputReader 的 E 键事件
    public event System.Action OnInteract
    {
        add => _inputReader.EPressEvent += value;
        remove => _inputReader.EPressEvent -= value;
    }

    // 返回事件：代理到 InputReader 的 ESC 键事件
    public event System.Action OnEscape
    {
        add => _inputReader.EscapePressEvent += value;
        remove => _inputReader.EscapePressEvent -= value;
    }
}
