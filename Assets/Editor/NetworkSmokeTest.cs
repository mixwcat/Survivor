#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 无头冒烟测试的**编辑器入口**：开大厅场景、置位开关、进 Play。
///
/// <para>
/// 断言全部在 <see cref="NetworkSmokeDriver"/> 里 —— 它必须活在**运行时程序集**中，
/// 因为进入 Play 会触发域重载（本项目的 <c>EditorSettings</c> 是
/// <c>m_EnterPlayModeOptionsEnabled: 1</c> + <c>m_EnterPlayModeOptions: 0</c>，
/// 即域重载照常发生），挂在 <c>EditorApplication.update</c> 上的 Editor 状态机会被整个清掉。
/// 两者的交接靠 <c>SessionState</c>（它能跨域重载存活）。
/// </para>
///
/// <para>
/// 批处理运行（**编辑器必须关着**，且**不要加 <c>-quit</c>** —— 要在 Play 里活着才能跑断言）：
/// <code>
/// Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; \
///           -executeMethod NetworkSmokeTest.RunFromCommandLine -logFile &lt;log&gt;
/// </code>
/// 成功时日志里有 <c>SMOKE_OK</c> 且退出码 0；失败是 <c>SMOKE_FAIL: &lt;原因&gt;</c> 且退出码 1。
/// </para>
///
/// <para>
/// ⚠️ 批处理下 <c>-nographics</c> 会让 DOTween 的升级窗口无法创建（"No graphic device is available"），
/// 实测会把编辑器卡住。跑之前先确认 DOTween 已 Setup、且那个升级提示不会再弹
/// （见 <c>CLAUDE.md</c> 的 DOTween 条目）。
/// </para>
/// </summary>
public static class NetworkSmokeTest
{
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Tools/Run Network Smoke Test (enters Play mode)")]
    public static void RunFromMenu()
    {
        SessionState.SetBool(NetworkSmokeDriver.PendingKey, true);

        if (!Application.isPlaying)
        {
            EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }
    }

    public static void RunFromCommandLine()
    {
        // 从大厅开始：网络流程的起点（Lobby 既是 offlineScene 也是房间）
        EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);

        SessionState.SetBool(NetworkSmokeDriver.PendingKey, true);

        Debug.Log("[Smoke] 进入 Play 模式…");
        EditorApplication.EnterPlaymode();
    }
}
#endif
