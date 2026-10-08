using UnityEngine;

/// <summary>
/// 菜单场景入口。全局服务由 GameBootstrap 统一初始化，
/// 这里只负责在服务就绪后展示菜单面板。
///
/// <para>
/// <b>关键服务失败时不再往下走</b>：继续显示菜单会让玩家点进大厅，
/// 然后在某个读不到档案的地方炸掉 —— 现场离原因太远。
/// 这里改为直接把失败原因摆到界面上（UI 可用时）并留红错。
/// </para>
/// </summary>
public class Main : MonoBehaviour
{
    async void Start()
    {
        if (!await GameBootstrap.TryWaitReadyAsync())
        {
            ShowStartupFailure();
            return;
        }

        IUIService ui = UIService.Service;
        if (ui == null)
        {
            Debug.LogError("[Main] IUIService 未就绪，无法显示菜单面板。");
            return;
        }

        await ui.ShowPanelAsync<MenuPanel>();
    }

    /// <summary>
    /// 把"启动失败"变成可见状态。UI 服务本身就是失败项时无从显示，只能留红错 ——
    /// 但两种情况下日志都能指出**是哪个服务**失败了。
    /// </summary>
    private static void ShowStartupFailure()
    {
        string reason = GameBootstrap.FailureReason ?? "未知原因";
        Debug.LogError($"[Main] 启动失败：{reason}");

        IUIService ui = UIService.Service;
        if (ui == null) return;

        _ = ui.ShowPanelAsync<TipsPanel>(panel => panel.SetMessage($"启动失败：{reason}"));
    }
}
