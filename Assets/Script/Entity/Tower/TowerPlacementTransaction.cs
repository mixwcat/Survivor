using UnityEngine;

/// <summary>
/// 塔放置事务 —— 一次「扣点 → 拖动 → 确认/取消」的**一次性**状态与付款人记录。
///
/// <para>
/// <b>为什么必须有它：</b>扣点发生在**异步加载之前**，而终结这件事的路径有四条
/// （确认、取消、加载失败、切场景销毁）。旧实现把退款写在 <c>CancelPlacement()</c> 里，
/// 于是加载失败与切场景两条路径**不退款**（点扣了、塔没出来），
/// 而取消按钮与销毁兜底又可能各退一次（点数凭空变多）。
/// 把状态收进一个对象、退款只认第一次状态迁移，才能保证
/// 「每一笔成功扣款，最终要么出现一座塔、要么全额退回一次」。
/// </para>
///
/// <para>
/// <b>它记下的是付款人本身，不是"当时的 LocalPlayer"：</b>退款发生在异步加载之后，
/// 那时 <c>LocalPlayer</c> 可能已经换人甚至被销毁 —— 重新读一次会把点退给错误的对象，
/// 或者静默丢掉（而玩家只会看到点数少了）。
/// </para>
///
/// <para>
/// 它不是 MonoBehaviour：状态必须比幽灵活得久一点（幽灵被销毁的路径正是要退款的那条）。
/// </para>
/// </summary>
public sealed class TowerPlacementTransaction
{
    /// <summary>事务状态。单向迁移，只能终结一次。</summary>
    public enum TransactionState
    {
        /// <summary>塔 prefab 正在异步加载。</summary>
        Loading = 0,

        /// <summary>加载完成，等待玩家确认或取消。</summary>
        Ready = 1,

        /// <summary>已确认放置（终态）。</summary>
        Committed = 2,

        /// <summary>已取消 / 失败（终态，且已退款）。</summary>
        Cancelled = 3,
    }

    /// <summary>扣款方。退款只会回到它手上。</summary>
    public PlayerController Payer { get; }

    /// <summary>本次扣掉的升级点。</summary>
    public int Cost { get; }

    public TransactionState State { get; private set; } = TransactionState.Loading;

    /// <summary>是否已终结（确认或取消）。终结后不再接受任何状态迁移与退款。</summary>
    public bool IsFinalized => State == TransactionState.Committed || State == TransactionState.Cancelled;

    public TowerPlacementTransaction(PlayerController payer, int cost)
    {
        Payer = payer;
        Cost = Mathf.Max(0, cost);
    }

    /// <summary>加载完成 → 可以确认。非 <see cref="TransactionState.Loading"/> 时返回 false。</summary>
    public bool TryMarkReady()
    {
        if (State != TransactionState.Loading) return false;

        State = TransactionState.Ready;
        return true;
    }

    /// <summary>
    /// 确认放置。**在实例化之前调用** ——
    /// 同帧双击时第二次会因为状态已终结而被拒绝，从而只生成一座塔。
    /// </summary>
    public bool TryCommit()
    {
        if (IsFinalized) return false;

        State = TransactionState.Committed;
        return true;
    }

    /// <summary>
    /// 取消并退款。**幂等**：重复调用是空操作。
    /// 确认、取消、加载失败、场景销毁四条路径都可以放心调它，谁先到谁生效。
    /// </summary>
    public bool TryCancel()
    {
        if (IsFinalized) return false;

        State = TransactionState.Cancelled;

        // Payer 可能已销毁（玩家阵亡 / 退出关卡）：Unity 的 == 重载会判为伪 null，
        // 此时只记账不退钱 —— 钱包也随玩家一起没了
        if (Cost > 0 && Payer != null)
            Payer.UpgradePoints?.Refund(Cost);

        return true;
    }
}
