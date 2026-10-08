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

    /// <summary>
    /// 瞄准换算用的相机。只在失效时重取 —— <c>Camera.main</c> 是 tag 查找，不能放在逐帧路径上；
    /// 而缓存下来的相机在场景切换后会变成伪 null（Unity 重载了 ==），
    /// 所以判据是「相机是否仍有效」而不是「是否取过」。
    /// </summary>
    private Camera _aimCamera;

    /// <summary>鼠标屏幕坐标 → 世界方向。Windows 的瞄准逻辑集中在这里，玩法代码零平台分支。</summary>
    public bool TryGetAimDirection(Vector2 worldOrigin, out Vector2 worldDirection)
    {
        worldDirection = Vector2.zero;

        if (_aimCamera == null) _aimCamera = Camera.main;
        if (_aimCamera == null) return false;

        Vector3 mouseWorld = _aimCamera.ScreenToWorldPoint(Input.mousePosition);
        Vector2 delta = new Vector2(mouseWorld.x - worldOrigin.x, mouseWorld.y - worldOrigin.y);

        // 鼠标正好压在武器上：没有有效方向（零向量会让 Atan2 得到无意义的角度）
        if (delta.sqrMagnitude < 0.0001f) return false;

        worldDirection = delta.normalized;
        return true;
    }

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

    /// <summary>
    /// 槽位切换请求：代理到 <see cref="InputReader"/>（新版 Input System 的
    /// <c>Alpha1/2/3</c> → 数字键 1/2/3）。
    ///
    /// <para>
    /// <b>本方法以前直接用旧版 <c>Input.GetKeyDown(KeyCode.Alpha1)</c> 轮询</b>，
    /// 而项目里其余输入早已走新版 Input System —— 混用会让"某个键没反应"要查两处
    /// （按键有没有配在 .inputactions 上、旧 API 在当前 Active Input Handling 下是否可用）。
    /// 现在按键配置、平台差异与一次性语义都收在 InputReader 里，本类只做转发。
    /// </para>
    ///
    /// <para>
    /// 注意本文件其余部分（<c>Input.mousePosition</c> / <c>GetMouseButtonDown</c>）仍是旧版 API：
    /// 它们能工作是因为 Active Input Handling 是 <b>Both</b>；改成 "Input System Package (New)"
    /// 会让那些调用在运行时抛异常。迁移鼠标输入是另一件事，不在本次范围内。
    /// </para>
    /// </summary>
    public int ConsumeSlotSwitchRequest()
    {
        // 判空与 InteractHeld / IsAlive 同一约定：句柄依赖的 InputReader 可能已被销毁
        // （工厂据此重建句柄），而本方法在 PlayerController.Update 里逐帧调用 ——
        // 不判空就是每帧一次 NRE。
        return _inputReader != null ? _inputReader.ConsumeSlotSwitchRequest() : -1;
    }

    /// <summary>底层 InputReader 是否仍然存在（宿主被销毁后需由工厂重建句柄）。</summary>
    public bool IsAlive => _inputReader != null;

    // 交互事件：代理到 InputReader 的 E 键事件
    public event System.Action OnInteract
    {
        add => _inputReader.EPressEvent += value;
        remove => _inputReader.EPressEvent -= value;
    }

    // 交互键按住状态：代理到 InputReader 的轮询属性
    public bool InteractHeld => _inputReader != null && _inputReader.InteractHeld;

    // 返回事件：代理到 InputReader 的 ESC 键事件
    public event System.Action OnEscape
    {
        add => _inputReader.EscapePressEvent += value;
        remove => _inputReader.EscapePressEvent -= value;
    }
}
