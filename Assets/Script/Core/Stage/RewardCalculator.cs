using UnityEngine;

/// <summary>
/// 奖励计算 —— **纯函数**。
///
/// <para>
/// <b>为什么必须是纯函数：</b>同一个 <c>RunResult</c> 必须算出同一个奖励。
/// 一旦它去读时间、随机数或全局状态，"结算时显示 320、存进去 280"这类问题
/// 就只能在线上被发现，而且无法复现。
/// </para>
///
/// <para>
/// 数值来自 P0 定案：胜利 <c>clamp(100 + 击杀×2 + 耐久比例×100, 100, 500)</c>；
/// 失败固定 30（D-10：先保证结算确定性，不做表现修正）。
/// </para>
/// </summary>
public static class RewardCalculator
{
    /// <summary>胜利的基础奖励。</summary>
    public const int VictoryBase = 100;

    /// <summary>失败的固定奖励（D-10）。</summary>
    public const int DefeatReward = 30;

    /// <summary>每次击杀的额外奖励。</summary>
    public const int KillValue = 2;

    /// <summary>推车耐久满值时的额外奖励。</summary>
    public const int HealthValue = 100;

    /// <summary>胜利奖励下限。</summary>
    public const int MinVictoryReward = 100;

    /// <summary>胜利奖励上限。设上限是为了让"刷击杀"不能无限拉高收益。</summary>
    public const int MaxVictoryReward = 500;

    public static int Calculate(RunOutcome outcome, int totalKills, float cartHealthNormalized)
    {
        if (outcome != RunOutcome.Victory) return DefeatReward;

        int kills = Mathf.Max(0, totalKills);
        float health = Mathf.Clamp01(cartHealthNormalized);

        int reward = VictoryBase + kills * KillValue + Mathf.RoundToInt(health * HealthValue);

        return Mathf.Clamp(reward, MinVictoryReward, MaxVictoryReward);
    }
}
