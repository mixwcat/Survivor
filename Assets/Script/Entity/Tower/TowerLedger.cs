using UnityEngine;

/// <summary>
/// 塔的投入账本 —— 记录"谁建的、花了多少"，用于拆除时**退款回投入者**。
///
/// <para>
/// <b>为什么必须有它：</b>升级点按玩家独立（<c>UpgradePointWallet</c>），
/// 而塔是场景里共享的对象。没有账本就无法回答两个问题：这座塔是谁的？
/// 拆了该退给谁、退多少？多人时"我投了 3 点你投了 1 点"更是完全无从区分。
/// </para>
///
/// <para>
/// <b>它是运行时状态，不写回 SO</b>：SO 是常驻只读资产，写进去会跨 Play 会话与跨玩家污染。
/// </para>
///
/// <para>
/// <b>退款是幂等的</b>：拆除、切场景、异步失败都可能走到同一条退款路径，
/// 而重复退款会让点数凭空变多。<see cref="TryRefund"/> 用一次性标记挡住第二次。
/// </para>
/// </summary>
public class TowerLedger : MonoBehaviour
{
    /// <summary>
    /// 拆除时扣掉的技能点。**默认 0 = 全额退还**：拆除是"重新规划"，不是惩罚 ——
    /// 玩家投入的点数原样退回，可以换个位置重来。
    ///
    /// <para>
    /// <b>预留（元成长钩子）：</b>原设计（D-07）是扣 1 点，让"反复建了拆"不能零成本。
    /// 后续若做「用局外金币升级工程师 → 拆除不扣这个技能点」，正确的形态是：
    /// 未升级时这里返回 1、升级后返回 0。届时把本常量换成"读元成长状态"的属性即可，
    /// <see cref="Refund"/> 与所有调用方都不用改。
    /// </para>
    /// </summary>
    public const int RefundPenalty = 0;

    /// <summary>建造者。只有 owner 能升级与拆除（D-06），退款也回 owner。</summary>
    public PlayerController Owner { get; private set; }

    /// <summary>建造时花费的升级点。</summary>
    public int BuildCost { get; private set; }

    /// <summary>升级累计花费（只累加**成功**的升级）。</summary>
    public int UpgradeSpent { get; private set; }

    /// <summary>是否已退款（幂等标记）。</summary>
    public bool IsRefunded { get; private set; }

    /// <summary>总投入 = 建造 + 升级。</summary>
    public int TotalInvested => BuildCost + UpgradeSpent;

    /// <summary>拆除可退金额 = max(0, 总投入 − <see cref="RefundPenalty"/>)。</summary>
    public int Refund => Mathf.Max(0, TotalInvested - RefundPenalty);

    /// <summary>建塔时调用（由 <c>TowerPlacementController</c> 在放置确认后调用）。</summary>
    public void RecordBuild(PlayerController owner, int cost)
    {
        Owner = owner;
        BuildCost = Mathf.Max(0, cost);
        UpgradeSpent = 0;
        IsRefunded = false;
    }

    /// <summary>升级**成功后**调用。失败路径不要调 —— 那会让账本与实际投入对不上。</summary>
    public void RecordUpgrade(int cost)
    {
        if (cost <= 0) return;

        UpgradeSpent += cost;
    }

    /// <summary>
    /// 一次性退款。返回 false 表示已经退过（调用方不需要处理，但可据此跳过 UI 提示）。
    /// </summary>
    /// <param name="amount">实际退款金额。</param>
    public bool TryRefund(out int amount)
    {
        amount = 0;
        if (IsRefunded) return false;

        IsRefunded = true;
        amount = Refund;

        // Owner 可能已销毁（玩家死亡/退出），此时只记账不退钱 ——
        // 伪 null 判断由 Unity 的 == 重载完成
        if (Owner != null) Owner.UpgradePoints?.Refund(amount);

        return true;
    }
}
