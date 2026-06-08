using UnityEngine;

/// <summary>
/// 输入抽象层 - 统一处理 Windows（鼠标+键盘）和 Android（触屏+摇杆）输入
/// 设计目标：
/// 1. 游戏逻辑只依赖接口，不依赖具体平台实现
/// 2. 支持未来联机模式（本地输入 vs 网络输入）
/// 3. 提高代码可测试性（可 mock 输入）
/// </summary>
public interface IInputHandle
{
    /// <summary>
    /// 移动输入（轮询读取）
    /// Windows: 由 InputReader 从新版 Input System 读取 WASD/方向键
    /// Android: 从移动摇杆读取方向
    /// </summary>
    Vector2 MoveInput { get; }

    /// <summary>
    /// 攻击方向输入（原始数据）
    /// Android: 攻击摇杆的方向向量（归一化）
    /// Windows: 不使用此属性，通过 ScreenPointerPosition 计算
    /// </summary>
    Vector2 AttackDirectionInput { get; }

    /// <summary>
    /// 屏幕指针位置
    /// Windows: 鼠标屏幕坐标
    /// Android: 缓存的触摸点屏幕坐标
    /// 用途：武器瞄准、UI 交互
    /// </summary>
    Vector2 ScreenPointerPosition { get; }

    /// <summary>
    /// 尝试获取世界触控（用于塔放置等非 UI 交互）
    /// 自动过滤 UI 区域的触控（如摇杆）
    /// </summary>
    /// <param name="screenPos">触控/鼠标的屏幕坐标</param>
    /// <param name="isDown">是否按下（左键/触屏开始）</param>
    /// <param name="isUp">是否抬起（左键松开/触屏结束）</param>
    /// <returns>是否有有效的世界触控</returns>
    bool TryGetWorldPointer(out Vector2 screenPos, out bool isDown, out bool isUp);

    /// <summary>
    /// 取消输入（右键/ESC）
    /// Windows: 鼠标右键
    /// Android: 无（返回 false）
    /// 用途：取消塔放置等操作
    /// </summary>
    bool HasCancelInput { get; }

    /// <summary>
    /// 交互事件（E 键）
    /// 用途：与 NPC 交互、拾取物品等
    /// </summary>
    event System.Action OnInteract;

    /// <summary>
    /// 返回/暂停事件（ESC 键）
    /// 用途：打开/关闭暂停菜单、返回上一级 UI
    /// </summary>
    event System.Action OnEscape;
}
