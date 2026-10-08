using UnityEngine;

/// <summary>
/// 零输入句柄 —— 用于「输入源尚未实现」的场合（目前只有联机的 <c>network_X</c> 分支）。
///
/// <para>
/// <b>为什么不是「回落到本地输入」：</b>那会让远程玩家读到本地设备的键盘/摇杆，
/// 表现成「远程角色跟着我动」，而且不会有任何报错 —— 这类静默串号最难排查。
/// 零输入至少让症状与因果一致：「该输入源还没接入」→「这个角色不动」。
/// </para>
///
/// <para>
/// <see cref="IsAlive"/> 恒为 true：本句柄不依赖任何场景对象，不需要被工厂重建。
/// </para>
/// </summary>
public sealed class NullInputHandle : IInputHandle
{
    public Vector2 MoveInput => Vector2.zero;

    public bool TryGetAimDirection(Vector2 worldOrigin, out Vector2 worldDirection)
    {
        worldDirection = Vector2.zero;
        return false;
    }

    public bool TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp)
    {
        screenPos = Vector2.zero;
        isDown = false;
        isUp = false;
        return false;
    }

    public bool HasCancelInput => false;

    /// <summary>空句柄不产生任何输入（联机占位 / 未实现平台）。</summary>
    public int ConsumeSlotSwitchRequest() => -1;

    // 空访问器：本句柄永远不产生输入事件（写成 add/remove 而不是自动事件，避免 CS0067 警告）
    public event System.Action OnInteract { add { } remove { } }
    public event System.Action OnEscape { add { } remove { } }

    /// <summary>零输入：永远不认为交互键被按住。</summary>
    public bool InteractHeld => false;

    public bool IsAlive => true;
}
