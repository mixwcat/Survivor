#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Mirror;
using UnityEngine;

/// <summary>
/// **临时**联机调试面板（IMGUI）—— 建房 / 加入 / 切场景 / 选角。
///
/// <para>
/// <b>它只是脚手架，不是正式 UI。</b>存在的理由是：验证联机骨架（连接、生成玩家、场景切换）
/// 需要一套能点的入口，而正式的房间面板要配 prefab、进 Addressables、接 <c>IUIService</c> ——
/// 在骨架还没验证之前做那些，等于把两件不确定的事叠在一起查。
/// 正式的 <c>NetworkRoomPanel</c> 落地后**整个文件连同它下面的引导代码一起删掉**。
/// </para>
///
/// <para>
/// 整个文件被 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> 包住：正式包（Release）里它不存在，
/// 连编译器都不会看到它。自举也放在这里，所以清理时不需要动 <c>GameBootstrap</c> 一行代码。
/// </para>
/// </summary>
public class DevNetworkPanel : MonoBehaviour
{
    private const ushort DefaultPort = 7777;

    private string _address = "127.0.0.1";
    private string _portText = DefaultPort.ToString();
    private Rect _window = new Rect(12f, 12f, 320f, 0f);

    /// <summary>
    /// 自举：在任何场景加载前创建一个常驻实例。
    /// 用 <c>BeforeSceneLoad</c> 是为了在 Menu 里也能看到它（那时 <c>[GameBootstrap]</c> 刚建好）。
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<DevNetworkPanel>() != null) return;

        GameObject go = new GameObject("[DevNetworkPanel]");
        DontDestroyOnLoad(go);
        go.AddComponent<DevNetworkPanel>();
    }

    private void OnGUI()
    {
        _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "联机调试（临时）");
    }

    private void DrawWindow(int id)
    {
        GUILayout.Label($"状态：{DescribeMode()}");
        GUILayout.Label($"就绪：{NetworkBootstrap.IsReady}" +
                        (NetworkBootstrap.IsReady ? "" : $"（{NetworkBootstrap.Manager?.name ?? "无 NetworkManager"}）"));

        if (NetworkServer.active && NetworkBootstrap.Manager != null)
            GUILayout.Label($"服务端连接数：{NetworkServer.connections.Count} / {NetworkBootstrap.Manager.maxConnections}");

        if (NetworkClient.active)
            GUILayout.Label($"本地玩家：{DescribeLocalPlayer()}");

        GUILayout.Space(6f);

        // ── 未联网：建房 / 加入 ──
        if (!NetworkBootstrap.IsActive)
        {
            if (!NetworkBootstrap.IsReady)
            {
                GUILayout.Label("联机未就绪 —— 检查 Console 里 NetworkBootstrap 的日志。");
                GUI.DragWindow();
                return;
            }

            if (GUILayout.Button("创建房间（Host）")) NetworkBootstrap.StartHost();

            GUILayout.BeginHorizontal();
            GUILayout.Label("地址", GUILayout.Width(36f));
            _address = GUILayout.TextField(_address);
            GUILayout.Label("端口", GUILayout.Width(36f));
            _portText = GUILayout.TextField(_portText, 5);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("加入房间"))
            {
                if (!ushort.TryParse(_portText, out ushort port)) port = DefaultPort;
                NetworkBootstrap.StartClient(_address, port);
            }

            GUI.DragWindow();
            return;
        }

        // ── 已联网 ──
        if (GUILayout.Button("离开房间")) NetworkBootstrap.Stop();

        // 角色：走 [Command] 上报到服务端（选角只改能力位，数值等下次生成玩家才生效）
        DrawCharacterPicker();

        GUILayout.Space(6f);

        // 场景切换只有服务端能做 —— 客户端点它只会得到一条"等待服务端指令"的日志
        if (NetworkServer.active)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("进入关卡")) SceneFlow.LoadPath(SceneFlow.StagePath);
            if (GUILayout.Button("返回大厅")) SceneFlow.LoadPath(SceneFlow.LobbyPath);
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("（切场景由服务端发起）");
        }

        GUI.DragWindow();
    }

    private void DrawCharacterPicker()
    {
        PlayerController local = PlayerManager.Service?.LocalPlayer;
        NetworkPlayerState state = local != null ? local.GetComponent<NetworkPlayerState>() : null;

        if (state == null)
        {
            GUILayout.Label("角色：（还没有本地玩家）");
            return;
        }

        PlayerSpawner spawner = PlayerSpawner.Current;
        if (spawner == null || spawner.AvailableRoles.Count == 0)
        {
            GUILayout.Label("角色：（场景没配可选角色）");
            return;
        }

        GUILayout.Label($"角色：{state.CharacterId}");

        GUILayout.BeginHorizontal();
        for (int i = 0; i < spawner.AvailableRoles.Count; i++)
        {
            CharacterDefinitionSO definition = spawner.AvailableRoles[i];
            if (definition == null) continue;

            if (GUILayout.Button(definition.id)) state.CmdSetCharacter(definition.id);
        }

        GUILayout.EndHorizontal();
    }

    private static string DescribeMode()
    {
        if (NetworkServer.active && NetworkClient.isConnected) return "Host（服务端 + 客户端）";
        if (NetworkServer.active) return "纯服务端";
        if (NetworkClient.isConnected) return "客户端";
        if (NetworkClient.active) return "客户端（连接中）";
        return "离线";
    }

    private static string DescribeLocalPlayer()
    {
        PlayerController local = PlayerManager.Service?.LocalPlayer;
        if (local == null) return "（无）";

        NetworkIdentity identity = local.GetComponent<NetworkIdentity>();
        return identity != null ? $"netId={identity.netId}" : "（非网络对象）";
    }
}
#endif
