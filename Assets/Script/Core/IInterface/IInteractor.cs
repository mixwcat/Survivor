using System;
using UnityEngine;

/// <summary>
/// 玩家侧「交互者」—— 回答三个问题：附近有什么可交互、当前选中哪个、按下交互键时做什么。
///
/// <para>
/// <b>为什么候选列表挂在玩家身上而不是做成全局服务：</b>
/// 「附近有什么」是**按玩家**的状态（联机时两个玩家站在同一座塔边，各自的目标可以不同），
/// 服务化会引入"谁在交互"的第二条真相。与升级选项、武器槽挂在 Player 上同理。
/// </para>
///
/// <para>
/// <b>注册方向：</b>由 <see cref="InteractionSensor"/> 在触发器进出时调用
/// <see cref="Register"/> / <see cref="Unregister"/>，交互物自己不写 trigger 代码。
/// </para>
///
/// <para>
/// <b>领域校验一律用 <see cref="Player"/>：</b>交互物不要读
/// <c>PlayerManager.Service.LocalPlayer</c> —— 那会让"谁的能力位"在联机下判错，
/// 而且把"本地"这件事写死在领域脚本里。
/// </para>
/// </summary>
public interface IInteractor
{
    /// <summary>交互者位置（距离比较用，通常是玩家根节点）。</summary>
    Transform Origin { get; }

    /// <summary>交互者对应的玩家。领域校验（能力位 / 塔归属）问它。</summary>
    PlayerController Player { get; }

    /// <summary>
    /// 是否本地玩家。世界空间提示与高亮**只对本地玩家显示** ——
    /// 远程玩家的交互者同样会选中目标，但不该点亮本地屏幕。
    /// </summary>
    bool IsLocal { get; }

    /// <summary>当前交互目标（没有则为 null）。</summary>
    IInteractable Current { get; }

    /// <summary>当前目标变化（参数可能为 null）。提示层订阅它。</summary>
    event Action<IInteractable> CurrentChanged;

    /// <summary>注册一个进入范围的可交互物（重复注册是空操作）。</summary>
    void Register(IInteractable target);

    /// <summary>注销一个离开范围（或被销毁）的可交互物（未注册过是空操作）。</summary>
    void Unregister(IInteractable target);

    /// <summary>
    /// 执行一次交互（PC 的 E 键、移动端点击提示都走这里）。
    /// 返回是否真的执行了 —— 目标不可用、没有目标、非本地玩家都返回 false。
    /// </summary>
    bool TryInteract();

    /// <summary>
    /// 交互键是否**正被按住**（轮询）。
    ///
    /// <para>
    /// <see cref="TryInteract"/> 是"按下"这一次性动作，表达不了"还按着" ——
    /// "按住若干秒"的持续交互（如修车）由交互物每帧读它推进进度，
    /// 松开或离开范围即中断。
    /// </para>
    ///
    /// <para>
    /// 远程玩家（网络输入源）恒为 false：他们的按住状态要由网络同步，本地读不到。
    /// </para>
    /// </summary>
    bool IsInteractHeld { get; }
}
