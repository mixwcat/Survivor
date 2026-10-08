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
/// <b>三种角色</b>（由 <c>SessionState</c> 指定，入口是 <c>Assets/Editor/NetworkSmokeTest.cs</c>）：
/// </para>
/// <list type="bullet">
/// <item><c>host</c> —— 单进程自测：建房 → 生成玩家 → 切场景 → 重建。覆盖**服务端**那一半。</item>
/// <item><c>server</c> —— 双进程测试的服务端：建房 → 等真客户端接入 → 切到关卡 → 常驻。</item>
/// <item><c>client</c> —— 双进程测试的客户端：连接 → 断言**客户端侧**的一切。</item>
/// </list>
///
/// <para>
/// <b>为什么必须有 <c>client</c> 这一路：</b>Host 单进程里服务端与客户端是同一个对象，
/// 所有 <c>ApplyNetwork*</c> 都会被权威守卫提前返回 ——
/// "客户端真的按广播走"这条路径在 Host 下**原理上就测不到**。
/// 已经因此漏过一个 bug：<c>NetworkAuthority</c> 对场景对象恒为 true，
/// 导致整批客户端守卫失效，而 Host 测试全绿。
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

    /// <summary>角色开关（<c>host</c> / <c>server</c> / <c>client</c>），同样借 <c>SessionState</c> 跨域重载。</summary>
    public const string RoleKey = "Survivor.SmokeTest.Role";

    private const string StagePath = "Assets/Scenes/Level0.unity";
    private const string LobbyPath = "Assets/Scenes/Lobby.unity";

    private const string ServerAddress = "127.0.0.1";
    private const ushort ServerPort = 7777;

    /// <summary>每一步的等待上限（秒）。Addressables 首次加载、副本工程的首次编译都会慢一些。</summary>
    private const float StepTimeoutSeconds = 90f;

    /// <summary>玩家数量超量后允许持续的秒数（<c>Destroy</c> 帧末生效造成的正常中间态）。</summary>
    private const float OverCountGraceSeconds = 2f;

    /// <summary>
    /// 双进程测试的**哨兵波次**：服务端在关卡里把它设成这个值，客户端断言自己看到了它。
    ///
    /// <para>
    /// <b>为什么需要哨兵：</b>"客户端波次是 0"和"广播根本没生效"长得一模一样 ——
    /// 波次本来就没人在正常流程里改过。而权威判据写错的症状是**完全静默**的，
    /// 所以必须有一个客户端**不可能自己算出来**的值。
    /// </para>
    /// </summary>
    private const int SentinelWave = 42;

    private string _role = "host";
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
        go.AddComponent<NetworkSmokeDriver>()._role = SessionState.GetString(RoleKey, "host");
        Debug.Log("[Smoke] 驱动已创建");
    }

    private void Start()
    {
        switch (_role)
        {
            case "server": StartCoroutine(RunServer()); break;
            case "client": StartCoroutine(RunClient()); break;
            default: StartCoroutine(RunHost()); break;
        }
    }

    // ── 单进程 Host：覆盖服务端那一半 ──

    private IEnumerator RunHost()
    {
        Log("角色=host，等 NetworkBootstrap 就绪");
        yield return WaitBootstrap();
        if (_failed) yield break;

        yield return WaitOfflinePlayer();
        if (_failed) yield break;

        Log($"离线玩家数量={CountPlayers()} → 建房");
        NetworkBootstrap.StartHost();

        yield return WaitHostConnected();
        if (_failed) yield break;

        yield return WaitForPlayerCount(1, "建房后的玩家数量");
        if (_failed) yield break;

        Log("切到关卡…");
        NetworkBootstrap.ServerChangeScene(StagePath);

        yield return WaitUntil(() => SceneManager.GetActiveScene().path == StagePath, "关卡场景加载完成");
        yield return WaitForPlayerCount(1, "关卡内的玩家数量");
        if (_failed) yield break;

        yield return WaitStageGameplay();
        if (_failed) yield break;

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

    // ── 双进程：服务端 ──

    /// <summary>
    /// 服务端：建房 → 等一个**真正的第二个进程**接进来 → 切到关卡 → 常驻。
    ///
    /// <para>
    /// 常驻而不退出：客户端要留在关卡里跑断言。<c>Tools\run-network-2p.ps1</c> 会在客户端
    /// 退出后杀掉本进程。这条路径靠日志里的 <c>SMOKE_OK</c> 判定，而不是退出码。
    /// </para>
    /// </summary>
    private IEnumerator RunServer()
    {
        Log("角色=server，等 NetworkBootstrap 就绪");
        yield return WaitBootstrap();
        if (_failed) yield break;

        yield return WaitOfflinePlayer();
        if (_failed) yield break;

        Log("建房，等真客户端接入…");
        NetworkBootstrap.StartHost();

        yield return WaitHostConnected();
        if (_failed) yield break;

        // ASCII 标记：编排脚本（Tools\run-network-2p.ps1）靠它判断"服务端已就绪，可以起客户端了"。
        // 脚本必须全 ASCII —— PowerShell 5.1 会把无 BOM 的 UTF-8 中文读成乱码并报语法错，
        // 所以这里不能让它去 grep 中文日志行。
        Debug.Log("[Smoke] SERVER_READY");

        yield return WaitForPlayerCount(1, "建房后的玩家数量");
        if (_failed) yield break;

        // 等第二个进程进来 —— 这一步只有双进程测试才会满足
        yield return WaitForPlayerCount(2, "客户端接入后的玩家数量");
        if (_failed) yield break;

        Log("客户端已接入，切到关卡…");
        NetworkBootstrap.ServerChangeScene(StagePath);

        yield return WaitUntil(() => SceneManager.GetActiveScene().path == StagePath, "关卡场景加载完成");
        yield return WaitForPlayerCount(2, "关卡内两端的玩家数量");
        if (_failed) yield break;

        yield return WaitStageGameplay();
        if (_failed) yield break;

        // 打哨兵：客户端**不可能自己算出来**的值，只有服务端广播能带过去
        if (GameLevelManager.Service != null) GameLevelManager.Service.CurrentWave = SentinelWave;
        Debug.Log("[Smoke] SENTINEL_SET");

        // 客户端会挑 netId 最小的那只敌人打一发致命伤害（见 AssertClientAuthorityPaths）。
        // 两端用同一个确定性规则选目标，所以不需要互相通信就能盯住同一只怪
        uint victimNetId = LowestEnemyNetId();
        Log($"盯着 netId={victimNetId} 的敌人，等客户端上报的伤害把它打死…");

        // 回归①：玩家血量同步。客户端随后会断言"队友的血量掉下来了"。
        // 回归②（更重要）：它同时是**递归 bug 的守门人** ——
        // PlayerHealthController.ApplyDamage 曾经写成 base.TakeDamage（而不是 base.ApplyDamage），
        // 那在服务端会无限递归 ⇒ 栈溢出。敌人那条扣血路径不走这里，所以上一轮的测试抓不到它
        PlayerController hostPlayer = FindLocalPlayer();
        if (hostPlayer != null && hostPlayer.TryGetComponent(out BaseHealthController hostHealth))
        {
            float before = hostHealth.CurrentHealth;
            hostHealth.TakeDamage(new DamageInfo(1f, 0f, null, DamageSource.Contact));
            Log($"服务端给自己扣 1 点：{before:F1} → {hostHealth.CurrentHealth:F1}（玩家路径无递归）");
        }
        else
        {
            Fail("服务端上找不到本地玩家，无法验证玩家血量同步");
            yield break;
        }

        yield return WaitUntil(() => !NetworkServer.spawned.ContainsKey(victimNetId),
                               "客户端上报的伤害被服务端结算，敌人已销毁", 90f);
        if (_failed) yield break;

        Log("伤害链路已确认：客户端命中 → 上报 → 服务端结算 → Destroy");
        Log($"服务端侧就绪（玩家={CountPlayers()}，敌人={CountSpawnedEnemies()}，" +
            $"推车={CurrentCartDistance():F2}，时钟={CurrentLevelTime():F2}）");
        Debug.Log("[Smoke] SMOKE_OK");

        // 常驻：等编排脚本杀掉本进程
        while (true) yield return new WaitForSeconds(5f);
    }

    // ── 双进程：客户端（**Host 测试覆盖不到的那一半**）──

    private IEnumerator RunClient()
    {
        Log("角色=client，等 NetworkBootstrap 就绪");
        yield return WaitBootstrap();
        if (_failed) yield break;

        yield return WaitOfflinePlayer();
        if (_failed) yield break;

        Log($"连接 {ServerAddress}:{ServerPort} …");
        NetworkBootstrap.StartClient(ServerAddress, ServerPort);

        yield return WaitUntil(() => NetworkClient.isConnected, "客户端连接建立");
        if (_failed) yield break;

        // 建房方（服务端）会把自己的玩家生成出来；客户端这一侧应当看到**两个**玩家
        yield return WaitForPlayerCount(2, "房间里两端的玩家数量");
        if (_failed) yield break;

        yield return AssertLocalPlayerIdentity();
        if (_failed) yield break;

        // 服务端会切到关卡，客户端跟着切（客户端不自己切场景）
        yield return WaitUntil(() => SceneManager.GetActiveScene().path == StagePath, "跟着服务端切到关卡", 120f);
        if (_failed) yield break;

        yield return WaitForPlayerCount(2, "关卡内两端的玩家数量");
        if (_failed) yield break;

        yield return WaitStageGameplay();
        if (_failed) yield break;

        yield return AssertClientAuthorityPaths();
        if (_failed) yield break;

        Pass();
    }

    /// <summary>
    /// ⭐ **本测试存在的全部理由**：证明客户端侧的 <c>ApplyNetwork*</c> 真的在执行。
    ///
    /// <para>
    /// 上面那些"推车在动 / 时钟在走"**不足以**证明广播生效 —— 权威判据写错时
    /// 客户端会自己推进，看起来一样在动。真正的判据只有两个：
    /// </para>
    /// <list type="number">
    /// <item>客户端看到了一个**它不可能自己算出来**的哨兵波次；</item>
    /// <item><c>CartController.AppliedNetworkStateCount &gt; 0</c> —— 只有"广播真的被应用"才会增长。</item>
    /// </list>
    /// </summary>
    private IEnumerator AssertClientAuthorityPaths()
    {
        yield return WaitUntil(() => CurrentWave() == SentinelWave,
                               $"服务端哨兵波次 {SentinelWave} 到达客户端");
        if (_failed) yield break;

        CartController cart = FindFirstObjectByType<CartController>();
        int applied = cart != null ? cart.AppliedNetworkStateCount : 0;

        if (applied <= 0)
        {
            Fail("客户端从未应用过服务端的推车状态（AppliedNetworkStateCount == 0）—— " +
                 "说明 CartController.ApplyNetworkState 被权威判据挡掉了，客户端的车是在自己推进");
            yield break;
        }

        Log($"客户端权威路径已确认：哨兵波次={SentinelWave}，已应用 {applied} 条推车状态广播");

        yield return AssertDamageRoundTrip();
    }

    /// <summary>
    /// ⭐⭐ **伤害的完整往返**：客户端本地调用 <c>TakeDamage</c> → 路由上报 →
    /// 服务端解析 netId 并结算 → <c>NetworkServer.Destroy</c> → 客户端看到销毁。
    ///
    /// <para>
    /// 这条链路跨越了本项目里最容易出错的三样东西：<c>[Command]</c> 的发送方身份
    /// （<c>LocalSender</c>）、netId 在两端的对应关系、以及"客户端不本地结算"这个约定。
    /// 它在 Host 测试里**完全测不到** —— Host 下 <c>TakeDamage</c> 直接本地结算，走不到路由。
    /// </para>
    ///
    /// <para>
    /// 目标用"netId 最小的那只"这种**确定性规则**挑，两端各自算一次就能盯住同一只怪，
    /// 不需要为了测试加任何跨进程通信。
    /// </para>
    /// </summary>
    private IEnumerator AssertDamageRoundTrip()
    {
        uint victimNetId = LowestEnemyNetId();
        if (victimNetId == 0u)
        {
            Fail("客户端上没有已 spawn 的敌人，无法验证伤害链路");
            yield break;
        }

        if (!NetworkClient.spawned.TryGetValue(victimNetId, out NetworkIdentity victim) || victim == null)
        {
            Fail($"客户端解析不出 netId={victimNetId} 的敌人");
            yield break;
        }

        if (!victim.TryGetComponent(out BaseHealthController health))
        {
            Fail($"netId={victimNetId} 上没有 BaseHealthController");
            yield break;
        }

        PlayerController local = FindLocalPlayer();
        EntityBehaviour attacker = local != null ? local.GetComponent<EntityBehaviour>() : null;

        Log($"对 netId={victimNetId} 的敌人打一发致命伤害（本地 TakeDamage，应被路由到服务端）");
        health.TakeDamage(new DamageInfo(99999f, 0f, attacker, DamageSource.Projectile));

        yield return WaitUntil(() => !NetworkClient.spawned.ContainsKey(victimNetId),
                               "敌人被服务端结算并销毁，客户端收到销毁", 90f);
        if (_failed) yield break;

        Log("伤害往返已确认：客户端命中 → [Command] 上报 → 服务端结算 → Destroy 广播回来");

        // ⭐ 队友的血量必须同步下来：不打开这条，客户端副本的血量永远不动
        //（HUD 血条一直满、角色永远不会死），而且完全静默
        yield return WaitUntil(RemotePlayerDamaged, "队友的血量同步到客户端（服务端扣了 1 点）", 60f);
        if (_failed) yield break;

        Log("玩家血量同步已确认：服务端扣血 → SyncVar → 客户端副本血量下降");
    }

    /// <summary>客户端侧：有没有一个**远程**玩家的血量低于其上限。</summary>
    private static bool RemotePlayerDamaged()
    {
        IPlayerManager players = PlayerManager.Service;
        if (players == null) return false;

        for (int i = 0; i < players.AllPlayers.Count; i++)
        {
            PlayerController player = players.AllPlayers[i];
            if (player == null) continue;
            if (player.TryGetComponent(out NetworkIdentity identity) && identity.isLocalPlayer) continue;
            if (!player.TryGetComponent(out BaseHealthController health)) continue;

            if (health.CurrentHealth < health.MaxHealth - 0.5f) return true;
        }

        return false;
    }

    /// <summary>客户端侧必须能认出"哪个是我的角色"——认错就等于相机跟错人、输入给错人。</summary>
    private IEnumerator AssertLocalPlayerIdentity()
    {
        yield return null;   // 让 OnStartLocalPlayer 跑完

        IPlayerManager players = PlayerManager.Service;
        if (players == null)
        {
            Fail("客户端上 PlayerManager 未就绪");
            yield break;
        }

        int localCount = 0;
        for (int i = 0; i < players.AllPlayers.Count; i++)
        {
            PlayerController player = players.AllPlayers[i];
            if (player == null) continue;
            if (player.TryGetComponent(out NetworkIdentity identity) && identity.isLocalPlayer) localCount++;
        }

        if (localCount != 1)
        {
            Fail($"客户端上标记为 isLocalPlayer 的角色有 {localCount} 个（应为 1）");
            yield break;
        }

        Log("客户端已认出唯一的本地角色");
    }

    // ── 关卡内的公共断言 ──

    private IEnumerator WaitStageGameplay()
    {
        Log("等敌人生成…");
        yield return WaitUntil(() => CountSpawnedEnemies() >= 1, "关卡内出现已 spawn 的敌人");
        if (_failed) yield break;

        Log("等推车开始行驶…");
        yield return WaitUntil(() => CurrentCartDistance() > 0.5f, "推车推进超过 0.5 弧长");
        if (_failed) yield break;

        yield return WaitUntil(() => CurrentLevelTime() > 1.0f, "关卡时钟走过 1 秒");
        if (_failed) yield break;

        Log($"关卡内：敌人={CountSpawnedEnemies()}（netId != 0），" +
            $"推车={CurrentCartDistance():F2}，时钟={CurrentLevelTime():F2}，阶段={CurrentPhase()}");
    }

    // ── 等待原语 ──

    private IEnumerator WaitBootstrap()
    {
        yield return WaitUntil(() => NetworkBootstrap.IsReady || GameBootstrap.FailureReason != null,
                               "NetworkBootstrap 就绪", 120f);

        if (_failed) yield break;

        if (!string.IsNullOrEmpty(GameBootstrap.FailureReason))
            Fail($"组合根关键服务失败：{GameBootstrap.FailureReason}");
    }

    private IEnumerator WaitOfflinePlayer()
    {
        Log("等大厅生成离线玩家");
        yield return WaitUntil(() => CountPlayers() >= 1, "离线玩家生成");
    }

    private IEnumerator WaitHostConnected()
    {
        yield return WaitUntil(() => NetworkServer.active && NetworkClient.isConnected, "Host 连接建立");
        if (_failed) yield break;

        if (SceneManager.GetActiveScene().path != LobbyPath)
        {
            Fail($"建房竟然切了场景（当前={SceneManager.GetActiveScene().path}）—— " +
                 "说明 onlineScene 没留空或 offlineScene 配错");
        }
    }

    private IEnumerator WaitUntil(Func<bool> condition, string what) => WaitUntil(condition, what, StepTimeoutSeconds);

    private IEnumerator WaitUntil(Func<bool> condition, string what, float timeoutSeconds)
    {
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;

        while (!condition())
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Fail($"等待超时（{timeoutSeconds:0} 秒）：{what}");
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

    // ── 观测 ──

    private static int CountPlayers()
    {
        IPlayerManager players = PlayerManager.Service;
        return players != null ? players.AllPlayers.Count : 0;
    }

    /// <summary>
    /// 场上**已经 spawn**（<c>netId != 0</c>）的敌人数量。
    ///
    /// <para>
    /// 不用 <c>GameLevelManager.GetEnemyCount()</c>：那个计数由 <c>EnemyController.OnEnable</c> 维护，
    /// 而 <c>OnEnable</c> 在 <c>Instantiate</c> 时就跑了 —— 即使 <c>NetworkServer.Spawn</c> 失败，
    /// 计数照样是正的。查 <c>netId</c> 才能证明"这只怪真的被 Mirror 接管了"。
    /// </para>
    ///
    /// <para>
    /// <b>在客户端角色下这条断言最有价值</b>：客户端能拿到 <c>netId</c> 说明
    /// assetId 与 <c>spawnPrefabs</c> 都对 —— 漏一个的症状是客户端报
    /// "Failed to spawn server object"，而 Host 端一切正常。
    /// </para>
    /// </summary>
    private static int CountSpawnedEnemies()
    {
        EnemyController[] all = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        int count = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (!all[i].TryGetComponent(out NetworkIdentity identity)) continue;
            if (identity.netId != 0) count++;
        }

        return count;
    }

    /// <summary>
    /// 场上 netId 最小的那只敌人的 netId（没有则返回 0）。
    ///
    /// <para>
    /// 用它当"共同目标"：服务端与客户端各自算一次就能盯住**同一只**怪，
    /// 不需要为了测试加跨进程通信。用 netId 而不是"第一个找到的" ——
    /// 后者在两端可能不是同一只。
    /// </para>
    /// </summary>
    private static uint LowestEnemyNetId()
    {
        EnemyController[] all = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        uint lowest = 0u;

        for (int i = 0; i < all.Length; i++)
        {
            if (!all[i].TryGetComponent(out NetworkIdentity identity)) continue;
            if (identity.netId == 0u) continue;

            if (lowest == 0u || identity.netId < lowest) lowest = identity.netId;
        }

        return lowest;
    }

    private static PlayerController FindLocalPlayer()
    {
        IPlayerManager players = PlayerManager.Service;
        if (players == null) return null;

        for (int i = 0; i < players.AllPlayers.Count; i++)
        {
            PlayerController player = players.AllPlayers[i];
            if (player == null) continue;
            if (player.TryGetComponent(out NetworkIdentity identity) && identity.isLocalPlayer) return player;
        }

        return null;
    }

    /// <summary>
    /// 当前关卡的推车已行驶弧长（没有推车时返回 0）。
    ///
    /// <para>
    /// 在**客户端**角色下它验证的是 <c>CartController.ApplyNetworkState</c> 真的在执行 ——
    /// 也就是"客户端按服务端的广播摆位"。Host 下这条路径会被权威守卫提前返回，测不到。
    /// </para>
    /// </summary>
    private static float CurrentCartDistance()
    {
        CartController cart = FindFirstObjectByType<CartController>();
        return cart != null ? cart.TravelledDistance : 0f;
    }

    /// <summary>
    /// 关卡时钟（<c>GameLevelManager.LevelTime</c>）。
    /// 服务端角色下验证"时钟守卫没把服务端自己挡住"；
    /// 客户端角色下验证 <c>ApplyNetworkClock</c> 真的在执行。
    /// </summary>
    private static float CurrentLevelTime()
    {
        IGameLevelManager level = GameLevelManager.Service;
        return level != null ? level.LevelTime : 0f;
    }

    private static string CurrentPhase()
    {
        StageDirector director = FindFirstObjectByType<StageDirector>();
        return director != null ? director.Phase.ToString() : "(无 StageDirector)";
    }

    private static int CurrentWave()
    {
        IGameLevelManager level = GameLevelManager.Service;
        return level != null ? level.CurrentWave : -1;
    }

    // ── 收尾 ──

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
