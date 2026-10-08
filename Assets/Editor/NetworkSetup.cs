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

    /// <summary>
    /// 推车 prefab。
    ///
    /// <para>
    /// ⚠️ 它**刻意没有** <c>NetworkIdentity</c>：推车是摆在场景里的对象，挂上 NetworkIdentity
    /// 就会变成 Mirror 的"场景对象"，进 Play 时被 <c>NetworkScenePostProcess</c> 强制禁用、
    /// 只能靠 <c>NetworkServer.SpawnObjects()</c> 激活 —— 而**单机模式没有服务端**，推车永远不会被激活。
    /// 它的状态走 <see cref="CartNetworkSync"/> 的显式消息（见 <c>CartStateMessage</c>）。
    /// </para>
    /// </summary>
    private const string CartPrefabPath = "Assets/Game/Prefabs/Cart/Cart.prefab";

    /// <summary>
    /// 会被 <c>NetworkServer.Spawn</c> 的 prefab 所在目录（递归扫 <c>*.prefab</c>）。
    ///
    /// <para>
    /// 用"扫目录"而不是硬编码文件名：新增一种敌人时只要把 prefab 放进这个目录，
    /// 重跑一次本脚本就会被接上。硬编码清单最容易出的问题是**漏一个**，
    /// 而漏掉的症状是运行时 "Failed to spawn server object"（只在客户端出现，Host 反而正常）。
    /// </para>
    /// </summary>
    private static readonly string[] SpawnPrefabDirs =
    {
        "Assets/Game/Prefabs/EnemyPrefab",
        "Assets/Game/Prefabs/Tower",
    };

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
        if (!SetupSpawnablePrefabs()) return false;
        if (!SetupTowerPrefabs()) return false;
        if (!SetupCartPrefab()) return false;
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
            EnsureComponent<NetworkHealthSync>(root);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Debug.Log("[NetworkSetup] 玩家 prefab 已接线：" +
                      "NetworkIdentity + NetworkRigidbodyReliable2D(ClientToServer) + " +
                      "NetworkPlayerState + NetworkHealthSync。");
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ── 可生成的 prefab（敌人 / 塔 / 投射物…）──

    /// <summary>
    /// 给 <see cref="SpawnPrefabDirs"/> 下的每个 prefab 补上网络组件。
    ///
    /// <para>
    /// 敌人是**服务端权威**：位置由服务端算、客户端只是重放，所以用
    /// <c>NetworkRigidbodyUnreliable2D</c> + <c>ServerToClient</c>（默认方向）。
    /// 选 Unreliable 而不是 Reliable：敌人数量多、位置每帧都在变，
    /// 丢一帧位置无所谓（下一次同步会覆盖），但可靠通道的重传会在拥塞时雪上加霜。
    /// </para>
    /// </summary>
    private static bool SetupSpawnablePrefabs()
    {
        for (int d = 0; d < SpawnPrefabDirs.Length; d++)
        {
            string dir = SpawnPrefabDirs[d];
            if (!Directory.Exists(dir))
            {
                Debug.LogError($"[NetworkSetup] 可生成 prefab 目录不存在：{dir}");
                return false;
            }

            string[] files = Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace("\\", "/");
                if (!SetupSpawnablePrefab(path)) return false;
            }

            Debug.Log($"[NetworkSetup] {dir}：已接线 {files.Length} 个可生成 prefab。");
        }

        return true;
    }

    private static bool SetupSpawnablePrefab(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null)
        {
            Debug.LogError($"[NetworkSetup] 无法加载 prefab 内容：{path}");
            return false;
        }

        try
        {
            EnsureComponent<NetworkIdentity>(root);

            if (root.GetComponent<Rigidbody2D>() == null)
            {
                Debug.LogWarning($"[NetworkSetup] {Path.GetFileName(path)} 根节点没有 Rigidbody2D，" +
                                 $"{nameof(NetworkRigidbodyUnreliable2D)} 会报错，已改为只挂 NetworkTransform。");
                var fallback = EnsureComponent<NetworkTransformUnreliable>(root);
                fallback.target = root.transform;
                fallback.syncInterval = EnemySyncInterval;
                fallback.onlySyncOnChange = true;
            }
            else
            {
                // AddComponent<NetworkTransform/NetworkRigidbody> 会触发 Reset()，此时 target 为空而抛 NRE ——
                // 那是噪声（组件仍会加上），随后显式设置即可
                var body = EnsureComponent<NetworkRigidbodyUnreliable2D>(root);
                body.target = root.transform;
                body.syncDirection = SyncDirection.ServerToClient;   // 敌人是服务端权威
                body.syncInterval = EnemySyncInterval;
                body.onlySyncOnChange = true;
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// 敌人的位置同步间隔（秒）。
    /// 20Hz 对这种俯视游戏足够（客户端会插值），比每帧同步省一个数量级的带宽 ——
    /// 场上可能有几十只敌人，这一项直接决定联机能不能跑。
    /// </summary>
    private const float EnemySyncInterval = 0.05f;

    // ── 塔 prefab ──

    /// <summary>
    /// 给塔挂上血量同步。
    ///
    /// <para>
    /// <b>为什么单独一步而不是并进 <see cref="SetupSpawnablePrefab"/>：</b>
    /// 敌人也在那个目录扫描里，而敌人**不需要**血量同步 ——
    /// 它们没有血条，死亡靠 <c>NetworkServer.Destroy</c> 广播，本身就是同步信号。
    /// 给几十只敌人各挂一个 SyncVar 是纯粹的浪费。
    /// </para>
    /// </summary>
    private static bool SetupTowerPrefabs()
    {
        const string dir = "Assets/Game/Prefabs/Tower";
        if (!Directory.Exists(dir))
        {
            Debug.LogError($"[NetworkSetup] 找不到塔 prefab 目录：{dir}");
            return false;
        }

        string[] files = Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            string path = files[i].Replace("\\", "/");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError($"[NetworkSetup] 无法加载 prefab 内容：{path}");
                return false;
            }

            try
            {
                if (root.GetComponent<TowerHealthController>() == null)
                {
                    Debug.LogWarning($"[NetworkSetup] {Path.GetFileName(path)} 上没有 TowerHealthController，已跳过血量同步。");
                    continue;
                }

                EnsureComponent<NetworkHealthSync>(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[NetworkSetup] {dir}：已给 {files.Length} 座塔挂上血量同步。");
        return true;
    }

    // ── 推车 prefab ──

    /// <summary>给推车挂上状态同步组件（**不加** NetworkIdentity，理由见 <see cref="CartPrefabPath"/> 的注释）。</summary>
    private static bool SetupCartPrefab()
    {
        if (!File.Exists(CartPrefabPath))
        {
            Debug.LogError($"[NetworkSetup] 找不到推车 prefab：{CartPrefabPath}");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(CartPrefabPath);
        if (root == null)
        {
            Debug.LogError($"[NetworkSetup] 无法加载 prefab 内容：{CartPrefabPath}");
            return false;
        }

        try
        {
            var sync = EnsureComponent<CartNetworkSync>(root);

            // 直接赋值而不是 SerializedObject：prefab 上虽然两条路都行，
            // 但项目里统一用直接赋值（见 CLAUDE.md 的批处理约定）
            sync.Cart = root.GetComponent<CartController>();
            sync.Health = root.GetComponent<CartHealthController>();

            PrefabUtility.SaveAsPrefabAsset(root, CartPrefabPath);
            Debug.Log("[NetworkSetup] 推车 prefab 已接线：CartNetworkSync（不加 NetworkIdentity）。");
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

            // spawnPrefabs 是 Mirror 解析 assetId → prefab 的**唯一**来源：
            // 不在这个列表里的 prefab，服务端 Spawn 之后客户端会报
            // "Failed to spawn server object, did you forget to add it to the NetworkManager?"，
            // 而 Host 端因为对象就在本地、看起来一切正常 —— 只在纯客户端暴露。
            var spawnables = new List<GameObject>();
            if (playerPrefab != null) spawnables.Add(playerPrefab);

            int scanned = 0;
            for (int d = 0; d < SpawnPrefabDirs.Length; d++)
            {
                string dir = SpawnPrefabDirs[d];
                if (!Directory.Exists(dir)) continue;

                string[] files = Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i].Replace("\\", "/");
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    if (!prefab.TryGetComponent(out NetworkIdentity _))
                    {
                        Debug.LogWarning($"[NetworkSetup] {path} 没有 NetworkIdentity，已跳过（先跑 SetupSpawnablePrefabs）。");
                        continue;
                    }

                    if (!spawnables.Contains(prefab)) spawnables.Add(prefab);
                    scanned++;
                }
            }

            nm.spawnPrefabs = spawnables;

            PrefabUtility.SaveAsPrefabAsset(root, NetworkManagerPrefabPath);
            Debug.Log($"[NetworkSetup] 联机 prefab 已就绪：{NetworkManagerPrefabPath}" +
                      $"（offlineScene={nm.offlineScene}，onlineScene=空，spawnPrefabs={spawnables.Count}" +
                      $"，其中可生成 prefab {scanned} 个）");
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
        var paths = new List<string> { PlayerPrefabPath, NetworkManagerPrefabPath };

        for (int d = 0; d < SpawnPrefabDirs.Length; d++)
        {
            string dir = SpawnPrefabDirs[d];
            if (!Directory.Exists(dir)) continue;

            string[] files = Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                paths.Add(files[i].Replace("\\", "/"));
        }

        for (int i = 0; i < paths.Count; i++)
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
        }

        Debug.Log($"[NetworkSetup] 已确认 {paths.Count} 个 prefab 的 assetId。");
        AssetDatabase.SaveAssets();
    }

    /// <summary>取组件，没有就加（幂等）。</summary>
    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        return go.TryGetComponent(out T existing) ? existing : go.AddComponent<T>();
    }
}
#endif
