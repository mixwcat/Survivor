using UnityEngine;

/// <summary>
/// 可交互物 —— 「靠近后能做什么」。
///
/// <para>
/// <b>每个方法都带 <see cref="IInteractor"/>：</b>能力位、归属、装备白名单都取决于**谁**在交互。
/// 旧接口的 <c>Interact()</c> 没有参数，实现只能去读全局 <c>LocalPlayer</c>，
/// 联机下"远程玩家站在塔边、本地玩家按 E"会判成同一个人的权限。
/// </para>
///
/// <para>
/// <b>不写 trigger 代码：</b>注册/注销由 <see cref="InteractionSensor"/> 统一负责，
/// 实现类只写"能不能交互 + 交互做什么 + 选中时怎么表现"。
/// </para>
///
/// <para>
/// <b>不要把区域型机制塞进来：</b><c>PortalController</c>（站圈倒计时）、自动拾取
/// 都不是按键交互 —— 硬塞会让本接口长出 Enter/Exit/Tick 三个成员。
/// </para>
/// </summary>
public interface IInteractable
{
    /// <summary>世界锚点（距离比较 / 提示挂点）。</summary>
    Transform Anchor { get; }

    /// <summary>重叠时优先级：数值大的先被选中（同优先级比距离）。</summary>
    int Priority { get; }

    /// <summary>
    /// 当前交互者能不能交互（能力位 / 配置是否齐 / 出行是否锁定）。
    /// 返回 false 的目标**不会被选中**，于是提示不显示、按键也不执行 ——
    /// "打开再拒绝"是更差的手感。
    /// </summary>
    bool CanInteract(IInteractor interactor, out string reason);

    /// <summary>提示内容（按键旁的动作文案）。只给事实，平台差异由提示层处理。</summary>
    InteractionPrompt GetPrompt(IInteractor interactor);

    /// <summary>执行交互（调用前交互者已确认 <see cref="CanInteract"/>）。</summary>
    void Interact(IInteractor interactor);

    /// <summary>成为当前交互目标（显示提示 / 高亮）。</summary>
    void OnSelected(IInteractor interactor);

    /// <summary>不再是当前交互目标（隐藏提示 / 取消高亮）。</summary>
    void OnDeselected(IInteractor interactor);
}
