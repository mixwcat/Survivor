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
    /// 世界空间的瞄准方向（已归一化）。
    ///
    /// <para>
    /// Windows：鼠标屏幕坐标 → 世界坐标，减去 <paramref name="worldOrigin"/> 得到方向。
    /// Android：攻击摇杆方向（忽略 <paramref name="worldOrigin"/>）。
    /// </para>
    ///
    /// <para>
    /// <b>为什么把这件事放在输入层：</b>「屏幕坐标 → 世界方向」需要相机，属于输入适配。
    /// 之前它散在武器代码里（配合 <c>#if UNITY_STANDALONE_WIN / #elif UNITY_ANDROID</c>），
    /// 后果有两条：① 平台判断出现在两处（本工厂与武器各一份），必须同步维护；
    /// ② Build Target 既非 Windows 也非 Android 时两个分支都不成立，
    /// 方向恒为零向量、枪不跟鼠标转，且**没有任何报错**。
    /// </para>
    /// </summary>
    /// <param name="worldOrigin">瞄准原点（通常传武器自身位置）。</param>
    /// <param name="worldDirection">归一化后的世界方向；返回 false 时为零向量。</param>
    /// <returns>本帧是否有有效瞄准；false 表示调用方应保持上一次朝向。</returns>
    bool TryGetAimDirection(Vector2 worldOrigin, out Vector2 worldDirection);

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
    /// 交互键是否**正被按住**（轮询）。
    ///
    /// <para>
    /// <see cref="OnInteract"/> 只在按下的那一帧发一次，表达不了"还按着" ——
    /// "按住 E 若干秒"这类持续交互（如修车）必须能轮询到按键状态。
    /// </para>
    ///
    /// <para>
    /// <b>触屏没有"按住"的等价物</b>：Android 返回 true（靠近并点击提示即视为持续按住，
    /// 离开范围会中断）。要让触屏也支持"松开即中断"，得由提示 UI 上报触摸按住状态。
    /// </para>
    /// </summary>
    bool InteractHeld { get; }

    /// <summary>
    /// 取出并清空「切换到武器槽」的请求（-1 = 本帧没有请求）。
    ///
    /// <para>
    /// <b>为什么是「消费」而不是属性：</b>切换是一次性动作，读两次不该触发两次。
    /// 属性形式会让"谁读到了这一帧的请求"取决于调用顺序 —— 而调用顺序在 Unity 里
    /// 由脚本执行顺序决定，很难看出来。
    /// </para>
    ///
    /// <para>
    /// Windows：数字键 <c>1/2/3</c>（新版 Input System 的 <c>Alpha1/2/3</c> action，
    /// 由 <c>InputReader</c> 在回调里记下、本方法取走）。
    /// Android：HUD 槽位按钮**直接调** <c>PlayerWeaponController.SwitchToSlot</c>
    /// （按钮是 UI 事件，不是输入设备），所以这里恒返回 -1。
    /// </para>
    ///
    /// <para>
    /// 返回值是**已装备槽序号**（0 = 第 1 把带出门的武器），不是候选武器下标 ——
    /// 越界与否由 <c>SwitchToSlot</c> 判定（例如现在没有角色能带 3 把，按键 3 会是空操作）。
    /// </para>
    /// </summary>
    int ConsumeSlotSwitchRequest();

    /// <summary>
    /// 返回/暂停事件（ESC 键）
    /// 用途：打开/关闭暂停菜单、返回上一级 UI
    /// </summary>
    event System.Action OnEscape;

    /// <summary>
    /// 句柄依赖的 Unity 对象是否仍然有效。
    /// <para>
    /// 场景切换会销毁句柄依赖的对象（Android 的 Joystick、PC 的 InputReader 宿主），
    /// 此时句柄虽非 null 但会**静默返回零输入**。工厂在复用缓存前必须校验此属性，
    /// 否则重开关卡后玩家会莫名其妙无法移动。
    /// </para>
    /// </summary>
    bool IsAlive { get; }
}
