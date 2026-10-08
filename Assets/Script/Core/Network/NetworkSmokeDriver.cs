#if UNITY_EDITOR
using System;
using System.Collections;
using Mirror;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 无头冒烟测试的**运行时驱动**（只在编辑器里存在，Release 包里连编译都进不去）。
///
/// <para>
/// <b>为什么驱动必须放在运行时程序集、而不是 Editor 脚本里：</b>
/// 本项目的 <c>EditorSettings.asset</c> 是 <c>m_EnterPlayModeOptionsEnabled: 1</c> +
/// <c>m_EnterPlayModeOptions: 0</c> —— 也就是**域重载照常发生**（日志里能看到
/// "Reloading assemblies for play mode" 与 <c>domain reloads=1</c>）。
/// 于是 Editor 脚本里挂在 <c>EditorApplication.update</c> 上的状态机，会在进入 Play 的瞬间
/// 连同静态字段一起被清掉：订阅没了、<c>_running</c> 变回 false，
/// 表现是"进了 Play 之后再无任何输出"。把状态机放进 Play 里的 <c>MonoBehaviour</c> 就没有这个问题。
/// </para>
///
/// <para>
/// 由 <c>Assets/Editor/NetworkSmokeTest.cs</c> 通过 <see cref="PendingKey"/> 这个
/// <c>SessionState</c> 开关（它能跨域重载存活）触发。
/// </para>
///
/// <para>
/// <b>它验证什么：</b>组合根能否装配 NetworkManager；在大厅里建房是否**不切场景**；
/// <b>切场景后玩家能否重建</b>（旧对象被场景卸载销毁、而 Mirror 不清 <c>conn.identity</c>）；
/// 来回切场景时玩家数量是否稳定。
/// <b>它验证不了：</b>真正的远程客户端、输入、相机、画面 —— 那些仍然必须由人 Play 确认。
/// </para>
/// </summary>
public class NetworkSmokeDriver : MonoBehaviour
{
    /// <summary>
    /// 跨域重载存活的开关：Editor 入口置位，本类在 Play 里读取后立刻清掉（只跑一次）。
    ///
    /// ⚠️ 必须是 <c>public</c>：入口在 <c>Assembly-CSharp-Editor</c>，而本类在 <c>Assembly-CSharp</c> ——
    /// <c>internal</c> 跨不了程序集，会得到 "does not contain a definition for 'PendingKey'"。
    /// </summary>
    public const string PendingKey = "Survivor.SmokeTest.Pending";

    private const string StagePath = "Assets/Scenes/Level0.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";

    /// <summary>每一步的等待上限（秒）。Addressables 首次加载在批处理里会慢一些。</summary>
    private const float StepTimeoutSeconds = 45f;

    /// <summary>玩家数量超量后允许持续的秒数（<c>Destroy</c> 帧末生效造成的正常中间态）。</summary>
    private const float OverCountGraceSeconds = 2f;

