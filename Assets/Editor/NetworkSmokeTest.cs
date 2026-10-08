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
/// <b>三种角色</b>，各自一个入口：
/// </para>
/// <list type="bullet">
/// <item><see cref="RunFromCommandLine"/>（<c>host</c>）—— 单进程自测，覆盖服务端那一半。
/// 命令：<c>Tools\run-network-smoke.ps1</c></item>
/// <item><see cref="RunServerFromCommandLine"/> / <see cref="RunClientFromCommandLine"/> —— 双进程测试的两半，
/// 命令：<c>Tools\run-network-2p.ps1</c>（它会同时拉起两个工程实例）。</item>
/// </list>
///
/// <para>
/// 批处理运行时（**编辑器必须关着**，且**不要加 <c>-quit</c>** —— 要在 Play 里活着才能跑断言）：
/// <code>
/// Unity.exe -batchmode -nographics -projectPath &lt;proj&gt; \
///           -executeMethod NetworkSmokeTest.RunFromCommandLine -logFile &lt;log&gt;
/// </code>
/// 成功时日志里有 <c>SMOKE_OK</c> 且退出码 0；失败是 <c>SMOKE_FAIL: &lt;原因&gt;</c> 且退出码 1。
/// </para>
/// </summary>
public static class NetworkSmokeTest
{
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Tools/Run Network Smoke Test (enters Play mode)")]
    public static void RunFromMenu() => Start("host", true);

    public static void RunFromCommandLine() => Start("host", false);

    /// <summary>
    /// 双进程测试的**服务端**：建房、等真客户端接入、切到关卡，然后常驻等被编排脚本杀掉。
    /// 判定看日志里的 <c>SMOKE_OK</c>，不看退出码。
    /// </summary>
    public static void RunServerFromCommandLine() => Start("server", false);

    /// <summary>
    /// 双进程测试的**客户端**：连接 <c>127.0.0.1:7777</c>，断言客户端侧的一切
    /// （看到两端玩家、认出自己的角色、跟着切场景、推车在动、时钟在走、敌人有 netId）。
    /// </summary>
    public static void RunClientFromCommandLine() => Start("client", false);

    private static void Start(string role, bool unusedEnterPlaymode)
    {
        // 从大厅开始：网络流程的起点（Lobby 既是 offlineScene 也是房间）
        EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);

        SessionState.SetString(NetworkSmokeDriver.RoleKey, role);
        SessionState.SetBool(NetworkSmokeDriver.PendingKey, true);

        Debug.Log($"[Smoke] 角色={role}，进入 Play 模式…");

        if (!Application.isPlaying) EditorApplication.EnterPlaymode();
    }
}
#endif
