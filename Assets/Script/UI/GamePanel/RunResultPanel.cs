using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算面板 —— 消费冻结的 <see cref="RunResult"/>，不读取任何场景对象。
///
/// <para>
/// <b>为什么只消费快照：</b>结算发生时玩家、推车、敌人随时可能被销毁或回收。
/// 面板若直接读 <c>CartController.HealthNormalized</c> 之类的活对象，
/// 轻则读到 0，重则对着已销毁对象抛异常 —— 而且只在"打完立刻返回大厅"时出现。
/// </para>
///
/// <para>
/// 本局的唯一结束面板：旧的 <c>DeadPanel</c> 已随「胜负只有一个权威入口」的整改删除
/// （两个结束入口并存时，单人局会同时弹出死亡面板与结算面板）。
/// prefab 是从 <c>DeadPanel.prefab</c> 复制来的，所以字段名沿用
/// <c>txtSurvivalTime</c> / <c>btnMenu</c> / <c>btnRestart</c>（改名会静默丢引用）。
/// </para>
/// </summary>
public class RunResultPanel : BasePanel
{
    /// <summary>
    /// 结算面板也是模态的：本局已经结束，背后的关卡不该继续推进
    /// （否则敌人还在刷、还在打一辆已经"结算完"的车）。
    /// </summary>
    public override bool WantsPause => true;

    /// <summary>字段名沿用 DeadPanel 的（prefab 按字段名反序列化，改名会静默丢引用）。</summary>
    public TMPro.TextMeshProUGUI txtSurvivalTime;

    public Button btnMenu;
    public Button btnRestart;

    private RunResult _result;
    private bool _saved;

    /// <summary>注入本局结果与保存状态（Init 之前调用）。</summary>
    public void SetResult(RunResult result, bool saved)
    {
        _result = result;
        _saved = saved;
    }

    public override void Init()
    {
        if (txtSurvivalTime != null)
        {
            string title = _result.IsVictory ? "运送完成" : "运送失败";
            string saveHint = _saved ? "" : "\n⚠ 进度未保存（离开时会再试一次）";

            txtSurvivalTime.text =
                $"{title}\n" +
                $"击杀 {_result.TotalKills}\n" +
                $"推车耐久 {_result.CartHealthNormalized:P0}\n" +
                $"奖励 {_result.RewardCoins} 金币" +
                saveHint;
        }

        btnRestart.onClick.AddListener(() => Leave(toLobby: true));
        btnMenu.onClick.AddListener(() => Leave(toLobby: false));
    }

    public override void EscLogic()
    {
        Leave(toLobby: true);
    }

    /// <summary>
    /// **唯一**的离场方法：先补一次保存，再切场景。
    ///
    /// <para>
    /// 结算时保存失败过（内存态已生效、只是没落盘）就在这里重试。
    /// 三个出口（返回大厅 / 返回菜单 / ESC）都必须走它 ——
    /// 旧实现只有"重开"按钮重试，从菜单或 ESC 离开的玩家会静默丢掉本局奖励，
    /// 而且重启后才发现，完全无从归因。
    /// </para>
    /// </summary>
    private void Leave(bool toLobby)
    {
        _saved = RunSettlement.RetrySaveIfNeeded(_saved);

        // 关卡 HUD 必须在这里收掉：面板实例挂在 DontDestroyOnLoad 的画布上，
        // 不隐藏的话它会跟着玩家进大厅/菜单继续显示上一局的等级、时间与摇杆
        UIService.Service?.HidePanel<GamePanel>();
        UIService.Service?.HidePanel<RunResultPanel>();

        if (toLobby) SceneFlow.LoadLobby();
        else SceneFlow.LoadMenu();
    }
}