    private bool _failed;
    private float _overCountSince = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!SessionState.GetBool(PendingKey, false)) return;

        // 立刻清掉：即使中途崩了，也不会污染下一次普通的 Play
        SessionState.SetBool(PendingKey, false);

        GameObject go = new GameObject("[NetworkSmokeDriver]");
        DontDestroyOnLoad(go);
        go.AddComponent<NetworkSmokeDriver>();
        Debug.Log("[Smoke] 驱动已创建");
    }

    private void Start() => StartCoroutine(Run());

    private IEnumerator Run()
    {
        Log("开始：等待 NetworkBootstrap 就绪");

        yield return WaitUntil(() => NetworkBootstrap.IsReady || GameBootstrap.FailureReason != null, "NetworkBootstrap 就绪");

        if (!string.IsNullOrEmpty(GameBootstrap.FailureReason))
        {
            Fail($"组合根关键服务失败：{GameBootstrap.FailureReason}");
            yield break;
        }

        // ── 1. 离线玩家先出场（用来验证建房时它会让位）──
        Log("联机已就绪，等大厅生成离线玩家");
        yield return WaitUntil(() => CountPlayers() >= 1, "离线玩家生成");

        Log($"离线玩家数量={CountPlayers()} → 建房");
        NetworkBootstrap.StartHost();

        // ── 2. 连接建立 ──
        yield return WaitUntil(() => NetworkServer.active && NetworkClient.isConnected, "Host 连接建立");

        if (SceneManager.GetActiveScene().path != LobbyPath)
        {
            Fail($"建房竟然切了场景（当前={SceneManager.GetActiveScene().path}）—— " +
                 "说明 onlineScene 没留空或 offlineScene 配错");
            yield break;
        }

        // ── 3. 服务端生成玩家，且**只有一个**（离线那个必须已让位）──
        yield return WaitForPlayerCount(1, "建房后的玩家数量");
        if (_failed) yield break;

        // ── 4. 切到关卡：这一步验证"跨场景重建玩家" ──
        Log("切到关卡…");
        NetworkBootstrap.ServerChangeScene(StagePath);

        yield return WaitUntil(() => SceneManager.GetActiveScene().path == StagePath, "关卡场景加载完成");
        yield return WaitForPlayerCount(1, "关卡内的玩家数量");
        if (_failed) yield break;

        // ── 5. 切回大厅：再来一次，验证不会累积 ──
        Log("切回大厅…");
        NetworkBootstrap.ServerChangeScene(LobbyPath);

        yield return WaitUntil(() => SceneManager.GetActiveScene().path == LobbyPath, "大厅场景加载完成");
        yield return WaitForPlayerCount(1, "回到大厅后的玩家数量");
        if (_failed) yield break;

        if (Mathf.Abs(Time.timeScale - 1f) > 0.001f)
        {
            Fail($"回到大厅后 Time.timeScale={Time.timeScale}（应为 1）");
            yield break;
        }

        Pass();
    }

    // ── 等待与断言 ──

    private IEnumerator WaitUntil(Func<bool> condition, string what)
    {
        float deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;

        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Fail($"等待超时（{StepTimeoutSeconds:0} 秒）：{what}");
                yield break;
            }

            yield return null;
        }

        Log($"满足：{what}");
    }

    /// <summary>
    /// 等玩家数量**稳定**在期望值。
    ///
    /// <para>
    /// 超量不立刻判失败：<c>Destroy</c> 是帧末生效的，"离线玩家已请求销毁、网络玩家已生成"
    /// 这个中间态会短暂出现 <c>count == 2</c>。要持续超过 <see cref="OverCountGraceSeconds"/>
    /// 才认定为真的重复生成。
    /// </para>
    /// </summary>
    private IEnumerator WaitForPlayerCount(int expected, string what)
    {
        float deadline = Time.realtimeSinceStartup + StepTimeoutSeconds;

        while (true)
        {
            int count = CountPlayers();

            if (count == expected)
            {
                _overCountSince = -1f;
                Log($"满足：{what} = {count}");
                yield break;
            }

            if (count > expected)
            {
                if (_overCountSince < 0f) _overCountSince = Time.realtimeSinceStartup;

                if (Time.realtimeSinceStartup - _overCountSince > OverCountGraceSeconds)
                {
                    Fail($"{what} = {count}（应为 {expected}）—— 多半是离线玩家没让位，" +
                         "或 OnServerReady 被并发触发导致重复生成");
                    yield break;
                }
            }
            else
            {
                _overCountSince = -1f;   // 偏少只是还没生成完
            }

            if (Time.realtimeSinceStartup > deadline)
            {
                Fail($"等待超时（{StepTimeoutSeconds:0} 秒）：{what}（当前 {count}，期望 {expected}）");
                yield break;
            }

            yield return null;
        }
    }

    private static int CountPlayers()
    {
        IPlayerManager players = PlayerManager.Service;
        return players != null ? players.AllPlayers.Count : 0;
    }

    private static void Log(string message) => Debug.Log($"[Smoke] {message}");

    private void Fail(string reason)
    {
        _failed = true;
        Debug.LogError($"[Smoke] SMOKE_FAIL: {reason}");
        Finish(1);
    }

    private void Pass()
    {
        Log($"全部通过（玩家数量={CountPlayers()}，timeScale={Time.timeScale}）");
        Debug.Log("[Smoke] SMOKE_OK");
        Finish(0);
    }

    private static void Finish(int exitCode)
    {
        // 先退 Play 再退编辑器：直接 Exit 有可能留下一个半死不活的会话
        if (Application.isPlaying) EditorApplication.ExitPlaymode();

        EditorApplication.Exit(exitCode);
    }
}
#endif
