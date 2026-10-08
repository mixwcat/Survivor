using System;

/// <summary>
/// 升级点钱包 —— **按玩家实例化**（挂在 Player 上），是升级点的唯一持有者。
///
/// <para>
/// <b>为什么从 <c>ExperienceLevController</c> 里拆出来：</b>
/// 经验/等级回答「这个玩家练到几级了」，升级点回答「这个玩家还能买几次升级」——
/// 前者由击杀累积、后者由消费改变，两者的生命周期和读者都不同。
/// 混在一起时，"等级点"这个名字会让人以为它跟等级绑定，而实际上塔建造、塔升级、
/// 角色三选一都在花它。
/// </para>
///
/// <para>
/// <b>每个玩家一份</b>：联机时各人的点数是独立钱包，退款也要回到投入者的钱包
/// （见清单 D-04/D-06：击杀者获得、塔归建造者）。
/// </para>
/// </summary>
public interface IUpgradePointWallet
{
    /// <summary>当前余额。</summary>
    int Balance { get; }

    /// <summary>余额变化（参数：新余额）。UI 订阅它刷新，不要轮询。</summary>
    event Action<int> Changed;

    /// <summary>余额不足导致消费失败时触发（由 UI 订阅做提示）。</summary>
    event Action Insufficient;

    /// <summary>
    /// 尝试消费。返回 false 表示余额不足且**状态未被改动**；
    /// 金额 ≤ 0 视为成功（与旧 <c>CanUseLevelPoint</c> 的行为一致，避免免费项被误判为失败）。
    /// </summary>
    bool TrySpend(int amount);

    /// <summary>发放升级点（升级奖励、击杀归属）。</summary>
    void Grant(int amount);

    /// <summary>退还升级点（取消放置、拆除防御塔）。调用方负责保证只退一次。</summary>
    void Refund(int amount);
}
