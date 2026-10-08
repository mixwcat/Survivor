#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Mirror;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 幂等的联机接线脚本（Editor）。
///
/// 做三件事：
/// <list type="number">
/// <item>给 Player prefab 的**根节点**挂 <c>NetworkIdentity</c> / <c>NetworkRigidbodyReliable2D</c> / <c>NetworkPlayerState</c>；</item>
/// <item>创建或更新 <c>Assets/Game/Prefabs/Net/NetworkManager.prefab</c>（NetworkManager + KcpTransport，配好场景与 spawnPrefabs）；</item>
/// <item>触发 Mirror 的 <c>assetId</c> 分配 —— **脚本改 prefab 不会自动分配**，不补这一步 Build 出来的客户端
/// 会在收到 SpawnMessage 时报 "Failed to spawn server object"。</item>
/// </list>
///
/// <para>
/// 手动运行：菜单 <c>Tools ▸ Setup Network (Mirror)</c><br/>
/// 批处理运行：<c>-executeMethod NetworkSetup.SetupFromCommandLine</c>
/// </para>
///
/// <para>
/// ⚠️ <b>必须在关了 Unity 编辑器的情况下跑批处理</b>（工程被占用时批处理会直接崩）。
/// </para>
/// </summary>
public static class NetworkSetup
{
    private const string PlayerPrefabPath = "Assets/Game/Prefabs/Common/Player.prefab";
    private const string NetDir = "Assets/Game/Prefabs/Net";
    private const string NetworkManagerPrefabPath = NetDir + "/NetworkManager.prefab";

    /// <summary>大厅既是房间也是 <c>offlineScene</c>；<c>onlineScene</c> 留空（见 Docs/MirrorPlan.md §1.3）。</summary>
    private const string OfflineScenePath = "Assets/Scenes/Lobby.unity";

    private const int MaxConnections = 4;
    private const ushort DefaultPort = 7777;

    [MenuItem("Tools/Setup Network (Mirror)")]
    public static void SetupFromMenu()
    {
        bool ok = Setup();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(ok ? "[NetworkSetup] CLI_OK (menu)" : "[NetworkSetup] CLI_FAIL (menu)");
    }

    public static void SetupFromCommandLine()
    {
        bool ok = false;
        try
        {
            ok = Setup();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[NetworkSetup] 异常: {e}");
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(ok ? "CLI_OK" : "CLI_FAIL");
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }
    }

