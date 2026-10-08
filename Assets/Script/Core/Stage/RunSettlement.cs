using UnityEngine;

/// <summary>
/// 结算 —— 把「这一局结束了」翻译成「档案变了、界面显示了」。
///
/// <para>
/// <b>顺序是固定的</b>：冻结结果 → 计算 → 更新档案 → **保存** → 展示。
/// 先保存再展示，是因为"显示拿到 320 但重启后没有"比"晚一秒看到结果"严重得多。
/// </para>
///
/// <para>
/// <b>只结算一次</b>：<c>StageDirector.FinishRun</c> 已经幂等，这里是第二道 ——
/// 结算会改档案并落盘，重复执行的后果是金币翻倍，值得多一层保护。
/// </para>
///
/// <para>
/// <b>保存失败要说出来</b>：内存态已经生效（金币加了、哨站推进了），
/// 只是没落盘。静默失败会让玩家在重启后发现进度丢失却找不到原因；
/// 这里把结果告诉面板，由它提示并允许重试。
/// </para>
/// </summary>
public class RunSettlement : MonoBehaviour
{
    [Header("关卡对象")]
    [Tooltip("关卡导演（本局结束事件的来源）")]
    public StageDirector Director;

    private bool _settled;

    private void Start()
    {
        if (Director == null)
        {
            Debug.LogError("[RunSettlement] 没有配置 StageDirector，本局结束时不会结算。");
            return;
        }

        Director.RunFinished += OnRunFinished;
    }

    private void OnDestroy()
    {
        if (Director != null) Director.RunFinished -= OnRunFinished;
    }

    private void OnRunFinished(RunResult result)
    {
        if (_settled) return;
        _settled = true;

        IPlayerProfileService profile = PlayerProfileService.Service;

        int reward = result.RewardCoins;
        bool saved = false;

        if (profile == null)
        {
            Debug.LogError("[RunSettlement] IPlayerProfileService 未注册，本局奖励无法入账。");
        }
        else
        {
            profile.GrantCoins(reward);

            // 只有胜利推进哨站：失败不推进，否则"打不过就重开"能刷进度
            if (result.IsVictory)
                profile.SetCurrentOutpost(profile.Profile.currentOutpost + 1);

            saved = profile.Save();
            if (!saved)
                Debug.LogError("[RunSettlement] 档案保存失败 —— 奖励已在内存生效，重启后会丢失。");
        }

        _ = ShowResultAsync(result, saved);
    }

    private async System.Threading.Tasks.Task ShowResultAsync(RunResult result, bool saved)
    {
        IUIService ui = UIService.Service;
        if (ui == null) return;

        await ui.ShowPanelAsync<RunResultPanel>(panel => panel.SetResult(result, saved));

        if (this == null) return;

        Debug.Log($"[RunSettlement] 本局结束：{result.Outcome}，击杀 {result.TotalKills}，" +
                  $"耐久 {result.CartHealthNormalized:P0}，奖励 {result.RewardCoins}，保存={(saved ? "成功" : "失败")}");
    }

    /// <summary>
    /// 离场前的保存补试。**所有结算出口（返回大厅 / 返回菜单 / ESC）都必须走它。**
    ///
    /// <para>
    /// 只在"重开"按钮上重试的话，从其它出口离开的玩家会静默丢掉本局奖励 ——
    /// 内存态已经生效（金币加了、哨站推进了），只是没落盘，
    /// 而玩家要等到下次启动才发现，且没有任何线索指向"那一刻保存失败过"。
    /// </para>
    ///
    /// <para>
    /// 补试再次失败时仍然放行离场：把玩家锁在结算面板上并不能让磁盘变得可写，
    /// 只会把"进度可能丢失"升级成"游戏卡住"。失败会留一条红错。
    /// </para>
    /// </summary>
    /// <param name="saved">结算时的保存结果。</param>
    /// <returns>最终是否已落盘。</returns>
    public static bool RetrySaveIfNeeded(bool saved)
    {
        if (saved) return true;

        IPlayerProfileService profile = PlayerProfileService.Service;
        if (profile == null)
        {
            Debug.LogError("[RunSettlement] IPlayerProfileService 未注册，本局奖励无法落盘。");
            return false;
        }

        if (profile.Save()) return true;

        Debug.LogError("[RunSettlement] 离场前重试保存仍然失败 —— 本局奖励只存在于内存，重启后会丢失。");
        return false;
    }
}
