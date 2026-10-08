/// <summary>一局的结局。</summary>
public enum RunOutcome
{
    Victory = 0,
    Defeat = 1,
}

/// <summary>
/// 一局结束时的**冻结快照** —— 结算 UI 与存档只消费它。
///
/// <para>
/// <b>为什么必须是快照：</b>结算发生在关卡末尾，此时玩家、推车、敌人随时可能被销毁或回收。
/// 如果结算面板直接去读 <c>CartController.Health</c> 之类的活对象，轻则读到 0，
/// 重则对着已销毁对象抛异常 —— 而且这类问题只在"打完立刻返回大厅"时出现。
/// 所以 <c>StageDirector.FinishRun</c> 在结束那一刻把需要的数字全部抄进这个结构。
/// </para>
///
/// <para>
/// <c>readonly struct</c>：结算路径上零分配，且天然不可被后续改动污染。
/// </para>
/// </summary>
public readonly struct RunResult
{
    /// <summary>胜利 / 失败。</summary>
    public readonly RunOutcome Outcome;

    /// <summary>关卡 id（<c>stage_01</c>）。</summary>
    public readonly string StageId;

    /// <summary>本局耗时（秒）。</summary>
    public readonly float ElapsedTime;

    /// <summary>全队击杀总数。</summary>
    public readonly int TotalKills;

    /// <summary>结束时推车的耐久比例（0..1）。</summary>
    public readonly float CartHealthNormalized;

    /// <summary>本次结算发放的金币（已按上下限裁剪）。</summary>
    public readonly int RewardCoins;

    public bool IsVictory => Outcome == RunOutcome.Victory;

    public RunResult(RunOutcome outcome, string stageId, float elapsedTime,
                     int totalKills, float cartHealthNormalized, int rewardCoins)
    {
        Outcome = outcome;
        StageId = stageId;
        ElapsedTime = elapsedTime;
        TotalKills = totalKills;
        CartHealthNormalized = cartHealthNormalized;
        RewardCoins = rewardCoins;
    }
}