    private static bool Setup()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"[NetworkSetup] 找不到玩家 prefab：{PlayerPrefabPath}");
            return false;
        }

        if (!SetupPlayerPrefab()) return false;
        if (!SetupNetworkManagerPrefab()) return false;

        // assetId 必须在 prefab 落盘之后、重新加载资产再触发（见类注释第 3 条）
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EnsureAssetIds();
        return true;
    }

    // ── Player prefab ──

    private static bool SetupPlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[NetworkSetup] 无法加载 prefab 内容：{PlayerPrefabPath}");
            return false;
        }

        try
        {
            // 根节点在资产里是**未激活**的（装配方注入 playerConfig 之后才激活）——
            // 这里刻意不改它。Mirror 客户端在 ApplySpawnPayload 里会自己 SetActive(true)
            //（NetworkClient.cs:1151），服务端那侧由 PlayerSpawner 显式激活。
            EnsureComponent<NetworkIdentity>(root);

            if (root.GetComponent<Rigidbody2D>() == null)
            {
                Debug.LogError($"[NetworkSetup] 玩家 prefab 根节点没有 Rigidbody2D，" +
                               $"{nameof(NetworkRigidbodyReliable2D)} 会报错。");
                return false;
            }

            // AddComponent<NetworkTransform/NetworkRigidbody> 会触发 Reset()，此时 target 为空而抛 NRE ——
            // 那是噪声（组件仍会加上），随后显式设置即可
            var body = EnsureComponent<NetworkRigidbodyReliable2D>(root);
            body.target = root.transform;
            body.syncDirection = SyncDirection.ClientToServer;   // 玩家移动是**客户端权威**
            body.syncInterval = 0f;
            body.onlySyncOnChange = true;

            EnsureComponent<NetworkPlayerState>(root);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[NetworkSetup] 玩家 prefab 已接线：" +
                      "NetworkIdentity + NetworkRigidbodyReliable2D(ClientToServer) + NetworkPlayerState。");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── NetworkManager prefab ──

    private static bool SetupNetworkManagerPrefab()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

        Directory.CreateDirectory(NetDir);

        bool existed = File.Exists(NetworkManagerPrefabPath);
        GameObject root = existed
            ? PrefabUtility.LoadPrefabContents(NetworkManagerPrefabPath)
            : new GameObject("NetworkManager");

        if (root == null)
        {
            Debug.LogError($"[NetworkSetup] 无法加载 prefab 内容：{NetworkManagerPrefabPath}");
            return false;
        }

        try
        {
            // ⚠️ Transport 必须与 NetworkManager 在**同一个 GameObject** 上：
            // NetworkManager.InitializeSingleton() 用 TryGetComponent<Transport>() 自动接线
            //（NetworkManager.cs:729-734），分开挂就会报 "No Transport on Network Manager"。
            var kcp = EnsureComponent<kcp2k.KcpTransport>(root);
            kcp.port = DefaultPort;

            var nm = EnsureComponent<SurvivorNetworkManager>(root);
            nm.transport = kcp;
            nm.dontDestroyOnLoad = true;      // 组合根创建的实例要活过所有场景切换
            nm.maxConnections = MaxConnections;

            // 场景分工（Docs/MirrorPlan.md §1.3）：
            //   offlineScene = Lobby（大厅本身就是房间）
            //   onlineScene 留空 ⇒ 建房时**不切场景**，直接 FinishStartHost
            //   ⚠️ 路径口径：NetworkManager 内部有一处只认路径的比较（:608,1297）
            nm.offlineScene = OfflineScenePath;
            nm.onlineScene = "";

            // 玩家生成权归 PlayerSpawner（它要在激活前注入职业配置），
            // 所以不用 Mirror 的 playerPrefab / autoCreatePlayer
            nm.autoCreatePlayer = false;
            nm.playerPrefab = null;

            // spawnPrefabs 是 Mirror 解析 assetId → prefab 的**唯一**来源。
            // 现在只登记玩家；敌人/塔/投射物在各自的阶段加进来。
            var spawnables = new List<GameObject>();
            if (playerPrefab != null) spawnables.Add(playerPrefab);
            nm.spawnPrefabs = spawnables;

            PrefabUtility.SaveAsPrefabAsset(root, NetworkManagerPrefabPath);
            Debug.Log($"[NetworkSetup] 联机 prefab 已就绪：{NetworkManagerPrefabPath}" +
                      $"（offlineScene={nm.offlineScene}，onlineScene=空，spawnPrefabs={spawnables.Count}）");
            return true;
        }
        finally
        {
            // 新建的分支用的是普通 GameObject，必须销毁；加载的分支要 Unload
            if (existed) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    // ── assetId ──

    /// <summary>
    /// 触发并校验 Mirror 的 <c>assetId</c> 分配。
    ///
    /// <para>
    /// <c>assetId</c> 是 Mirror 在编辑器里按 prefab 的 GUID 派生出来的（<c>NetworkIdentity.AssetGuidToUint</c>），
    /// 但它**只在编辑器访问 getter 时才写入序列化字段**。脚本化改 prefab 不会走那条路径，
    /// 于是落盘的 <c>_assetId</c> 还是 0 —— 编辑器里一切正常，Build 出来的客户端却会在
    /// 收到 SpawnMessage 时报 "Failed to spawn server object"。
    /// </para>
    /// </summary>
    private static void EnsureAssetIds()
    {
        string[] paths = { PlayerPrefabPath, NetworkManagerPrefabPath };

        for (int i = 0; i < paths.Length; i++)
        {
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            NetworkIdentity identity = go != null ? go.GetComponent<NetworkIdentity>() : null;
            if (identity == null) continue;

            // 读一次 getter 即触发分配
            uint assetId = identity.assetId;

            if (assetId == 0)
            {
                Debug.LogError($"[NetworkSetup] {paths[i]} 的 assetId 仍是 0 —— " +
                               "Build 客户端会无法生成该对象。");
                continue;
            }

            EditorUtility.SetDirty(identity);
            Debug.Log($"[NetworkSetup] {Path.GetFileName(paths[i])} assetId = {assetId}");
        }

        AssetDatabase.SaveAssets();
    }

    /// <summary>取组件，没有就加（幂等）。</summary>
    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }
}
#endif
