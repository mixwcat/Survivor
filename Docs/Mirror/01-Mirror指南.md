# Mirror 联机框架参考手册

> **用途**：供 AI agent / 开发者在本项目做联机开发时查阅。索引进同目录 `README.md`。
> **核对基准**：本仓库 vendored 源码 `Assets/Mirror/`，`Assets/Mirror/version.txt` = **96.11.3**。
> **环境**：Unity 6000.0.44f1，C# 9 / .NET Standard 2.1，目标平台 Android（触屏）+ Windows。
> **项目形态**：2D 俯视 survivor-like 合作 PvE。
>
> **本文档的写法约定**
> - 所有 API 签名都从**本地源码**逐条抄录，并标注 `文件路径:行号`。凡是官方文档与本地源码不一致的，**以本地源码为准**，并在第 17 章列出差异。
> - 标 **【官方推荐】** 的是 Mirror 官方文档明确写的推荐做法；标 **【常见坑】** 的是容易踩且不好排查的问题。
> - 没读到的写「未核实」，不猜。

---

## 目录

- [0. 源码文件地图（速查）](#0-源码文件地图速查)
- [1. 网络模型与权威](#1-网络模型与权威)
- [2. NetworkManager 生命周期与回调](#2-networkmanager-生命周期与回调)
- [3. NetworkIdentity](#3-networkidentity)
- [4. NetworkBehaviour](#4-networkbehaviour)
- [5. 远程调用（Command / ClientRpc / TargetRpc / 端限制属性）](#5-远程调用command--clientrpc--targetrpc--端限制属性)
- [6. 状态同步（SyncVar / Sync 集合 / SyncObject）](#6-状态同步syncvar--sync-集合--syncobject)
- [7. 序列化与 Weaver](#7-序列化与-weaver)
- [8. 消息、通道与 batching](#8-消息通道与-batching)
- [9. NetworkTransform / NetworkAnimator / NetworkRigidbody2D](#9-networktransform--networkanimator--networkrigidbody2d)
- [10. Interest Management（兴趣管理 / 可见性）](#10-interest-management兴趣管理--可见性)
- [11. 场景管理](#11-场景管理)
- [12. Transports / 移动端 / NAT / Discovery](#12-transports--移动端--nat--discovery)
- [13. 官方推荐做法与常见坑（总集）](#13-官方推荐做法与常见坑总集)
- [14. 性能与带宽建议](#14-性能与带宽建议)
- [15. 落到本项目（Survivor）的注意事项](#15-落到本项目survivor的注意事项)
- [16. 未核实 / 存疑清单](#16-未核实--存疑清单)
- [17. 官方文档与本地源码不一致清单](#17-官方文档与本地源码不一致清单)
- [18. 官方文档 URL 索引](#18-官方文档-url-索引)

---

## 0. 源码文件地图（速查）

| 主题 | 文件 |
|---|---|
| NetworkManager / 回调 / 场景切换 | `Assets/Mirror/Core/NetworkManager.cs` |
| NetworkServer（静态） | `Assets/Mirror/Core/NetworkServer.cs` |
| NetworkClient（静态） | `Assets/Mirror/Core/NetworkClient.cs` |
| NetworkIdentity | `Assets/Mirror/Core/NetworkIdentity.cs` |
| NetworkBehaviour | `Assets/Mirror/Core/NetworkBehaviour.cs` |
| NetworkConnection 基类 | `Assets/Mirror/Core/NetworkConnection.cs` |
| NetworkConnectionToClient | `Assets/Mirror/Core/NetworkConnectionToClient.cs` |
| NetworkConnectionToServer | `Assets/Mirror/Core/NetworkConnectionToServer.cs` |
| 属性定义（`[Command]` 等） | `Assets/Mirror/Core/Attributes.cs` |
| RPC 注册表 | `Assets/Mirror/Core/RemoteCalls.cs` |
| 内置消息结构 | `Assets/Mirror/Core/Messages.cs` |
| `NetworkMessage` 标记接口 | `Assets/Mirror/Core/NetworkMessage.cs` |
| SyncVar / SyncObject 支持 | `Assets/Mirror/Core/NetworkBehaviour.cs`、`NetworkBehaviourSyncVar.cs` |
| SyncList | `Assets/Mirror/Core/SyncList.cs` |
| SyncDictionary | `Assets/Mirror/Core/SyncDictionary.cs` |
| SyncSet / SyncHashSet / SyncSortedSet | `Assets/Mirror/Core/SyncSet.cs` |
| SyncObject 基类 | `Assets/Mirror/Core/SyncObject.cs` |
| 序列化读写 | `Assets/Mirror/Core/NetworkWriter.cs`、`NetworkReader.cs`、`NetworkWriterExtensions.cs`、`NetworkReaderExtensions.cs` |
| 通道常量 | `Assets/Mirror/Core/Tools/Utils.cs`（`class Channels`） |
| Transport 基类 / `PortTransport` | `Assets/Mirror/Core/Transport.cs`、`PortTransport.cs` |
| 网络时间 | `Assets/Mirror/Core/NetworkTime.cs` |
| Weaver 保险丝 | `Assets/Mirror/Core/WeaverFuse.cs` |
| 连接质量 / 诊断 | `Assets/Mirror/Core/ConnectionQuality.cs`、`NetworkDiagnostics.cs` |
| NetworkTransform | `Assets/Mirror/Components/NetworkTransform/NetworkTransformBase.cs` + `…Reliable.cs` / `…Unreliable.cs` / `…Hybrid.cs` |
| NetworkAnimator | `Assets/Mirror/Components/NetworkAnimator.cs` |
| NetworkRigidbody（2D） | `Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyReliable2D.cs`、`…Unreliable2D.cs` |
| Interest Management | `Assets/Mirror/Core/InterestManagement.cs`、`InterestManagementBase.cs`、`Assets/Mirror/Components/InterestManagement/**` |
| NetworkStartPosition | `Assets/Mirror/Core/NetworkStartPosition.cs` |
| NetworkStatistics | `Assets/Mirror/Components/NetworkStatistics.cs` |
| NetworkDiscovery | `Assets/Mirror/Components/Discovery/NetworkDiscovery.cs`、`NetworkDiscoveryBase.cs` |
| KCP Transport（默认） | `Assets/Mirror/Transports/KCP/KcpTransport.cs` |
| Latency Simulation | `Assets/Mirror/Transports/Latency/LatencySimulation.cs` |
| Telepathy（TCP） | `Assets/Mirror/Transports/Telepathy/TelepathyTransport.cs` |
| SimpleWeb（WebGL） | `Assets/Mirror/Transports/SimpleWeb/SimpleWebTransport.cs` |
| NetworkManagerHUD | `Assets/Mirror/Core/NetworkManagerHUD.cs` |

> ⚠️ Mirror 是 **vendored**（源码直接放在 `Assets/Mirror/`），不是 UPM 包。
> 这意味着：改 Mirror 源码 = 改项目；升级 Mirror 必须整目录替换。**原则上不要修改 `Assets/Mirror/` 下的任何文件**，扩展一律走「子类 + 自定义 Transport + 自定义 `Writer<T>`/`Reader<T>`」。
> **唯一例外**是本项目已声明的那一处本地补丁（Unity 6 domain reload 下 `NetworkConnection()` 的 `Time.time` 异常，
> 见 `03-项目落地注意.md` §15.3 与 `../MirrorPlan.md` P0.3）—— 打补丁时必须在文件里留注释说明，升级 Mirror 后要重贴。

> 📦 **以下子目录不在本仓库里**（已加进 `.gitignore`，新克隆的工作区看不到它们；本文档提到这些路径时按"本地不存在"理解）：
> `Assets/Mirror/Examples/`（官方示例）、`Assets/Mirror/Hosting/` 与 `Assets/Mirror/Transports/Edgegap/`（Edgegap 云托管与中继）、
> `Assets/Mirror/Transports/Encryption/`（加密传输，体积主要是 BouncyCastle）。
> 运行必需的 `Core` / `Components` / `Editor` / `Authenticators` / `CompilerSymbols` / `Presets` / `Plugins`
> 与 `Transports` 下的 KCP / Latency / Middleware / Multiplex / SimpleWeb / Telepathy / Threaded **都在仓库里**。
> 需要看官方示例时，从 Mirror 官方仓库或原始 unitypackage 补回即可（不影响编译）。

---

## 1. 网络模型与权威

### 1.1 三种运行模式

Mirror 只有三种模式，由 `NetworkManagerMode` 枚举表示（`Assets/Mirror/Core/NetworkManager.cs:11`）：

```csharp
public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly, Host }
```

| 模式 | 启动入口 | `NetworkServer.active` | `NetworkClient.active` | 说明 |
|---|---|---|---|---|
| 纯服务器 | `StartServer()` | `true` | `false` | 无本地玩家 |
| 纯客户端 | `StartClient()` | `false` | `true` | |
| Host（主机=服务端+本地客户端） | `StartHost()` | `true` | `true` | 同一进程；`NetworkServer.activeHost == true` |

关键静态判据（源码核对）：

```csharp
// Assets/Mirror/Core/NetworkServer.cs:96
public static bool active { get; internal set; }
// Assets/Mirror/Core/NetworkServer.cs:100
public static bool activeHost => localConnection != null;

// Assets/Mirror/Core/NetworkClient.cs:84
public static bool active => connectState == ConnectState.Connecting ||
                             connectState == ConnectState.Connected;
// Assets/Mirror/Core/NetworkClient.cs:89
public static bool activeHost => connection is LocalConnectionToServer;
// Assets/Mirror/Core/NetworkClient.cs:92 / 95
public static bool isConnecting => connectState == ConnectState.Connecting;
public static bool isConnected  => connectState == ConnectState.Connected;
```

一行示例：

```csharp
if (NetworkServer.active && !NetworkClient.active) { /* 纯服务器分支 */ }
```

【常见坑】`NetworkServer.active` 是「服务器进程活着」，不是「这个对象已经 spawn 了」。
判断**对象**是否已 spawn，用 `NetworkBehaviour.isServer` / `isClient`（它们来自 `netIdentity`，语义是「本对象已 spawn 且处于该端」）。源码注释明确写了这一点（`NetworkBehaviour.cs:57-59`）。

### 1.2 Authority（权威）

官方定义（[Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority)）：

> Authority is a way of deciding who owns an object and has control over it.

- **Server Authority（默认）**：服务器控制该对象。
- **Client Authority**：客户端控制该对象。当客户端对某对象有权威时，该客户端**可以调用该对象上的 `[Command]`**，并且**该客户端断线时这个对象会被自动销毁**。

【官方推荐】官方明确写：*Use server authority for cheat safety.*（见 `NetworkBehaviour.syncDirection` 的 Tooltip，`Assets/Mirror/Core/NetworkBehaviour.cs:36`）。

**注意 96.11.3 的 `NetworkBehaviour.authority` 不是简单的 `isOwned`**，它同时看 `syncDirection`（`Assets/Mirror/Core/NetworkBehaviour.cs:96-109`）：

```csharp
public bool authority
{
    get
    {
        // host mode needs to be checked explicitly
        if (isClient && isServer) return syncDirection == SyncDirection.ServerToClient || isOwned;

        // client-only
        if (isClient) return syncDirection == SyncDirection.ClientToServer && isOwned;

        // server-only
        return syncDirection == SyncDirection.ServerToClient;
    }
}
```

【常见坑】在 host 模式下，`syncDirection == ServerToClient`（默认值）时 `authority` **恒为 true**，哪怕这个对象不是你拥有的。
如果你要写「只有我能操作我自己的角色」，**请用 `isOwned` 或 `isLocalPlayer`，不要用 `authority`**。

### 1.3 如何授予 / 移除客户端权威

**（a）spawn 时授予** —— `NetworkServer.Spawn` 的第二个参数：

```csharp
GameObject go = Instantiate(prefab);
NetworkServer.Spawn(go, connectionToClient);   // 签名见 NetworkServer.cs:1704
```

签名（`Assets/Mirror/Core/NetworkServer.cs`）：

```csharp
public static void Spawn(GameObject obj, NetworkConnectionToClient ownerConnection = null);          // :1704
public static void Spawn(GameObject obj, uint assetId, NetworkConnectionToClient ownerConnection = null); // :1711
public static void Spawn(GameObject obj, GameObject ownerPlayer);                                    // :1670
```

【官方推荐】官方文档：*If you spawn a player object using `NetworkServer.AddPlayerForConnection` then it will automatically be given authority.*（[Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority)）

**（b）运行时授予** —— `NetworkIdentity.AssignClientAuthority`：

```csharp
identity.AssignClientAuthority(conn);   // Assets/Mirror/Core/NetworkIdentity.cs:1547
```

完整签名：

```csharp
public bool AssignClientAuthority(NetworkConnectionToClient conn);  // NetworkIdentity.cs:1547
public void RemoveClientAuthority();                                // NetworkIdentity.cs:1620
```

官方给的「玩家捡起物品」范式：

```csharp
// Command on player object
void CmdPickupItem(NetworkIdentity item)
{
    item.AssignClientAuthority(connectionToClient);
}
```

**（c）变更通知回调** —— `NetworkIdentity.clientAuthorityCallback`：

```csharp
// Assets/Mirror/Core/NetworkIdentity.cs:319 / 322
public delegate void ClientAuthorityCallback(NetworkConnectionToClient conn, NetworkIdentity identity, bool authorityState);
public static event ClientAuthorityCallback clientAuthorityCallback;
```

```csharp
void OnEnable()  => NetworkIdentity.clientAuthorityCallback += OnAuthorityChanged;
void OnDisable() => NetworkIdentity.clientAuthorityCallback -= OnAuthorityChanged;

static void OnAuthorityChanged(NetworkConnectionToClient conn, NetworkIdentity identity, bool authorityState)
{
    Debug.Log($"{identity.name} authority -> {authorityState} for conn {conn?.connectionId}");
}
```

【常见坑】
- `AssignClientAuthority` 只能在**服务器**上、对**已 spawn** 的对象调用，否则报错（源码 `NetworkIdentity.cs:1551`：`"AssignClientAuthority can only be called on the server for spawned objects."`）。
- 一个对象只能有一个 owner；已有 owner 时再 `AssignClientAuthority` 会报错，必须先 `RemoveClientAuthority()`（`NetworkIdentity.cs:1561-1564`）。
- **玩家对象不能 `RemoveClientAuthority`**（`NetworkIdentity.cs:1628-1631` 直接 `LogError("RemoveClientAuthority cannot remove authority for a player object")`）。要换玩家对象请用 `NetworkServer.ReplacePlayerForConnection`。
- 【官方 Note】*A change of `NetworkServer.Spawn` hierarchy is not automatically synced.*（[Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority)）—— 换父节点不会自动同步，需要自己写同步逻辑。

### 1.4 怎么判断权威

| 端 | 判据 | 源码 |
|---|---|---|
| 客户端 | `identity.isOwned` | `NetworkIdentity.cs:116` |
| 服务器 | `identity.connectionToClient != null` → 该连接是 owner；`null` → 服务器自己拥有 | `NetworkIdentity.cs:201` |
| 通用 | `NetworkBehaviour.isOwned`（转发 `netIdentity.isOwned`） | `NetworkBehaviour.cs:79` |

```csharp
// 只有本机拥有的对象才接受输入
if (!isOwned) return;
```

【官方推荐】官方原文：*The `identity.isOwned` property can be used to check if the local player has authority over an object.* / *The `identity.connectionToClient` property can be checked to see which client has authority over an object. If it is null then the server has authority.*（[Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority)）

### 1.5 Authority 与 SyncVar 的关系

【官方明确】*Even if a client has authority over an object the server still controls SyncVar and control other serialization features. A component will need to use a Command to update the state on the server in order for it to sync to other clients.*（[Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority)）

也就是说：**客户端权威 ≠ 客户端能改 SyncVar**。想让自己的状态被所有人看到，还是要 `[Command]` → 服务器改 SyncVar → 广播。

---

## 2. NetworkManager 生命周期与回调

### 2.1 类声明与单例

```csharp
// Assets/Mirror/Core/NetworkManager.cs:17
public class NetworkManager : MonoBehaviour
// :151
public static NetworkManager singleton { get; internal set; }
// :174
public NetworkManagerMode mode { get; private set; }
```

【常见坑】`singleton` 的 setter 是 `internal`，**外部不能赋值**。它由 `InitializeSingleton()`（`NetworkManager.cs:223` 的 `Awake` 调用）设置。

`InitializeSingleton()` 行为（源码 `NetworkManager.cs` 内）：
- 若 `singleton != null && singleton == this` → 直接 `return true`。
- 若 `dontDestroyOnLoad == true` 且已有其它 `singleton` → **打 warning 并 `Destroy(gameObject)`**（`NetworkManager.cs:703`），返回 `false`（`Awake` 会直接 return，不再继续初始化）。
- 若 `dontDestroyOnLoad == true` → 设 `singleton = this`，`transform.SetParent(null)`，`DontDestroyOnLoad(gameObject)`。
- 若 `dontDestroyOnLoad == false` → 只设 `singleton = this`（不跨场景保留）。

【官方 Note】*You can only ever have one active Network Manager in each scene because it's a singleton. Do not place the Network Manager component on a networked game object (one which has a Network Identity component), because Mirror disables these when the Scene loads.*（[Network Manager](https://mirror-networking.gitbook.io/docs/manual/components/network-manager)）

【常见坑 · Domain Reload】本项目在编辑器里**关闭了 Domain Reload**（`EnterPlayModeOptionsEnabled`，见 `CLAUDE.md`）。这意味着：
- **静态状态会跨 Play 会话存活**。`NetworkManager.singleton`、`NetworkServer.active`、`NetworkClient.spawned`、`NetworkManager.networkSceneName`、`NetworkIdentity.sceneIds` 等全部是静态的，第二次 Play 时可能还是上次的残留值。
- Mirror 提供了统一重置入口：

```csharp
// Assets/Mirror/Core/NetworkManager.cs:778
public static void ResetStatics()
{
    if (singleton) singleton.StopHost();
    startPositions.Clear();
    startPositionIndex = 0;
    clientReadyConnection = null;
    loadingSceneAsync = null;
    networkSceneName = string.Empty;
    singleton = null;
}
```

【官方推荐】关闭 Domain Reload 的项目应在自己的 Bootstrap 里主动调用一次清理：

```csharp
// 项目侧建议：Play 开始时先兜底清理
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetMirrorStatics()
{
    NetworkManager.ResetStatics();
    NetworkServer.Shutdown();
    NetworkClient.Shutdown();
}
```

> 注：`NetworkManager.ResetStatics` 会调用 `singleton.StopHost()`，因此它本身有副作用；放在 `SubsystemRegistration` 阶段属于本项目自定的防御策略，**官方文档没有给出「关 Domain Reload 时该怎么做」的明确指引（未核实）**。

### 2.2 主要序列化字段（Inspector 可见）

全部来自 `Assets/Mirror/Core/NetworkManager.cs`：

| 字段 | 行号 | 默认值 | 说明 |
|---|---|---|---|
| `bool dontDestroyOnLoad` | 24 | `true` | 跨场景保留。**不勾选则切场景断线** |
| `bool runInBackground` | 29 | `true` | |
| `HeadlessStartOptions headlessStartMode` | 35 | `DoNothing` | `{ DoNothing, AutoStartServer, AutoStartClient }` |
| `bool editorAutoStart` | 38 | `false` | 编辑器里也自动启动 headless |
| `int sendRate` | 44 | `60` | 广播 tick 率；`ApplyConfiguration()` 里赋给 `NetworkServer.tickRate` |
| `int unreliableBaselineRate` | 48 | `1` | |
| `bool unreliableRedundancy` | 54 | `false` | |
| `Transport transport` | 65 | — | 必填 |
| `string networkAddress` | 70 | `"localhost"` | |
| `int maxConnections` | 75 | `100` | |
| `bool disconnectInactiveConnections` | 82 | `false` | |
| `float disconnectInactiveTimeout` | 85 | `60f` | |
| `bool exceptionsDisconnect` | 88 | `true` | 异常即断开（安全默认） |
| `NetworkAuthenticator authenticator` | 92 | — | |
| `string offlineScene` | 98 | `""` | |
| `string onlineScene` | 104 | `""` | |
| `float offlineSceneLoadDelay` | 107 | `0` | |
| `GameObject playerPrefab` | 115 | — | 必须有 `NetworkIdentity` |
| `bool autoCreatePlayer` | 120 | `true` | |
| `PlayerSpawnMethod playerSpawnMethod` | 125 | — | `{ Random, RoundRobin }` |
| `List<GameObject> spawnPrefabs` | 129 | 空 | **动态 spawn 的 prefab 必须登记在这里** |
| `SnapshotInterpolationSettings snapshotSettings` | 136 | — | |
| `ConnectionQualityMethod evaluationMethod` | 140 | — | |
| `float evaluationInterval` | 145 | `3` | |
| `bool timeInterpolationGui` | 148 | `false` | |

只读属性：

```csharp
// :154
public int numPlayers => NetworkServer.connections.Count(kv => kv.Value.identity != null);
// :157
public bool isNetworkActive => NetworkServer.active || NetworkClient.active;
// :806
public static string networkSceneName { get; protected set; } = "";
// :808
public static AsyncOperation loadingSceneAsync;
// :132
public static List<Transform> startPositions = new List<Transform>();
// :133
public static int startPositionIndex;
```

【常见坑】`numPlayers` 是**每次访问都遍历一遍 `connections`**（LINQ `Count` + lambda）。不要每帧读它；要显示人数请自己缓存 + 节流。

### 2.3 启动 / 停止 API

```csharp
// Assets/Mirror/Core/NetworkManager.cs
public void StartServer();          // :333
public void StartClient();          // :403
public void StartClient(Uri uri);   // :434
public void StartHost();            // :457
public void StopHost();             // :581
public void StopServer();           // :589
public void StopClient();           // :631
public virtual void ServerChangeScene(string newSceneName);  // :815
public virtual void OnApplicationQuit();                     // :660
public virtual void ConfigureHeadlessFrameRate();            // :685
public virtual void OnDestroy();                             // :796
```

```csharp
// 最小启动示例
void StartHostNow()  => NetworkManager.singleton.StartHost();
void StartServerNow() => NetworkManager.singleton.StartServer();
void JoinLocalhost() => NetworkManager.singleton.StartClient();   // 用 networkAddress
void JoinUri()       => NetworkManager.singleton.StartClient(new Uri("kcp://192.168.1.20:7777"));
```

【常见坑】`StartHost` 是**异步**的。源码注释（`NetworkManager.cs:467`）明确写：

```
// StartHost is inherently ASYNCHRONOUS (=doesn't finish immediately)
//   Listen
//   ConnectHost
//   if onlineScene:
//       LoadSceneAsync
//       ...
//       FinishLoadSceneHost
//           FinishStartHost
//               SpawnObjects
//               StartHostClient      <= not guaranteed to happen after SpawnObjects if onlineScene is set!
//   else:
//       FinishStartHost
// there is NO WAY to make it synchronous because both LoadSceneAsync
// and LoadScene do not finish loading immediately.
```

**所以：不要在 `StartHost()` 返回后立刻假设玩家已 spawn / 场景已就绪。** 用 `OnStartHost` / `OnServerAddPlayer` / `OnClientSceneChanged` 回调，或者轮询 `NetworkClient.localPlayer != null`。

`StartHost()` 的卫语句（`NetworkManager.cs:459`）：

```csharp
if (NetworkServer.active || NetworkClient.active)
{
    Debug.LogWarning("Server or Client already started.");
    return;
}
```

### 2.4 可重写的回调（完整清单 + 真实签名）

以下签名全部逐条来自 `Assets/Mirror/Core/NetworkManager.cs`。

#### 生命周期

```csharp
public virtual void OnValidate();                  // :177
public virtual void Reset();                       // :205
public virtual void Awake();                       // :223
public virtual void Start();                       // :240
public virtual void Update();                      // :265  ← 重写时必须调用 base.Update()
public virtual void LateUpdate();                  // :271
public virtual void OnApplicationQuit();           // :660
public virtual void ConfigureHeadlessFrameRate();  // :685
public virtual void OnDestroy();                   // :796
```

【常见坑】`Update()` 里源码注释写 `// make sure to call base.Update() when overwriting`（`NetworkManager.cs:264`）。`Update` 里会调 `ApplyConfiguration()`（把 `sendRate` 等推给 `NetworkServer`）。重写不调 base 会导致 Inspector 改的 `sendRate` 不生效。

#### 服务器端

```csharp
public virtual void OnServerConnect(NetworkConnectionToClient conn) { }                          // :1332
public virtual void OnServerDisconnect(NetworkConnectionToClient conn) { … }                     // :1336
public virtual void OnServerReady(NetworkConnectionToClient conn) { … }                          // :1346
public virtual void OnServerAddPlayer(NetworkConnectionToClient conn) { … }                      // :1358
public virtual void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason) { }   // :1378
public virtual void OnServerTransportException(NetworkConnectionToClient conn, Exception exception) { }      // :1381
public virtual void OnServerChangeScene(string newSceneName) { }                                 // :1384
public virtual void OnServerSceneChanged(string sceneName) { }                                   // :1387
```

#### 客户端

```csharp
public virtual void OnClientConnect() { … }                                                      // :1390
public virtual void OnClientDisconnect() { }                                                     // :1408
public virtual void OnClientError(TransportError error, string reason) { }                        // :1411
public virtual void OnClientTransportException(Exception exception) { }                           // :1414
public virtual void OnClientNotReady() { }                                                       // :1417
public virtual void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { }  // :1421
public virtual void OnClientSceneChanged() { … }                                                 // :1427
```

#### 启动 / 停止

```csharp
public virtual void OnStartHost()   { }   // :1446
public virtual void OnStartServer() { }   // :1449
public virtual void OnStartClient() { }   // :1452
public virtual void OnStopServer()  { }   // :1455
public virtual void OnStopClient()  { }   // :1458
public virtual void OnStopHost()    { }   // :1461
```

#### 触发顺序（官方文档原文）

来源：[NetworkManager Callbacks](https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkmanager-callbacks)

**Host 模式**

| 事件 | 顺序 |
|---|---|
| Host 启动 | `OnStartServer` → `OnStartHost` → `OnServerConnect` → `OnStartClient` → `OnClientConnect` → `OnServerSceneChanged` → `OnServerReady` → `OnServerAddPlayer` → `OnClientChangeScene` → `OnClientSceneChanged` |
| 客户端接入 | `OnServerConnect` → `OnServerReady` → `OnServerAddPlayer` |
| 客户端断开 | `OnServerDisconnect` |
| Host 停止 | `OnStopHost` → `OnServerDisconnect` → `OnStopClient` → `OnStopServer` |

**Client 模式**

| 事件 | 顺序 |
|---|---|
| 客户端启动 | `OnStartClient` → `OnClientConnect` → `OnClientChangeScene` → `OnClientSceneChanged` |
| 客户端停止 | `OnStopClient` → `OnClientDisconnect` |

**Server 模式**

| 事件 | 顺序 |
|---|---|
| 服务器启动 | `OnStartServer` → `OnServerSceneChanged` |
| 客户端接入 | `OnServerConnect` → `OnServerReady` → `OnServerAddPlayer` |
| 客户端断开 | `OnServerDisconnect` |
| 服务器停止 | `OnStopServer` |

【常见坑】**Host 停止时 `OnStopHost` 最先触发，`OnStopServer` 最后**。很多人在 `OnStopServer` 里清理「服务器态资源」，但那时客户端态已经拆完了。需要对称清理的写在 `OnStopHost`。

### 2.5 默认实现做了什么（重写前必读）

`OnServerReady`（`NetworkManager.cs:1346`）：

```csharp
public virtual void OnServerReady(NetworkConnectionToClient conn)
{
    if (conn.identity == null) { /* 现在允许 ready 时没有 player 对象 */ }
    NetworkServer.SetClientReady(conn);
}
```

`OnServerAddPlayer`（`NetworkManager.cs:1358`）：

```csharp
public virtual void OnServerAddPlayer(NetworkConnectionToClient conn)
{
    if (conn.identity != null)
    {
        Debug.LogError("There is already a player for this connection.");
        return;
    }

    Transform startPos = GetStartPosition();
    GameObject player = startPos != null
        ? Instantiate(playerPrefab, startPos.position, startPos.rotation)
        : Instantiate(playerPrefab);

    player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
    NetworkServer.AddPlayerForConnection(conn, player);
}
```

【官方 Warning】*When implementing these functions, be sure to take care of the functionality that the default implementations provide. For example, in `OnServerAddPlayer`, the function `NetworkServer.AddPlayer` must be called to activate the player game object for the connection.*（[Network Manager](https://mirror-networking.gitbook.io/docs/manual/components/network-manager)）

**自定义生成玩家的范式**（重写时必须自己调 `AddPlayerForConnection`）：

```csharp
public override void OnServerAddPlayer(NetworkConnectionToClient conn)
{
    Transform start = GetStartPosition();
    GameObject player = Instantiate(playerPrefab, start.position, start.rotation);

    // 本项目：Player prefab 根节点在资产里是未激活的，注入配置后再交给 Mirror
    // （顺序参考 CLAUDE.md：Instantiate 之后、激活之前注入 playerConfig）
    NetworkServer.AddPlayerForConnection(conn, player);   // ← 不能漏
}
```

### 2.6 起始位置

```csharp
// Assets/Mirror/Core/NetworkManager.cs
public static void RegisterStartPosition(Transform start);    // :1106
public static void UnRegisterStartPosition(Transform start);  // :1121
public virtual Transform GetStartPosition();                  // :1128
```

`NetworkStartPosition` 组件会**自动**注册 / 注销（`Assets/Mirror/Core/NetworkStartPosition.cs`）：

```csharp
public class NetworkStartPosition : MonoBehaviour
{
    public void Awake()     => NetworkManager.RegisterStartPosition(transform);
    public void OnDestroy() => NetworkManager.UnRegisterStartPosition(transform);
}
```

```csharp
// 场景里放一个空物体，挂上 NetworkStartPosition 即可
// 代码里注册（例如动态生成的出生点）：
NetworkManager.RegisterStartPosition(myTransform);
```

【官方 Note】官方原文（[Network Start Position](https://mirror-networking.gitbook.io/docs/manual/components/network-start-position)）：
- *The Network Manager will spawn players at (0, 0, 0) by default.*
- *Random（可能两个玩家用同一个点）或 Round Robin（依次用完所有点，超出后循环）。*

【常见坑】`NetworkStartPosition` 用的是 **`Awake`**。如果出生点物体在场景里是**未激活**的，`Awake` 不会跑 → 不会注册。本项目有「prefab 根节点未激活」的约定（见 `CLAUDE.md`），手动往场景里拖出生点时要留意。

### 2.7 NetworkManagerHUD（调试用）

`Assets/Mirror/Core/NetworkManagerHUD.cs`。官方说明见 [Network Manager HUD](https://mirror-networking.gitbook.io/docs/manual/components/network-manager-hud)。

【常见坑】`NetworkManagerHUD` 和 `NetworkDiscoveryHUD` 会**抢屏幕**，上真机前记得去掉或加开关。本项目是触屏 Android，HUD 的按钮很小，仅用于编辑器调试。

---

## 3. NetworkIdentity

### 3.1 类声明

```csharp
// Assets/Mirror/Core/NetworkIdentity.cs:57-60
[DefaultExecutionOrder(-1)]
[AddComponentMenu("Network/Network Identity")]
[HelpURL("https://mirror-networking.gitbook.io/docs/components/network-identity")]
public sealed class NetworkIdentity : MonoBehaviour
```

【重要】`NetworkIdentity` 是 **`sealed`**，不能继承。`[DefaultExecutionOrder(-1)]` 保证它的 `Awake` 早于同物体上的其它组件 —— 这是 Mirror 内部依赖（`NetworkIdentity.cs:55-56` 注释：*let's make sure it's always called before their Awake's*）。

【常见坑 · 与项目约定冲突】本项目 `CLAUDE.md` 要求「核心 Manager 用负 `DefaultExecutionOrder`」。`NetworkIdentity` 用 `-1`。如果你给玩家相关脚本设置更负的 `DefaultExecutionOrder`（比如 `-100`），它会**早于** `NetworkIdentity.Awake` 执行，此时 `netIdentity` 还没赋值，`isServer`/`isClient` 会 NRE。
【官方推荐】**不要在 `Awake` 里做网络判断**，用 `OnStartServer` / `OnStartClient` / `OnStartLocalPlayer`。

### 3.2 状态属性（全部是 `{ get; internal set; }`）

| 成员 | 行号 | 说明 |
|---|---|---|
| `bool isClient` | 74 | 已 spawn 且客户端态。**设为 true 后永不回 false** |
| `bool isServer` | 89 | 已 spawn 且服务器态。**设为 true 后永不回 false** |
| `bool isHost` | 92 | `isServer && isClient` |
| `bool isLocalPlayer` | 106 | 本机玩家对象。**设为 true 后永不回 false** |
| `bool isServerOnly` | 109 | `isServer && !isClient` |
| `bool isClientOnly` | 112 | `isClient && !isServer` |
| `bool isOwned` | 116 | 客户端：本连接拥有该对象 |
| `uint netId` | 131 | 运行时唯一 id |
| `NetworkConnection connectionToServer` | 198 | 仅本地玩家对象有效 |
| `NetworkConnectionToClient connectionToClient` | 201 | 服务器侧：拥有该对象的连接 |
| `NetworkBehaviour[] NetworkBehaviours` | 214 | 私有 setter |
| `Visibility visibility` | 229 | 字段，默认 `Visibility.Default` |
| `bool serverOnly` | 190 | 字段 |
| `bool SpawnedFromInstantiate` | 327 | 私有 setter |

【关键 · 96.11.3 变化】源码注释（`NetworkIdentity.cs:64-73`）明确说明 `isClient` / `isServer` / `isLocalPlayer` **一旦置 true 就永不复位**，且**不依赖 `NetworkServer.active` / `NetworkClient.active`**。原因是为了让 `OnDestroy()` 里还能正确判断端。这是刻意的设计（修复 issue #1475 / #1484 / #2533）。

【常见坑】因此 `isClient == true` 不代表「现在客户端还在跑」。要判断「当前网络是否活着」请用 `NetworkClient.active` / `NetworkServer.active`。

### 3.3 可见性 / 观察者（96.11.3 与旧版差异很大）

```csharp
// Assets/Mirror/Core/NetworkIdentity.cs:24
public enum Visibility { Default, ForceHidden, ForceShown }

// :126-128  —— 注意是 Dictionary，不是 HashSet！
public readonly Dictionary<int, NetworkConnectionToClient> observers =
    new Dictionary<int, NetworkConnectionToClient>();

// :229
public Visibility visibility = Visibility.Default;
```

```csharp
// 让一个「计分板」对象永远对所有人可见（不受 Interest Management 影响）
scoreIdentity.visibility = Visibility.ForceShown;

// 让一个怪物在重生期间对所有人隐藏
monsterIdentity.visibility = Visibility.ForceHidden;
```

【常见坑】旧教程里的 `NetworkIdentity.visible`（bool）在 96.11.3 **已不存在**，取而代之的是 `visibility` 枚举（源码 `[FormerlySerializedAs("visible")]`，`NetworkIdentity.cs:228`）。旧教程里的 `identity.observers` 是 `HashSet<NetworkConnectionToClient>`，现在是 `Dictionary<int, NetworkConnectionToClient>`（key = `connectionId`）。**照抄旧代码会编译不过。**

### 3.4 三个 id 的区别

来源：[IDs](https://mirror-networking.gitbook.io/docs/manual/guides/ids) + 源码。

| id | 类型 | 何时分配 | 用途 |
|---|---|---|---|
| `assetId` | `uint` | 编辑器里，prefab 的 GUID → uint | 客户端据此找到要实例化哪个 prefab |
| `sceneId` | `ulong` | 编辑器 `OnPostProcessScene` | 场景对象的身份（客户端据此把场景里的对象对上号） |
| `netId` | `uint` | 运行时 `OnStartServer` / spawn 时 | 消息寻址；`NetworkServer.spawned` / `NetworkClient.spawned` 的 key |
| `connectionId` | `int` | Transport 分配 | 连接标识。**0 保留给 host 的本地连接** |

```csharp
// Assets/Mirror/Core/NetworkIdentity.cs
public uint assetId { get; internal set; }         // :155   —— 有内部 setter，外部不能改
public ulong sceneId;                              // :136   —— public 字段
public static NetworkIdentity GetSceneIdentity(ulong id);   // :310
public static uint AssetGuidToUint(Guid guid);     // :400
public static void ResetNextNetworkId();           // :316
```

```csharp
// assetId 与 Guid 的换算（项目侧如需自查）
uint id = NetworkIdentity.AssetGuidToUint(new Guid("0123456789abcdef0123456789abcdef"));
```

源码注释（`NetworkIdentity.cs:146-154`）：Mirror 用 `AssetDatabase.AssetPathToGUID` 得到 Guid 字符串（32 字符 → 网络上 64 字节），内部序列化成 uint（16 字节）以省带宽。

【常见坑】`assetId` 的 setter 是 `internal`。**不能在游戏代码里给 `assetId` 赋值**（`NetworkServer.Spawn(obj, assetId, conn)` 这个重载内部会做）。源码里对 `value == 0` 会 `LogError("Can not set AssetId to empty guid …")`。

【常见坑】`sceneId` 是 `public` 字段，能改，但**不要改**。源码 `AssignSceneID()`（`NetworkIdentity.cs:490`）在编辑器里生成并检查重复；如果场景没打开就打包，会抛：

```csharp
throw new InvalidOperationException(
    $"Scene {gameObject.scene.path} needs to be opened and resaved before building, " +
    $"because the scene object {name} has no valid sceneId yet.");
```

（`NetworkIdentity.cs:514`）—— **打包前务必把带 `NetworkIdentity` 的场景打开并保存一次。**

### 3.5 spawn / unspawn / destroy

```csharp
// Assets/Mirror/Core/NetworkServer.cs
public static void Spawn(GameObject obj, NetworkConnectionToClient ownerConnection = null);   // :1704
public static void Spawn(GameObject obj, uint assetId, NetworkConnectionToClient ownerConnection = null); // :1711
public static void UnSpawn(GameObject obj);     // :1902  => UnSpawnInternal(obj, resetState: true)
public static void Destroy(GameObject obj);     // :1909
public static bool SpawnObjects();              // :1621  （把所有场景对象 spawn 出去）
public static void RebuildObservers(NetworkIdentity identity, bool initialize);  // :2021
```

```csharp
// 服务器：动态生成一个敌人
GameObject enemy = Instantiate(enemyPrefab, pos, rot);
NetworkServer.Spawn(enemy);

// 回收
NetworkServer.Destroy(enemy);   // 服务器+客户端一起销毁（推荐）
// NetworkServer.UnSpawn(enemy); // 只取消 spawn，对象留在服务器（可再次 Spawn）
```

`SpawnObject` 内部的校验（`NetworkServer.cs:1717` 起），每条都会 `LogError` 并 return：

| 检查 | 错误信息 |
|---|---|
| 传进来的是 prefab 资产 | `GameObject {obj.name} is a prefab, it can't be spawned. Instantiate it first.` |
| 服务器未启动 | `SpawnObject for {obj}, NetworkServer is not active. Cannot spawn objects without an active server.` |
| 没有 `NetworkIdentity` | `SpawnObject {obj} has no NetworkIdentity. Please add a NetworkIdentity to {obj}` |
| 对象来自 `Instantiate(场景对象)` | 静默 return（`NetworkIdentity.Awake` 已经报过一次错） |
| 同一 `netId` 已 spawn | `{identity.name} [netId={netId}] was already spawned.`（Warning） |

【常见坑 · 最高频】客户端报：

```
Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=… netId=…
```

（`Assets/Mirror/Core/NetworkClient.cs:1272`）

原因：**该 prefab 没有登记进 `NetworkManager.spawnPrefabs`**。
【官方推荐】*In addition to the Player Prefab, you must also register other prefabs that you want to dynamically spawn during game play with the Network Manager.*（[Network Manager](https://mirror-networking.gitbook.io/docs/manual/components/network-manager)）

两种登记方式：

```csharp
// 方式 1：Inspector 的 "Registered Spawnable Prefabs" 列表（等价于 spawnPrefabs）
// 方式 2：代码注册
NetworkClient.RegisterPrefab(myPrefab);                          // NetworkClient.cs:736
NetworkClient.RegisterPrefab(myPrefab, newAssetId);              // :704
NetworkClient.RegisterPrefab(myPrefab, spawnHandler, unspawnHandler);  // :772
```

【常见坑 · DDOL 与 spawnPrefabs】官方原文：*If you have one Network Manager that is persisted through scenes via Don't Destroy On Load (DDOL), you need to register all prefabs to it which might be spawned in any scene. If you have a separate Network Manager in each scene, you only need to register the prefabs relevant for that scene.*（[Network Manager](https://mirror-networking.gitbook.io/docs/manual/components/network-manager)）

【常见坑】`NetworkIdentity.Awake` 会检查 `hasSpawned`（`NetworkIdentity.cs:376`）：

```
{name} has already spawned. Don't call Instantiate for NetworkIdentities that were in the scene
since the beginning (aka scene objects). Otherwise the client won't know which object to use
for a SpawnSceneObject message.
```

即：**场景对象不能 `Instantiate`**。场景里已有的 `NetworkIdentity` 对象由 Mirror 自己 spawn，你只需要 `NetworkServer.SpawnObjects()`（或让 `NetworkManager` 自动做）。

【官方 Danger】*Mirror does not support Network Identities on nested GameObjects. Otherwise, Mirror will emit an error. To avoid this, ensure your parent GameObject is the only GameObject in the stack with a Network Identity. Child GameObjects can access the parents' Network Identity component via Unity's built-in `GetComponentInParent`.*（[Network Identity](https://mirror-networking.gitbook.io/docs/manual/components/network-identity)）

编辑器侧对应检查：`NetworkIdentity.cs` 里的 `DisallowChildNetworkIdentities()`（在 `OnValidate` 中调用，`#if UNITY_EDITOR`）。

### 3.6 玩家对象管理 API

```csharp
// Assets/Mirror/Core/NetworkServer.cs
public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player);              // :1241
public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId); // :1225

public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, bool keepAuthority = false);  // :1291
public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, bool keepAuthority = false); // :1281
public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, ReplacePlayerOptions replacePlayerOptions);  // :1308
public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, ReplacePlayerOptions replacePlayerOptions);  // :1298

public static void RemovePlayerForConnection(NetworkConnectionToClient conn, bool destroyServerObject);   // :1377
public static void RemovePlayerForConnection(NetworkConnectionToClient conn, RemovePlayerOptions removeOptions = RemovePlayerOptions.KeepActive);  // :1386

public static void DestroyPlayerForConnection(NetworkConnectionToClient conn);   // :1073

public static void SetClientReady(NetworkConnectionToClient conn);       // :1416
public static void SetClientNotReady(NetworkConnectionToClient conn);    // :1499
public static void SetAllClientsNotReady();                             // :1510
```

枚举（`NetworkServer.cs:9 / 21`）：

```csharp
public enum ReplacePlayerOptions
{
    KeepAuthority,  // 对象保持激活；不移除 ownership
    KeepActive,     // 对象保持激活；只移除 ownership
    Unspawn,        // 客户端 unspawn，服务器保留
    Destroy         // 服务器+客户端都销毁
}

public enum RemovePlayerOptions
{
    KeepActive,  // 保持激活；只移除 ownership
    Unspawn,     // 客户端 unspawn，服务器保留
    Destroy      // 服务器+客户端都销毁
}
```

【官方推荐】换角色（选人 / 换皮肤）用 `ReplacePlayerForConnection`，不要 `Remove` + `Add`：

```csharp
// 服务器：把玩家的对象换成另一个（保留 connection 与 ownership）
GameObject newPlayer = Instantiate(newCharacterPrefab, pos, rot);
NetworkServer.ReplacePlayerForConnection(conn, newPlayer, ReplacePlayerOptions.KeepAuthority);
```

【常见坑】`AddPlayerForConnection` 对同一 `conn` 调用两次会报 `"There is already a player for this connection."`（见 `OnServerAddPlayer` 默认实现，`NetworkManager.cs:1360`）。

### 3.7 spawn 时序

【官方】*The state of SyncVars is applied to game objects on clients before `OnStartClient()` is called, so the state of the object is always up-to-date inside `OnStartClient()`.*（[SyncVars](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars)）

即客户端回调顺序（`NetworkBehaviour` 侧）：

| 场景 | 顺序 |
|---|---|
| 服务器 spawn | `OnStartServer` → `OnRebuildObservers` → `Start` |
| 客户端（本地玩家） | `OnStartAuthority` → `OnStartClient` → `OnStartLocalPlayer` → `Start` |
| Host（玩家对象） | `OnStartServer` → `OnRebuildObservers` → `OnStartAuthority` → `OnStartClient` → `OnSetHostVisibility` → `OnStartLocalPlayer` → `Start` |

来源：[NetworkBehaviour Callbacks](https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkbehaviour-callbacks)

【官方 Note】*`Start` is called by unity before the first frame, while normally this happens after Mirror's callbacks. But if you don't call `NetworkServer.Spawn` the same frame as `instantiate` then `Start` may be called first.*（同上）

【常见坑】`OnStartClient` 之前 UI 可能还没就绪 —— 反过来说，**`OnStartClient` 里可以放心读 SyncVar 初值，但不一定能安全访问「其它对象」**（别的对象可能还没 spawn 完）。要等所有对象都就绪，用 `ObjectSpawnFinishedMessage` 时机（客户端侧没有公开回调，需自行 `NetworkClient.RegisterHandler<ObjectSpawnFinishedMessage>`，**未核实是否推荐**）。

---

## 4. NetworkBehaviour

### 4.1 类声明与同步配置字段

```csharp
// Assets/Mirror/Core/NetworkBehaviour.cs:30
public abstract class NetworkBehaviour : MonoBehaviour

// :12 / :15 / :24
public enum SyncMethod { Reliable, Hybrid }
public enum SyncMode { Observers, Owner }
public enum SyncDirection { ServerToClient, ClientToServer }

// :33 / :37 / :42 / :54
[HideInInspector] public SyncMethod syncMethod = SyncMethod.Reliable;
[HideInInspector] public SyncDirection syncDirection = SyncDirection.ServerToClient;
[HideInInspector] public SyncMode syncMode = SyncMode.Observers;
[Range(0, 2)] [HideInInspector] public float syncInterval = 0;
```

官方 Tooltip 原文（`NetworkBehaviour.cs:32` / `:36` / `:41` / `:52`）：

- `syncMethod`：*Reliable: only sends when changed. Recommended for most games! Unreliable: immediately sends at the expense of bandwidth. Only for hardcore competitive games.*
- `syncDirection`：*Server Authority calls OnSerialize on the server and syncs it to clients. Client Authority calls OnSerialize on the owning client, syncs it to server, which then broadcasts it to all other clients. Use server authority for cheat safety.*
- `syncMode`：*By default synced data is sent from the server to all Observers of the object. Change this to Owner to only have the server update the client that has ownership authority for this object.*
- `syncInterval`：*Time in seconds until next change is synchronized to the client. '0' means send immediately if changed. '0.5' means only send changes every 500ms. (This is for state synchronization like SyncVars, SyncLists, OnSerialize. Not for Cmds, Rpcs, etc.)*

【官方推荐】`syncInterval` 默认 `0` 是刻意的（`NetworkBehaviour.cs:48-51` 注释）：*NetworkServer & NetworkClient broadcast() are behind a sendInterval timer now. it makes sense to keep every component's syncInterval setting at '0' by default. otherwise, the overlapping timers could introduce unexpected latency.*

### 4.2 属性速查

```csharp
// Assets/Mirror/Core/NetworkBehaviour.cs
public bool isServer      => netIdentity.isServer;       // :60
public bool isClient      => netIdentity.isClient;       // :63
public bool isHost        => isServer && isClient;       // :66
public bool isLocalPlayer => netIdentity.isLocalPlayer;  // :69
public bool isServerOnly  => netIdentity.isServerOnly;   // :72
public bool isClientOnly  => netIdentity.isClientOnly;   // :75
public bool isOwned       => netIdentity.isOwned;        // :79
public bool authority     { get { … } }                  // :96   ← 见 1.2，依赖 syncDirection
public uint netId                      => netIdentity.netId;               // :112
public NetworkConnection connectionToServer => netIdentity.connectionToServer; // :116
public NetworkConnectionToClient connectionToClient => netIdentity.connectionToClient; // :119
public NetworkIdentity netIdentity { get; internal set; }   // :135
public byte ComponentIndex { get; internal set; }           // :138
```

【官方】*`isOwned`（formerly `hasAuthority`）* —— 旧版叫 `hasAuthority`，96.x 已改名（[Network Behaviour](https://mirror-networking.gitbook.io/docs/manual/components/networkbehaviour)）。旧代码里的 `hasAuthority` **在本版本不存在**。

### 4.3 Dirty bit / hook guard API

```csharp
// Assets/Mirror/Core/NetworkBehaviour.cs
protected ulong syncVarDirtyBits;                                       // :153
protected bool GetSyncVarHookGuard(ulong dirtyBit);                     // :206
protected void SetSyncVarHookGuard(ulong dirtyBit, bool value);         // :210
public void SetSyncVarDirtyBit(ulong dirtyBit);                         // :229
public void SetDirty();                                                 // :244  => SetSyncVarDirtyBit(ulong.MaxValue)
public bool IsDirty();                                                  // :249
public bool IsDirty_BitsOnly();                                         // :258
public void ClearAllDirtyBits(bool clearSyncTime = true);               // :263
protected void InitSyncObject(SyncObject syncObject);                   // :280
```

【常见坑】没有 `syncVarHookGuard` **属性**。96.11.3 只有 `GetSyncVarHookGuard` / `SetSyncVarHookGuard`（`protected`）。旧教程里的 `syncVarHookGuard = true;` 编译不过。

【常见坑】`syncVarDirtyBits` 是 `protected` 字段。自己写 `OnSerialize` 时必须手动处理它，否则 SyncVar 不脏 → 不发。

### 4.4 回调（全部 `public virtual void X() {}`）

```csharp
// Assets/Mirror/Core/NetworkBehaviour.cs
public virtual void OnStartServer()      {}   // :1466  服务器 spawn 时 / 服务器启动时对场景对象
public virtual void OnStopServer()       {}   // :1469  服务器销毁或 unspawn 时
public virtual void OnStartClient()      {}   // :1472  客户端 spawn 时（SyncVar 初值已就位）
public virtual void OnStopClient()       {}   // :1475  收到 ObjectDestroyMessage / ObjectHideMessage 时
public virtual void OnStartLocalPlayer() {}   // :1478  本机玩家对象
public virtual void OnStopLocalPlayer()  {}   // :1481
public virtual void OnStartAuthority()   {}   // :1484  spawn 时已有权威，或服务器后来授予
public virtual void OnStopAuthority()    {}   // :1487  权威被移除（例如玩家对象被 replace 但没销毁）
public virtual void OnSerialize(NetworkWriter writer, bool initialState);    // :1225
public virtual void OnDeserialize(NetworkReader reader, bool initialState);  // :1232
protected virtual void SerializeSyncVars(NetworkWriter writer, bool initialState);    // :1261
protected virtual void DeserializeSyncVars(NetworkReader reader, bool initialState);  // :1273
public virtual bool Weaved() => false;                                       // :1492
```

【官方 Warning】*`OnSerialize`: called when behaviour is serialize before it is sent to client, when overriding make sure to call `base.OnSerialize`.*（[NetworkBehaviour Callbacks](https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkbehaviour-callbacks)）

【关键 · 96.11.3 变化】`OnRebuildObservers` / `OnCheckObserver` / `OnSetHostVisibility` **已经不在 `NetworkBehaviour` 上**了。
官方原文：*Note: `OnRebuildObservers` and `OnSetHostVisibility` is now on `NetworkVisibility` instead of `NetworkBehaviour`.*（[NetworkBehaviour Callbacks](https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkbehaviour-callbacks)）
实际在本版本它们位于 **`InterestManagement` / `InterestManagementBase`** 组件上（见第 10 章），而 `NetworkVisibility` 这个类**在本地源码中不存在**（文档措辞过时）。

【官方 Danger】*Do not put objects in `DontDestroyOnLoad` (DDOL) in `Awake`. You can do that in `Start` instead.*（[Network Behaviour](https://mirror-networking.gitbook.io/docs/manual/components/networkbehaviour)）

### 4.5 一行范式

```csharp
public class PlayerController : NetworkBehaviour
{
    public override void OnStartServer()      { /* 只跑在服务器：初始化权威状态 */ }
    public override void OnStartClient()      { /* SyncVar 初值已就位，可刷 UI */ }
    public override void OnStartLocalPlayer() { /* 只跑在本机玩家：装相机、开输入 */ }
    public override void OnStopLocalPlayer()  { /* 卸输入 */ }
    public override void OnStartAuthority()   { /* 拿到权威（捡到道具等） */ }
    public override void OnStopAuthority()    { /* 失去权威 */ }
}
```

---

## 5. 远程调用（Command / ClientRpc / TargetRpc / 端限制属性）

### 5.1 属性真实定义

全部来自 `Assets/Mirror/Core/Attributes.cs`：

```csharp
[AttributeUsage(AttributeTargets.Field)]
public class SyncVarAttribute : PropertyAttribute { public string hook; }        // :15-19

[AttributeUsage(AttributeTargets.Method)]
public class CommandAttribute : Attribute                                        // :25-30
{
    public int channel = Channels.Reliable;
    public bool requiresAuthority = true;
}

[AttributeUsage(AttributeTargets.Method)]
public class ClientRpcAttribute : Attribute                                      // :35-39
{
    public int channel = Channels.Reliable;
    public bool includeOwner = true;
}

[AttributeUsage(AttributeTargets.Method)]
public class TargetRpcAttribute : Attribute                                      // :45-49
{
    public int channel = Channels.Reliable;
}

public class ServerAttribute : Attribute {}          // :55
public class ServerCallbackAttribute : Attribute {}  // :62
public class ClientAttribute : Attribute {}          // :69
public class ClientCallbackAttribute : Attribute {}  // :76
public class SceneAttribute : PropertyAttribute {}   // :81
public class ShowInInspectorAttribute : Attribute {} // :88
public class ReadOnlyAttribute : PropertyAttribute {}// :94
public class WeaverPriorityAttribute : Attribute {}  // :100
```

【重要 · 96.11.3 没有 `[SyncObject]` 属性】**本地 `Attributes.cs` 里不存在 `SyncObjectAttribute`。** 自定义同步容器请用 `InitSyncObject()`（见 6.4）。任务清单里提到的 `[SyncObject]` 在本版本**不可用**。

【常见坑】`ClientRpcAttribute` **没有** `requiresAuthority` 参数；`TargetRpcAttribute` **只有** `channel`。别照抄记忆里的签名。

### 5.2 端限制属性 `[Server]` / `[Client]` 等

官方定义（[Attributes](https://mirror-networking.gitbook.io/docs/manual/guides/attributes)）：

| 属性 | 行为 |
|---|---|
| `[Server]` | 只有服务器能调用；客户端调用会**打 warning** |
| `[ServerCallback]` | 同 `[Server]`，但客户端调用**不打 warning**（静默 return） |
| `[Client]` | 只有客户端能调用；服务器调用会**打 warning** |
| `[ClientCallback]` | 同 `[Client]`，但服务器调用**不打 warning**（静默 return） |

【官方 Note】*when using abstract or virtual methods the Attributes need to be applied to the override methods too.*（[Attributes](https://mirror-networking.gitbook.io/docs/manual/guides/attributes)）

```csharp
// 只跑在服务器的 Update（客户端上静默跳过，不刷日志）
[ServerCallback]
void Update()
{
    if (!NetworkServer.active) return;   // 官方在 InterestManagement 里也这么写
}

// 只有服务器能调用的方法；客户端误调会打 warning
[Server]
void ApplyDamage(int amount) { }
```

【官方】*The `[Server]` and `[Client]` attributes do not generate compile time errors, but they do emit a warning log message if called in the wrong context.*（[Network Behaviour](https://mirror-networking.gitbook.io/docs/manual/components/networkbehaviour)）

【常见坑 · 性能】`[Server]` / `[Client]` 在错误端会**每次调用都打一条 warning**。放在 `Update` 里就是每帧一条日志 → Android 上直接卡死。
**逐帧路径一律用 `[ServerCallback]` / `[ClientCallback]`**，并自己加 `if (!isServer) return;`。

### 5.3 `[Command]`

官方定义（[Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions)）：

> Commands are sent from player objects on the client to player objects on the server. For security, Commands can only be sent from YOUR player object by default, so you cannot control the objects of other players. You can bypass the authority check using `[Command(requiresAuthority = false)]`.

```csharp
[Command]
void CmdDropCube() { /* 跑在服务器 */ }
```

【官方推荐】命名约定：**`Cmd` 前缀**；*Commands functions should have the prefix "Cmd" and cannot be static.*（[Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions)）

**能否调用 Command 的三条例外**（官方原文）：

> It is possible to invoke commands on non-player objects if any of the following are true:
> * The object was spawned with client authority
> * The object has client authority set with `NetworkIdentity.AssignClientAuthority`
> * the Command has the `requiresAuthority` option set false.

**`NetworkConnectionToClient` 参数注入**（官方原文）：

> * You can include an optional `NetworkConnectionToClient sender = null` parameter in the Command method signature and Mirror will fill in the sending client for you.
> * **Do not try to set a value for this optional parameter...it will be ignored.**

```csharp
public enum DoorState : byte { Open, Closed, Locked }

public class Door : NetworkBehaviour
{
    [SyncVar] public DoorState doorState;

    [Command(requiresAuthority = false)]
    public void CmdSetDoorState(NetworkConnectionToClient sender = null)
    {
        // sender 由 Mirror 注入，调用方传什么都会被忽略
        bool hasKey = sender.identity.GetComponent<PlayerState>().hasDoorKey;
        doorState = hasKey ? DoorState.Open : DoorState.Closed;
    }
}
```

**channel 参数**：

```csharp
[Command(channel = Channels.Unreliable)]   // 允许丢包（高频输入，例如每帧朝向）
void CmdAim(Vector2 dir) { }
```

【官方 Warning】*Be careful of sending commands from the client every frame! This can cause a lot of network traffic.*（[Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions)）

【官方 · 调用者校验规则】
- 客户端调用 `[Command]`：**默认必须拥有该对象**（`requiresAuthority = true`）。不满足时 Mirror 侧不会执行，会走「无权限」分支（本地源码 `NetworkBehaviour.SendCommandInternal`，`NetworkBehaviour.cs:367`）。
- **服务器端调用 `[Command]` 方法本身是无效的** —— 官方原文：*It's not possible to call this from a server. Use this as a wrapper around another function, if you want to call it from the server too.*（[Attributes](https://mirror-networking.gitbook.io/docs/manual/guides/attributes)）

```csharp
// 想在两端都能触发的正确写法：Command 只做包装
public void DropCube()
{
    if (isServer) DropCubeOnServer();     // 服务器直接调
    else          CmdDropCube();          // 客户端走 Command
}

[Command] void CmdDropCube() => DropCubeOnServer();
void DropCubeOnServer() { /* 真正逻辑 */ }
```

> 注：`SendCommandInternal` 在「无权限」时是**静默失败还是打日志**，需读 weaver 生成的代码才能确认，本版本**未核实**。保守做法：不要依赖静默失败，客户端自己先判 `isOwned`。

### 5.4 `[ClientRpc]`

官方原文：

> ClientRpc calls are sent from objects on the server to objects on clients. They can be sent from any server object with a NetworkIdentity that has been spawned. Since the server has authority, then there no security issues with server objects being able to send these calls.

```csharp
[ClientRpc]
public void RpcDamage(int amount) { /* 跑在所有客户端 */ }
```

【官方推荐】命名约定：**`Rpc` 前缀**，不能 static。

**Host 模式行为**（官方原文）：

> When running a game as a host with a local client, ClientRpc calls will be invoked on the local client even though it is in the same process as the server. So the behaviours of local and remote clients are the same for ClientRpc calls.

即：**host 上 `Rpc` 也会在本机执行一遍**。不要写「我是服务器所以我不执行 Rpc」的假设。

**排除 owner**（官方原文）：

> ClientRpc messages are only sent to observers of an object according to its Network Visibility. Player objects are always observers of themselves. In some cases, you may want to exclude the owner client when calling a ClientRpc. This is done with the `includeOwner` option: `[ClientRpc(includeOwner = false)]`.

```csharp
[ClientRpc(includeOwner = false)]   // 不发给拥有者（例如「别人看你受伤」的表现）
void RpcOtherPlayersSeeHit() { }
```

### 5.5 `[TargetRpc]`

官方原文：

> **Context Matters:**
> * If the first parameter of your TargetRpc method is a `NetworkConnection` then that's the connection that will receive the message regardless of context.
> * If the first parameter is any other type, then the owner client of the object with the script containing your TargetRpc will receive the message.

```csharp
public class Player : NetworkBehaviour
{
    [Command]
    void CmdMagic(GameObject target, int damage)
    {
        target.GetComponent<Player>().health -= damage;
        NetworkIdentity opponentIdentity = target.GetComponent<NetworkIdentity>();
        TargetDoMagic(opponentIdentity.connectionToClient, damage);   // 显式指定连接
    }

    [TargetRpc]
    public void TargetDoMagic(NetworkConnectionToClient target, int damage)
    {
        // 只在「被打的那个人」的客户端执行
        Debug.Log($"Magic Damage = {damage}");
    }

    [Command]
    public void CmdHealMe()
    {
        health += 10;
        TargetHealed(10);      // 没有 NetworkConnection 参数 → 发给 owner
    }

    [TargetRpc]
    public void TargetHealed(int amount) { Debug.Log($"Health increased by {amount}"); }
}
```

（示例来自 [Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions)）

【官方推荐】命名约定：**`Target` 前缀**，不能 static。

### 5.6 参数限制

【官方原文】*Arguments to remote actions cannot be sub-components of game objects, such as script instances or Transforms.*（[Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions)）

也就是说：

```csharp
[Command] void CmdBad(Transform t) { }        // ❌ Transform 不支持
[Command] void CmdBad2(MyScript s) { }        // ❌ 脚本实例不支持
[Command] void CmdGood(GameObject go) { }     // ✅ GameObject（必须已 spawn 且有 NetworkIdentity）
[Command] void CmdGood2(uint netId) { }       // ✅ 更稳：传 netId 自己查
[Command] void CmdGood3(Vector2 dir, int id) { }  // ✅ 基础类型
```

【常见坑】`NetworkIdentity` / `NetworkBehaviour` / `GameObject` 作为 RPC 参数时，**对端如果还没 spawn 该对象，会收到 `null`**（[Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)）。

【官方推荐】*These should not be used in SyncVars or Sync\* Collections or Rpc's because they'll be null on the client if the corresponding object hasn't already been spawned.*（[Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)）

**更稳的写法**：传 `netId`（`uint`），自己查 `NetworkClient.spawned`：

```csharp
[SyncVar(hook = nameof(OnTargetChanged))]
public uint targetNetId;

void OnTargetChanged(uint _, uint newValue)
{
    target = NetworkClient.spawned.TryGetValue(targetNetId, out NetworkIdentity id)
        ? id.gameObject
        : null;
}
```

（范式来自 [Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)）

### 5.7 RPC 注册表（了解即可）

```csharp
// Assets/Mirror/Core/RemoteCalls.cs
public enum RemoteCallType { Command, ClientRpc }    // :8
public delegate void RemoteCallDelegate(NetworkBehaviour obj, NetworkReader reader, NetworkConnectionToClient senderConnection);  // :11
public static class RemoteProcedureCalls { … }       // :31
```

```csharp
// Assets/Mirror/Core/NetworkIdentity.cs:262
internal void HandleRemoteCall(byte componentIndex, ushort functionHash, RemoteCallType remoteCallType,
                               NetworkReader reader, NetworkConnectionToClient senderConnection = null);
```

`HandleRemoteCall` 是 `internal` —— 应用层不需要碰。RPC 的分发由 Weaver 生成的代码 + `RemoteProcedureCalls` 自动完成。

---

## 6. 状态同步（SyncVar / Sync 集合 / SyncObject）

### 6.1 SyncVar 基础

官方原文（[SyncVars](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars)）：

> SyncVars are properties of classes that inherit from NetworkBehaviour, which are synchronized from the server to clients. When a game object is spawned, or a new player joins a game in progress, they are sent the latest state of all SyncVars on networked objects that are visible to them.

**四条必须记住的官方结论**：

1. *The state of SyncVars is applied to game objects on clients **before** `OnStartClient()` is called, so the state of the object is always up-to-date inside `OnStartClient()`.*
2. *SyncVars can use any type supported by Mirror. **You can have up to 64 SyncVars on a single NetworkBehaviour script, including SyncLists**.*
3. *The server automatically sends SyncVar updates when the value of a SyncVar changes, so you do not need to track when they change or send information about the changes yourself. **Changing a value in the inspector will not trigger an update.***
4. *Don't assign them from a client, it's pointless. **Don't let them be null, you will get errors**.*（[Attributes](https://mirror-networking.gitbook.io/docs/manual/guides/attributes)）

```csharp
public class Enemy : NetworkBehaviour
{
    [SyncVar] public int health = 100;   // 初值在客户端 OnStartClient 前就已就位
}
```

【常见坑】`64` 这个上限来自 `NetworkIdentity` 的 dirty mask 设计（`NetworkIdentity.cs:216-219`）：

```csharp
// to save bandwidth, we send one 64 bit dirty mask
// instead of 1 byte index per dirty component.
// which means we can't allow > 64 components (it's enough).
const int MaxNetworkBehaviours = 64;
```

注意是「**每个 `NetworkBehaviour` 组件**最多 64 个 SyncVar（含 SyncList）」，且**每个 `NetworkIdentity` 最多 64 个 `NetworkBehaviour`**。

### 6.2 SyncVar hook

官方原文（[SyncVar Hooks](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvar-hooks)）：

> * The Hook method must have **two parameters** of the same type as the SyncVar property. One for the old value, one for the new value.
> * The Hook is always called **after** the property value is set. You don't need to set it yourself.
> * The Hook only fires for **changed** values, and changing a value in the inspector will not trigger an update.
> * As of version 11.1.4 (March 2020) and later, hooks can be **virtual** methods and overriden in a derived class.

```csharp
[SyncVar(hook = nameof(OnHealthChanged))]
int health = 100;

void OnHealthChanged(int oldHealth, int newHealth)
{
    healthBar.fillAmount = newHealth / 100f;
}
```

**Hook 调用顺序**（官方原文）：

> Hooks are invoked in the order the syncvars are defined in the file.

官方例子：`X`（无 hook）、`Y`（hook1）、`Z`（hook2）同时被服务器改动，顺序是：

```
1. X value is set
2. Y value is set
3. Hook1 is called
4. Z value is set
5. Hook2 is called
```

【常见坑】hook **只在客户端（接收侧）触发**，服务器改值时不触发（源码 `Attributes.cs` 的 XML 注释：*Notice that the hook method will not be called on the change side*）。所以「服务器本地也要更新 UI」的情况必须自己再调一次。

【常见坑】`[SyncVar]` 是 `PropertyAttribute`（`Attributes.cs:15`），**只能打在字段上**（`[AttributeUsage(AttributeTargets.Field)]`），不能打属性、不能 static。

**hook guard 范式**（防止 hook 里回写 SyncVar 造成循环）：

```csharp
[SyncVar(hook = nameof(OnColorChanged))]
Color color = Color.white;

void OnColorChanged(Color oldC, Color newC)
{
    if (GetSyncVarHookGuard(1UL)) return;   // 96.11.3：没有 syncVarHookGuard 属性，用这两个方法
    SetSyncVarHookGuard(1UL, true);
    // …做一些会改到 SyncVar 的事…
    SetSyncVarHookGuard(1UL, false);
}
```

### 6.3 Sync 集合

#### SyncList

```csharp
// Assets/Mirror/Core/SyncList.cs
public class SyncList<T> : SyncObject, IList<T>, IReadOnlyList<T>    // :7
public enum Operation : byte { … }                                    // :24  OP_ADD/OP_INSERT/OP_SET/OP_REMOVEAT/OP_CLEAR

public Action<int> OnAdd;                 // :10
public Action<int> OnInsert;              // :13
public Action<int, T> OnSet;              // :16
public Action<int, T> OnRemove;           // :19
public Action OnClear;                    // :22
public Action<Operation, int, T> OnChange;// :40
public Action<Operation, int, T, T> Callback;  // :48

public SyncList();                                                    // :75
public SyncList(IEqualityComparer<T> comparer);                       // :77
public SyncList(IList<T> objects, IEqualityComparer<T> comparer = null);  // :83
```

【官方 Info】*SyncList must be declared **readonly** and initialized in the constructor.*（[SyncLists](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/synclists)）

【官方 Warning】*Note that by the time you wire up the Action handlers, the list will already be initialized, so they will not get invoked for the initial data, only updates.*（同上）

```csharp
public class Inventory : NetworkBehaviour
{
    public readonly SyncList<string> namesList = new SyncList<string>();

    public override void OnStartClient()
    {
        namesList.OnAdd    += OnItemAdded;
        namesList.OnInsert += OnItemInserted;
        namesList.OnSet    += OnItemChanged;
        namesList.OnRemove += OnItemRemoved;
        namesList.OnClear  += OnListCleared;

        // 官方：初值在挂 handler 之前就已经填好了，需要手动补一次
        for (int i = 0; i < namesList.Count; i++)
            namesList.OnAdd.Invoke(i);
    }

    public override void OnStopClient()
    {
        namesList.OnAdd    -= OnItemAdded;
        namesList.OnInsert -= OnItemInserted;
        namesList.OnSet    -= OnItemChanged;
        namesList.OnRemove -= OnItemRemoved;
        namesList.OnClear  -= OnListCleared;
    }

    void OnItemAdded(int index)                     => Debug.Log($"add {namesList[index]}");
    void OnItemInserted(int index)                  => Debug.Log($"insert {namesList[index]}");
    void OnItemChanged(int index, string oldValue)  => Debug.Log($"set {index}: {oldValue} -> {namesList[index]}");
    void OnItemRemoved(int index, string oldValue)  => Debug.Log($"remove {index}: {oldValue}");
    void OnListCleared()                            { foreach (var n in namesList) Debug.Log($"clear {n}"); }
}
```

【官方 Note】*`OnChange` is a catch-all event … It is called after the specific events above. **Strongly recommended to use the specific events above instead!***（[SyncLists](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/synclists)）

`OnChange` 的 value 语义（官方原文）：

| Operation | value 参数 |
|---|---|
| `OP_ADD` | **新**值 |
| `OP_INSERT` | **新**值 |
| `OP_SET` | **旧**值 |
| `OP_REMOVEAT` | **旧**值 |
| `OP_CLEAR` | `null` / default（此时可以遍历 list） |

【常见坑】`SyncList<T>` 实现了 `IReadOnlyList<T>`。本项目 `CLAUDE.md` 的性能红线里写「循环里不要 `foreach` 遍历接口类型集合（装箱枚举器）」。`SyncList<T>` 有自己的 `public Enumerator GetEnumerator()`（`SyncList.cs:480`），直接 `foreach (var x in syncList)` 走的是**结构体枚举器，不装箱**；但 `foreach (var x in (IReadOnlyList<T>)syncList)` 会装箱。逐帧遍历请用 `for (int i = 0; i < list.Count; i++)`。

#### SyncDictionary

```csharp
// Assets/Mirror/Core/SyncDictionary.cs
public class SyncIDictionary<TKey, TValue> : SyncObject,
        IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>    // :7
{
    public Action<TKey> OnAdd;                    // :10
    public Action<TKey, TValue> OnSet;            // :13
    public Action<TKey, TValue> OnRemove;         // :16
    public Action OnClear;                        // :19
    public Action<Operation, TKey, TValue> OnChange;  // :35
}

public class SyncDictionary<TKey, TValue> : SyncIDictionary<TKey, TValue>   // :421
{
    public SyncDictionary();                                   // :423
    public SyncDictionary(IEqualityComparer<TKey> eq);         // :424
    public SyncDictionary(IDictionary<TKey, TValue> d);        // :425
}
```

```csharp
public readonly SyncDictionary<int, float> cooldowns = new SyncDictionary<int, float>();
```

#### SyncSet / SyncHashSet / SyncSortedSet

```csharp
// Assets/Mirror/Core/SyncSet.cs
public class SyncSet<T> : SyncObject, ISet<T>       // :7
public class SyncHashSet<T> : SyncSet<T>            // :400
public class SyncSortedSet<T> : SyncSet<T>          // :409
```

```csharp
public readonly SyncHashSet<string> tags = new SyncHashSet<string>();
```

#### 集合支持的类型限制

【官方】*Arrays / ArraySegments of any of the above — **Not supported with Sync\* collections***（[Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)）

即：`SyncList<int[]>`、`SyncList<ArraySegment<byte>>` **不支持**。

### 6.4 SyncObject（自定义同步容器）

```csharp
// Assets/Mirror/Core/SyncObject.cs
public abstract class SyncObject                       // :13
{
    internal NetworkBehaviour networkBehaviour;        // :16   ← internal，外部拿不到
    public Action OnDirty;                             // :19
    public Func<bool> IsRecording = () => true;        // :29
    public Func<bool> IsWritable  = () => true;        // :35

    public abstract void ClearChanges();               // :39
    public abstract void OnSerializeAll(NetworkWriter writer);      // :42
    public abstract void OnSerializeDelta(NetworkWriter writer);    // :45
    public abstract void OnDeserializeAll(NetworkReader reader);    // :48
    public abstract void OnDeserializeDelta(NetworkReader reader);  // :51
    public abstract void Reset();                      // :54
}
```

**注册方式：`InitSyncObject()`，不是 `[SyncObject]` 属性。**

```csharp
// Assets/Mirror/Core/NetworkBehaviour.cs:280
protected void InitSyncObject(SyncObject syncObject);
```

```csharp
public class MyRingBuffer : SyncObject
{
    readonly List<int> data = new List<int>();

    public override void ClearChanges() { }
    public override void OnSerializeAll(NetworkWriter writer)
    {
        writer.WriteInt(data.Count);
        foreach (int v in data) writer.WriteInt(v);
    }
    public override void OnSerializeDelta(NetworkWriter writer) => OnSerializeAll(writer);
    public override void OnDeserializeAll(NetworkReader reader)
    {
        data.Clear();
        int count = reader.ReadInt();
        for (int i = 0; i < count; i++) data.Add(reader.ReadInt());
    }
    public override void OnDeserializeDelta(NetworkReader reader) => OnDeserializeAll(reader);
    public override void Reset() => data.Clear();
}

public class Holder : NetworkBehaviour
{
    public readonly MyRingBuffer buffer = new MyRingBuffer();

    void Awake() => InitSyncObject(buffer);   // ← 唯一的注册入口
}
```

【常见坑】`SyncObject.networkBehaviour` 是 `internal`，且 `InitSyncObject` 是 `protected`。所以 `SyncObject` 子类**必须在 `NetworkBehaviour` 子类的实例方法里注册**（通常在 `Awake` 或字段初始化后的第一个回调）。

### 6.5 服务器 / 客户端回调时序总表

| 回调 | 触发时机（官方原文） |
|---|---|
| `OnStartServer` | *called when behaviour is spawned on server* |
| `OnStopServer` | *called when behaviour is destroyed or unspawned on server* |
| `OnSerialize` | *called when behaviour is serialize before it is sent to client*（重写要调 `base`） |
| `OnStartClient` | *called when behaviour is spawned on client* |
| `OnStartAuthority` | *called when behaviour has authority when it is spawned (eg local player)* / *called when behaviour is given authority by the sever* |
| `OnStartLocalPlayer` | *called when the behaviour is on the local player object* |
| `OnStopAuthority` | *called when authority is taken from the object (eg local player is replaced but not destroyed)* |
| `OnStopClient` | *called when object is destroyed on client by the `ObjectDestroyMessage` or `ObjectHideMessage` messages* |

来源：[NetworkBehaviour Callbacks](https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkbehaviour-callbacks)

【官方 Note】`OnStopLocalPlayer`：*called on clients before `OnStopClient` for the player game object on the local client*（[Network Behaviour](https://mirror-networking.gitbook.io/docs/manual/components/networkbehaviour)）

---

## 7. 序列化与 Weaver

### 7.1 Weaver 是什么

【官方原文】*Mirror creates Serialize and Deserialize functions for types using **Weaver**. Weaver edits dll after unity compiles them using **Mono.Cecil**. This allows mirror to have a lot of complex features like SyncVar, ClientRpc and Message Serialization without the user needed to manually set everything up.*（[Serialization](https://mirror-networking.gitbook.io/docs/manual/guides/serialization)）

```csharp
// Assets/Mirror/Core/WeaverFuse.cs
public static class WeaverFuse
{
    // 源码里返回 false；Weaver 在织入成功后会把 IL 改成 true
    public static bool Weaved() =>
#if UNITY_2020_3_OR_NEWER
        false;
#else
        true;
#endif
}
```

【常见坑】看到 `WeaverFuse.Weaved()` 源码返回 `false` 是**正常的** —— 那是「织入前的值」。运行时应为 `true`。若运行时拿到 `false`，说明 Weaver 根本没跑起来（源码注释：*otherwise running server/client would give lots of random 'writer not found' etc. errors*）。

### 7.2 支持的类型

**内置读写函数**（`Assets/Mirror/Core/NetworkWriterExtensions.cs` / `NetworkReaderExtensions.cs`）—— 实测本版本：

**Write（75 个）**：`Array`、`ArraySegment`、`ArraySegmentAndSize`、`Bool`、`BoolNullable`、`Byte`、`ByteNullable`、`BytesAndSize`、`Char`、`CharNullable`、`Color`、`Color32`、`Color32Nullable`、`ColorNullable`、`DateTime`、`DateTimeNullable`、`Decimal`、`DecimalNullable`、`Double`、`DoubleNullable`、`Float`、`FloatNullable`、`GameObject`、`Guid`、`GuidNullable`、`Half`、`HashSet`、`Int`、`IntNullable`、`LayerMask`、`LayerMaskNullable`、`List`、`Long`、`LongNullable`、`Matrix4x4`、`Matrix4x4Nullable`、`NetworkBehaviour`、`NetworkIdentity`、`Plane`、`PlaneNullable`、`Quaternion`、`QuaternionNullable`、`Ray`、`RayNullable`、`Rect`、`RectNullable`、`SByte`、`SByteNullable`、`Short`、`ShortNullable`、`Sprite`、`String`、`Texture2D`、`Transform`、`UInt`、`UIntNullable`、`ULong`、`ULongNullable`、`Uri`、`UShort`、`UShortNullable`、`VarInt`、`VarLong`、`VarUInt`、`VarULong`、`Vector2`、`Vector2Int`、`Vector2IntNullable`、`Vector2Nullable`、`Vector3`、`Vector3Int`、`Vector3IntNullable`、`Vector3Nullable`、`Vector4`、`Vector4Nullable`

**Read（49 个）**：`ReadArraySegmentAndSize`、`ReadBool`、`ReadByte`、`ReadBytes`、`ReadBytesAndSize`、`ReadChar`、`ReadColor`、`ReadColor32`、`ReadDateTime`、`ReadDecimal`、`ReadDouble`、`ReadFloat`、`ReadGameObject`、`ReadGuid`、`ReadHalf`、`ReadHashSet`、`ReadInt`、`ReadVarInt`、`ReadLayerMask`、`ReadList`、`ReadLong`、`ReadVarLong`、`ReadMatrix4x4`、`ReadNetworkBehaviour`、`ReadNetworkBehaviourSyncVar`、`ReadNetworkIdentity`、`ReadPlane`、`ReadQuaternion`、`ReadRay`、`ReadRect`、`ReadSByte`、`ReadShort`、`ReadSprite`、`ReadString`、`ReadArray`、`ReadTexture2D`、`ReadTransform`、`ReadUInt`、`ReadVarUInt`、`ReadULong`、`ReadVarULong`、`ReadUri`、`ReadUShort`、`ReadVector2`、`ReadVector2Int`、`ReadVector3`、`ReadVector3Int`、`ReadVector4`

【重要 · 96.11.3 变化】**没有 `WritePackedInt32` / `ReadPackedInt32`**。压缩整数叫 **`WriteVarInt` / `ReadVarInt`**（源码 `NetworkWriterExtensions.cs:46`：`[WeaverPriority] public static void WriteVarInt(this NetworkWriter writer, int value) => Compression.CompressVarInt(writer, value);`）。

【常见坑】官方文档 [Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types) 的自定义序列化示例里用的是 `writer.WritePackedInt32(...)` / `reader.ReadPackedInt32()` —— **这些 API 在 96.11.3 不存在**，照抄会编译失败。请改成 `WriteVarInt` / `ReadVarInt`。

### 7.3 Weaver 自动生成读写的类型

【官方原文】（[Serialization](https://mirror-networking.gitbook.io/docs/manual/guides/serialization)）：

Weaver will Generate Read Write functions for:
- Classes or Structs
- Enums
- Arrays（例如 `int[]`）
- ArraySegments
- Lists

**Classes and Structs**：*Weaver will Read/Write **every public field** in the type, unless the field is marked with `[System.NonSerialized]`. If there is an unsupported type in the class or struct Weaver will fail to make Read/Write functions for it.*
【官方 Note】*Weaver does not check properties.*

**Enums**：*Weaver will use the **underlying Type** of an enum to Read and Write them. By default this is `int`.*

```csharp
public enum Switch : byte { Left, Middle, Right }   // 用 byte 读写
```

**Collections**：*Weaver will use the elements Read/Write function. The element must have a Read/Write function so must be a supported type, or have a custom Read/Write function.*

### 7.4 不支持的类型

【官方原文】*NOTE: Types in this list can have custom writers.*

- Jagged and Multidimensional array（`int[][]`、`int[,]`）
- Types that Inherit from `UnityEngine.Component`
- `UnityEngine.Object`
- `UnityEngine.ScriptableObject`
- Generic Types（`MyData<T>`）—— *Custom Read/Write must declare T*
- Interfaces
- Types that references themselves（自引用类型）

【官方】*If you have a type that has a field that is not able to be Serialize, you can mark that field with `[System.NonSerialized]` and weaver will ignore it.*

### 7.5 自定义 Writer / Reader

【官方原文】Read Write functions are static methods in the form of:

```csharp
public static void WriteMyType(this NetworkWriter writer, MyType value)
{
    // write MyType data here
}

public static MyType ReadMyType(this NetworkReader reader)
{
    // read MyType data here
}
```

【官方推荐】*It is **best practice** to make Read/Write functions **extension methods** so they can be called like `writer.WriteMyType(value)`. It is a good idea to call them `ReadMyType` and `WriteMyType` … However the name of the function doesn't matter, weaver should be able to find it no matter what it is called.*（[Serialization](https://mirror-networking.gitbook.io/docs/manual/guides/serialization)）

**本项目可用的一行范式**（例如给 `DateTime` 加支持）：

```csharp
public static class MirrorCustomReadWrite
{
    public static void WriteDateTime(this NetworkWriter writer, DateTime dt) => writer.WriteLong(dt.Ticks);
    public static DateTime ReadDateTime(this NetworkReader reader) => new DateTime(reader.ReadLong());
}
```

**给不支持的 Component 类型加支持**（官方例子，`Rigidbody`）：

```csharp
public static void WriteRigidbody(this NetworkWriter writer, Rigidbody rigidbody)
{
    NetworkIdentity networkIdentity = rigidbody.GetComponent<NetworkIdentity>();
    writer.WriteNetworkIdentity(networkIdentity);
}

public static Rigidbody ReadRigidbody(this NetworkReader reader)
{
    NetworkIdentity networkIdentity = reader.ReadNetworkIdentity();
    return networkIdentity != null ? networkIdentity.GetComponent<Rigidbody>() : null;
}
```

**ScriptableObject 的推荐做法**（【官方推荐】只传 name，对端查表）：

```csharp
public static void WriteArmor(this NetworkWriter writer, Armor armor) => writer.WriteString(armor.name);
public static Armor ReadArmor(this NetworkReader reader) => Resources.Load<Armor>(reader.ReadString());
```

官方理由（[Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)）：*Scriptable objects often reference other assets such as textures, prefabs, or other types that can't be serialized. … Scriptable objects sometimes have a large amount of data in them. The generated reader and writers may not work or may be inneficient for these situations.*

> ⚠️ **本项目特别提示**：`CLAUDE.md` 规定「资源一律走 Addressables，禁止硬编码地址字符串」。因此上面官方例子里的 `Resources.Load` **不要照抄**。本项目的 `BaseEntitySO` 等资产应传 **稳定 `id`**（见 `Assets/EntityIdCatalog.csv`），对端用 id 反查。

**继承与多态**（【官方 Warning】*This code does not work out of the box.*）：

```csharp
[Command]
void CmdEquip(Item item)
{
    // IMPORTANT: this does not work. Mirror will pass you an object of type Item
    // even if you pass a weapon or an armor.
    if (item is Weapon weapon) { … }   // 永远进不来
}
```

【官方原文】*Mirror does not serialize the type name to keep messages small and for security reasons, therefore Mirror cannot figure out the type of object it received by looking at the message.*（[Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)）

解决：给基类写自定义读写，**自己写一个 type tag**：

```csharp
public static class ItemSerializer
{
    const byte WEAPON = 1;
    const byte ARMOR  = 2;

    public static void WriteItem(this NetworkWriter writer, Item item)
    {
        if (item is Weapon w)      { writer.WriteByte(WEAPON); writer.WriteString(w.name); writer.WriteVarInt(w.hitPoints); }
        else if (item is Armor a)  { writer.WriteByte(ARMOR);  writer.WriteString(a.name); writer.WriteVarInt(a.hitPoints); writer.WriteVarInt(a.level); }
    }

    public static Item ReadItem(this NetworkReader reader)
    {
        byte type = reader.ReadByte();
        switch (type)
        {
            case WEAPON: return new Weapon { name = reader.ReadString(), hitPoints = reader.ReadVarInt() };
            case ARMOR:  return new Armor  { name = reader.ReadString(), hitPoints = reader.ReadVarInt(), level = reader.ReadVarInt() };
            default: throw new Exception($"Invalid item type {type}");
        }
    }
}
```

（示例结构来自 [Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)，`ReadPackedInt32` 已替换为本版本真实存在的 `ReadVarInt`）

### 7.6 调试 Weaver

【官方推荐】*You can use tools like ILSpy and dnSpy to view the complied code after Weaver has altered it. This can help to understand and debug what Mirror and Weaver does.*（[Serialization](https://mirror-networking.gitbook.io/docs/manual/guides/serialization)）

**Weaver 报错排查清单**：

| 症状 | 可能原因 |
|---|---|
| `writer not found` / `reader not found` 运行时报错 | Weaver 没跑（`WeaverFuse.Weaved()` 返回 false） |
| Weaver 报某个类型没有读写函数 | 该类型在 7.4 的不支持清单里 → 写自定义 `Writer<T>`/`Reader<T>` |
| Weaver 报泛型类型失败 | 泛型类型不支持，自定义读写必须声明具体的 `T` |
| 结构体/类里某个字段导致失败 | 该字段标记 `[System.NonSerialized]`，或给它写自定义读写 |
| 改了 `Assets/Mirror/` 源码后行为异常 | 不要改 Mirror 源码；Weaver 会重新织入整个程序集 |

【常见坑 · Unity 6 / IL2CPP】本版本 `WeaverFuse.cs` 的 `#if UNITY_2020_3_OR_NEWER` 分支说明 Mirror 走的是 **ILPostProcessor** 路径（源码注释：*this trick only works for ILPostProcessor. CompilationFinishedHook can't weaver Mirror.dll.*）。
- ILPostProcessor 的错误**会阻止进入 Play 模式**（源码注释：*note that ILPostProcessor errors already block entering playmode*）。
- 因此 Unity 6 下如果 Weaver 报错，你会**根本进不去 Play**，而不是「进去了但行为怪」。这反而是好事，容易定位。
- IL2CPP 具体注意事项：**未核实**（未在本地源码中找到 IL2CPP 专属分支或文档页）。

---

## 8. 消息、通道与 batching

### 8.1 通道

```csharp
// Assets/Mirror/Core/Tools/Utils.cs:28-31
public static class Channels
{
    public const int Reliable = 0;   // ordered
    public const int Unreliable = 1; // unordered
}
```

【重要】**96.11.3 只有两个通道**：`Reliable` 和 `Unreliable`。**没有 `UnreliableSequenced`**。

```csharp
[Command(channel = Channels.Unreliable)] void CmdMove(Vector2 dir) { }   // 高频、可丢
[ClientRpc(channel = Channels.Unreliable)] void RpcFlash() { }           // 表现类、可丢
```

### 8.2 发送 API（真实签名）

**NetworkServer**（`Assets/Mirror/Core/NetworkServer.cs`）：

```csharp
public static void SendToAll<T>(T message, int channelId = Channels.Reliable, bool sendToReadyOnly = false);  // :655
public static void SendToReady<T>(T message, int channelId = Channels.Reliable);                              // :700
public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable);  // :748
public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, int channelId);               // :788
```

【重要 · 与任务清单的差异】**本版本没有 `NetworkServer.SendToClient`，也没有公开的 `NetworkServer.SendToObservers`。**
- `SendToObservers<T>` 存在但是**私有的**（`NetworkServer.cs:714`：`static void SendToObservers<T>(NetworkIdentity identity, T message, int channelId = Channels.Reliable)`，无访问修饰符 → private）。
- 想发给单个客户端，用 **`conn.Send(...)`**：

```csharp
// 发给一个客户端（推荐）
conn.Send(new MyMessage { value = 42 });                       // NetworkConnection.Send<T>
conn.Send(new MyMessage { value = 42 }, Channels.Unreliable);  // 指定通道
```

**NetworkClient**（`Assets/Mirror/Core/NetworkClient.cs`）：

```csharp
public static void Send<T>(T message, int channelId = Channels.Reliable);                    // :507
public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true); // :564
public static void RegisterHandler<T>(Action<T, int> handler, bool requireAuthentication = true);  // :585
public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true);  // :607
public static void ReplaceHandler<T>(Action<T, int> handler, bool requireAuthentication = true);   // :625
public static bool UnregisterHandler<T>();                                                   // :641
```

**NetworkConnection**（`Assets/Mirror/Core/NetworkConnection.cs`）：

```csharp
public void Send<T>(T message, int channelId = Channels.Reliable);   // :88
public abstract void Disconnect();                                   // :195
public virtual void Cleanup();                                       // :201
```

**NetworkServer 的 handler 注册**（注意签名多了 sender）：

```csharp
public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true);     // :1090
public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true); // :1107
public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true);                                  // :1123
public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true);       // :1130
public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true);  // :1142
public static void UnregisterHandler<T>();                                                                                  // :1154
public static void ClearHandlers();                                                                                          // :1162
```

【常见坑】`NetworkServer` 和 `NetworkClient` 的 `RegisterHandler<T>` **签名不同**：
- `NetworkClient.RegisterHandler<T>(Action<T> handler, …)`
- `NetworkServer.RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, …)`

写成一样会编译失败。

### 8.3 NetworkMessage

```csharp
// 用法：实现空接口 NetworkMessage（Assets/Mirror/Core/NetworkMessage.cs）
public struct ScoreMessage : NetworkMessage
{
    public int score;
    public Vector3 scorePos;
    public int lives;
}
```

【官方原文】（[Network Messages](https://mirror-networking.gitbook.io/docs/manual/guides/communications/network-messages)）：

> * Make sure all your members are **public fields**. Weaver doesn't serialize properties.
> * If you need classes or complex containers such as `List<T>` and `Dictionary<T,K>`, you must implement the Read and Write methods yourself.

完整收发范式（官方例子）：

```csharp
public struct ScoreMessage : NetworkMessage
{
    public int score;
    public Vector3 scorePos;
    public int lives;
}

// 服务器发
NetworkServer.SendToAll(new ScoreMessage { score = 10, scorePos = Vector3.zero, lives = 3 });

// 客户端收
NetworkClient.RegisterHandler<ScoreMessage>(OnScore);
NetworkClient.Connect("localhost");

void OnScore(ScoreMessage msg) => Debug.Log("OnScoreMessage " + msg.score);
```

【官方 Info】*There is a public interface called `NetworkMessage` that you can extend to make serializable network message structs. Serialize and Deserialize methods are automatically generated for network messages by Mirror.*（同上）

**内置消息结构**（`Assets/Mirror/Core/Messages.cs`）—— 一般不需要自己用，但排查协议问题时会看到：

| 消息 | 行号 |
|---|---|
| `TimeSnapshotMessage` | 12 |
| `ReadyMessage` / `NotReadyMessage` / `AddPlayerMessage` | 17 / 19 / 21 |
| `SceneMessage { sceneName, sceneOperation, customHandling }` | 23 |
| `SceneOperation { Normal, LoadAdditive, UnloadAdditive }` | 31 |
| `CommandMessage` / `RpcMessage` | 38 / 48 |
| `SpawnMessage` | 65 |
| `ChangeOwnerMessage` | 105 |
| `ObjectSpawnStartedMessage` / `ObjectSpawnFinishedMessage` | 132 / 134 |
| `ObjectDestroyMessage` / `ObjectHideMessage` | 136 / 141 |
| `EntityStateMessage` | 147 |
| `EntityStateMessageUnreliableBaseline` / `…Delta` | 157 / 173 |
| `NetworkPingMessage` / `NetworkPongMessage` | 188 / 206 |

【常见坑】`EntityStateMessageUnreliableBaseline` / `EntityStateMessageUnreliableDelta` 是 96.x 新增的（配合 `syncMethod = SyncMethod.Hybrid` 与 `unreliableBaselineRate`）。旧版文档里没有它们。

### 8.4 Batching（批处理）

【官方原文】（[Timestamp Batching](https://mirror-networking.gitbook.io/docs/manual/general/timestamp-batching)）：

> * Every message that you send will be **batched until the end of the frame** in order to minimize bandwidth and transport calls. For example, if you send a lot of 10 byte messages then we can usually fit ~120 of them into one MTU sized batch of around 1200 bytes.
> * For the Transport, it's pretty convenient to send around messages in 1200 byte chunks (see MTU). Messages larger than MTU are sent as a single batch. To be exact, the Transport decides the batch size that Mirror aims for via `Transport.GetBatchThreshold()`.
> * Mirror batching is **bidirectional**. Which means that both the client and the server batch their messages and flush them out at the end of the frame.

```csharp
// Assets/Mirror/Core/Transport.cs:159
public virtual int GetBatchThreshold(int channelId = Channels.Reliable);
```

KCP 的实现（`Assets/Mirror/Transports/KCP/KcpTransport.cs:243`）：

```csharp
public override int GetBatchThreshold(int channelId) => …;   // 具体阈值见源码
```

**时间戳**（官方原文）：

> Mirror includes *an 8 byte `double` precision* `timestamp` **in every Batch**. Instead of including it in every message, we include it once per ~1200 byte batch.
> * On the **client**: `NetworkClient.connection.remoteTimeStamp`
> * On the **server**: `connectionToClient.remoteTimeStamp`

```csharp
// Assets/Mirror/Core/NetworkConnection.cs:59
public double remoteTimeStamp { get; internal set; }
```

```csharp
// 客户端：某个 Rpc / OnDeserialize 是服务器什么时候发的
double sentAt = NetworkClient.connection.remoteTimeStamp;

// 服务器：某个 Cmd 是客户端什么时候发的
double sentAt = connectionToClient.remoteTimeStamp;
```

【官方 Note】*Note that on the client, we don't use an object's `connectionToServer` because only the player owned objects have connections to the server. Instead we use the client's `NetworkClient.connection` to server, which is always guaranteed to be there.*（[Timestamp Batching](https://mirror-networking.gitbook.io/docs/manual/general/timestamp-batching)）

【常见坑】**只有拥有者对象才有 `connectionToServer`**（`NetworkIdentity.cs:196` 注释：*This is only valid for player objects on the client*）。在客户端想拿「服务器连接」一律用 `NetworkClient.connection`。

### 8.5 消息大小上限

```csharp
// Assets/Mirror/Core/NetworkWriter.cs
public const ushort MaxStringLength = ushort.MaxValue - 1;   // :14
public const int DefaultCapacity = 1500;                     // :19
public int Capacity => buffer.Length;                        // :26
```

超限检查出现在 4 个发送入口，都是 **`LogError` + 丢弃该消息**（不影响其它消息）：

| 入口 | 文件:行 |
|---|---|
| `NetworkConnection.Send<T>` | `Assets/Mirror/Core/NetworkConnection.cs:104` |
| `NetworkServer.SendToAll<T>` | `Assets/Mirror/Core/NetworkServer.cs:678` |
| `NetworkServer.SendToObservers<T>`（私有） | `Assets/Mirror/Core/NetworkServer.cs:734` |
| `NetworkServer.SendToReadyObservers<T>` | `Assets/Mirror/Core/NetworkServer.cs:768` |

报错文本形如：

```
NetworkServer.SendToObservers: message of type {typeof(T)} with a size of {writer.Position} bytes
is larger than the max allowed message size in one batch: {max}.
The message was dropped, please make it smaller.
```

【常见坑】单条消息不能超过一个 batch（约 MTU 1200 字节，具体由 `Transport.GetBatchThreshold` 决定）。要传大块数据（存档、关卡配置）**必须自己分片**。

【常见坑】字符串上限 `ushort.MaxValue - 1 = 65534` 字节（`MaxStringLength`）。

---

## 9. NetworkTransform / NetworkAnimator / NetworkRigidbody2D

### 9.1 NetworkTransformBase 公共 API

```csharp
// Assets/Mirror/Components/NetworkTransform/NetworkTransformBase.cs
public enum CoordinateSpace { Local, World }                          // :25
public enum UpdateMethod { Update, FixedUpdate, LateUpdate }          // :26
public abstract class NetworkTransformBase : NetworkBehaviour         // :28

public Transform target;                                              // :36
public UpdateMethod updateMethod = UpdateMethod.Update;               // :40
protected bool IsClientWithAuthority => isClient && authority;        // :44
public readonly SortedList<double, TransformSnapshot> clientSnapshots = new(16);  // :47
public readonly SortedList<double, TransformSnapshot> serverSnapshots = new(16);  // :48

public bool syncPosition = true;    // do not change at runtime!       // :52
public bool syncRotation = true;    // do not change at runtime!       // :53
public bool syncScale    = false;   // do not change at runtime!       // :54
public bool onlySyncOnChange = true;                                  // :58
public bool compressRotation = true;                                  // :60
public bool interpolatePosition = true;                               // :66
public bool interpolateRotation = true;                               // :68
public bool interpolateScale    = true;                               // :70
public CoordinateSpace coordinateSpace = CoordinateSpace.Local;       // :75
public uint sendIntervalMultiplier { get; }                           // :79  只读，由 syncInterval 换算
public bool timelineOffset = true;                                    // :111
public Vector3 velocity { get; private set; }                         // :131
public Vector3 angularVelocity { get; private set; }                  // :132
public bool showGizmos;                                               // :136
public bool showOverlay;                                              // :137
public Color overlayColor = new Color(0, 0, 0, 0.5f);                 // :138

protected virtual void Configure();                                   // :156
protected virtual Vector3 GetPosition();                              // :176
protected virtual Quaternion GetRotation();                           // :180
protected virtual Vector3 GetScale();                                 // :184
protected virtual void SetPosition(Vector3 position);                 // :188
protected virtual void SetRotation(Quaternion rotation);              // :197
protected virtual void SetScale(Vector3 scale);                       // :206
protected virtual TransformSnapshot Construct();                      // :218
protected virtual void Apply(TransformSnapshot interpolated, TransformSnapshot endGoal);  // :271

public void CmdTeleport(Vector3 destination);                         // :299
public void CmdTeleport(Vector3 destination, Quaternion rotation);    // :321
public void RpcTeleport(Vector3 destination);                         // :343
public void RpcTeleport(Vector3 destination, Quaternion rotation);    // :359
public void ServerTeleport(Vector3 destination, Quaternion rotation); // :373
protected virtual void OnTeleport(Vector3 destination);               // :386
protected virtual void OnTeleport(Vector3 destination, Quaternion rotation);  // :410
public virtual void ResetState();                                     // :434
public virtual void Reset();                                          // :446
```

```csharp
// 瞬移（服务器权威）
GetComponent<NetworkTransformBase>().ServerTeleport(new Vector3(10, 0, 0), Quaternion.identity);
```

【常见坑】源码注释反复强调 `syncPosition` / `syncRotation` / `syncScale` 是 `// do not change at runtime!`。运行中改这些字段会导致快照结构不一致。

### 9.2 `sendIntervalMultiplier` 的换算逻辑

```csharp
// NetworkTransformBase.cs:79-107
public uint sendIntervalMultiplier
{
    get
    {
        if (syncInterval > 0)
        {
            // 例如 NetworkServer.sendInterval = 1/60 = 0.0166
            //      NetworkTransform.syncInterval = 0.5 (500ms)
            //      0.5 / 0.0166 = 30.1  → 30x sendInterval
            float multiples = syncInterval / NetworkServer.sendInterval;
            return multiples > 1 ? (uint)Mathf.RoundToInt(multiples) : 1;
        }
        return 1;   // syncInterval == 0 → 每个 sendInterval 一次
    }
}
```

【常见坑】`NetworkTransform` 的 `syncInterval` 继承自 `NetworkBehaviour`。官方文档说「sync interval has moved to NetworkManager as **Send Rate**」—— 这句话**在本版本不准确**：`syncInterval` 仍在 `NetworkBehaviour` 上，只是 `NetworkTransform` 把它换算成 `sendIntervalMultiplier` 来对齐 tick。

### 9.3 三个变体的区别

官方（[Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform)）原文：

> Mirror currently provides **two** NetworkTransform variations:
> * Reliable: low bandwidth, same latency as Rpcs/Cmds/etc.
> * Unreliable: high bandwidth, extremely low latency
>
> **Use NetworkTransformReliable unless you need super low latency.**

【重要】官方文档只列了 **两个**，但本地源码里有 **三个**：

| 变体 | 文件 | 特点 |
|---|---|---|
| `NetworkTransformReliable` | `NetworkTransformReliable.cs` | 低带宽，延迟同 RPC |
| `NetworkTransformUnreliable` | `NetworkTransformUnreliable.cs` | 高带宽，极低延迟 |
| `NetworkTransformHybrid` | `NetworkTransformHybrid.cs` | 源码存在；`rotationPrecision` 等更细粒度量化；**官方文档未描述，未核实其定位** |

各自的额外字段：

```csharp
// NetworkTransformReliable.cs
public float onlySyncOnChangeCorrectionMultiplier = 2;   // :16
public float rotationSensitivity = 0.01f;                // :20
public float positionPrecision = 0.01f; // 1 cm          // :31
public float scalePrecision = 0.01f;    // 1 cm          // :35

// NetworkTransformUnreliable.cs
public float bufferResetMultiplier = 3;                  // :16
public float positionSensitivity = 0.01f;                // :19
public float rotationSensitivity = 0.01f;                // :20
public float scaleSensitivity = 0.01f;                   // :21

// NetworkTransformHybrid.cs
public float onlySyncOnChangeCorrectionMultiplier = 2;   // :14
public float rotationSensitivity = 0.01f;                // :18
public float positionPrecision = 0.01f;                  // :29
public float rotationPrecision = 0.001f;                 // :33
public float scalePrecision = 0.01f;                     // :35
public bool debugDraw = false;                           // :40
```

### 9.4 SyncDirection 与 NetworkTransform

【官方原文】（[Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform)）：

> By default, Network Transform is **server-authoritative** unless you change the **Sync Direction** to **Client To Server**. Client Authority applies to player objects as well as non-player objects that have been specifically assigned to a client, but **only for this component**. With this enabled, position changes are send from the client to the server.

【官方原文】（[SyncDirection](https://mirror-networking.gitbook.io/docs/manual/general/syncdirection)）：

> * **ServerToClient** is the default. `OnSerialize` (and all SyncVars, SyncLists) are sent from server to client every `syncInterval`. `OnDeserialize` is called on the client.
> * **ClientToServer** is for client authoritative components. `OnSerialize` is called on the **owner client** every `syncInterval`. It's then send to the server, where `OnDeserialize` is called. Afterwards the server broadcasts to all other clients except the owner. `OnDeserialize` is then called on those other clients.

【官方 Info】*Note that ClientToServer is a bit of a simplification. Technically it goes from the owner client, to the server, where it's then broadcast to all the other observer clients which aren't owners.*（同上）

```csharp
// 客户端权威移动（本项目：玩家自己控制自己）
// 在 Player prefab 的 NetworkTransform 组件上设 Sync Direction = Client To Server
```

【官方 Note】*ClientToServer data could still be verified in `OnDeserialize` on the server. That's why it's technically not 'client authority', hence the name SyncDirection. For example, while the owner client may sync NetworkTransform data to the server, you could still validate every move in the server's `OnDeserialize` before applying & broadcasting it.*（[SyncDirection](https://mirror-networking.gitbook.io/docs/manual/general/syncdirection)）

【官方推荐】*Use **server authority** for cheat safety.* —— 合作 PvE 可以接受客户端权威移动（省 RPC、手感好），但伤害结算等关键数值必须服务器权威。

### 9.5 Snapshot Interpolation 与 buffer

【官方原文】（[Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform)）：

> `NetworkTransform` sends movement updates every `sendInterval` over the `Unreliable` channel. … Our `NetworkTransform` component solves it by using **Snapshot Interpolation**.
>
> In short, smooth movement over non-ideal conditions is achieved through **buffering**. The worse the conditions, the higher the **Buffer Time Multiplier** needs to be. However, the higher it is the longer it buffers too.
>
> The total buffer time is calculated by `sendInterval * Buffer Time Multiplier`. **It's usually recommended to use a factor of '3'.**
>
> This means that while you can **increase** the `Buffer Time Multiplier` to make up for worse conditions, you could also **decrease** the `sendInterval` to still keep a reasonably low buffering delay.

相关字段在 `NetworkManager.snapshotSettings`（`SnapshotInterpolationSettings`，`NetworkManager.cs:136`）：

```csharp
// 通过 NetworkManager Inspector 的 Snapshot Settings 调整
NetworkManager.singleton.snapshotSettings.bufferTimeMultiplier = 3f;
```

【未核实】`SnapshotInterpolationSettings` 的完整字段列表（`bufferTimeMultiplier` / `driftEmaDuration` / `deliveryTimeEmaDuration` / `dynamicAdjustment` / `catchupNegativeThreshold` / `catchupPositiveThreshold` / `catchupSpeed` / `slowdownSpeed` 等）本次未逐条核对源码，仅从 `NetworkTransformBase` / `NetworkConnectionToClient` 的引用（`NetworkClient.snapshotSettings.driftEmaDuration`、`.deliveryTimeEmaDuration`、`.bufferTimeMultiplier`）确认这三个字段存在。

### 9.6 2D 项目注意事项

【官方原文】*Mirror currently provides two NetworkTransform variations…*（[Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform)）

**2D 相关的实际情况**（从源码读出的结论）：

1. **没有 `NetworkTransform2D` 组件**。2D 项目直接用 `NetworkTransformReliable` / `Unreliable`。
2. `coordinateSpace = CoordinateSpace.Local`（默认）—— 2D 项目通常父节点是根，Local 与 World 等价。
3. `syncScale = false`（默认）—— 2D 通常不需要同步缩放，保持关闭可省带宽。
4. **`compressRotation = true`（默认）** 对 2D 是浪费：2D 旋转只有一个 Z 轴分量，但压缩格式是给 3D 四元数用的。**2D 项目建议评估 `syncRotation` 是否真的需要**；如果只需要 Z 轴角度，考虑自己写一个极小的 `NetworkBehaviour` + `[SyncVar] float angle`（量化到 1/100 度）比 NetworkTransform 更省。
5. **`SpatialHashingInterestManagement.CheckMethod` 默认是 `XZ_FOR_3D`**（`SpatialHashingInterestManagement.cs:39`）—— **2D 项目（XY 平面）必须改成 2D 的选项**，否则可见性判断会用错平面。

```csharp
// 2D 项目：确认 CheckMethod 选的是 XY 平面
var aoi = NetworkManager.singleton.GetComponent<SpatialHashingInterestManagement>();
// aoi.checkMethod = SpatialHashingInterestManagement.CheckMethod.XY_FOR_2D;  // ← 枚举值需在 Inspector 确认
```

【未核实】`SpatialHashingInterestManagement.CheckMethod` 的完整枚举成员名（源码只列出了字段默认值 `CheckMethod.XZ_FOR_3D`，枚举体在 `SpatialHashingInterestManagement.cs:33-38`，本次未展开）。

### 9.7 NetworkAnimator

```csharp
// Assets/Mirror/Components/NetworkAnimator.cs
public class NetworkAnimator : NetworkBehaviour      // :20
public bool clientAuthority;                         // :27
public Animator animator;                            // :35
protected override void OnValidate();                // :101
public virtual void Reset();                         // :119
public override void OnSerialize(NetworkWriter writer, bool initialState);    // :417
public override void OnDeserialize(NetworkReader reader, bool initialState);  // :443
public void SetTrigger(string triggerName);          // :477
public void SetTrigger(int hash);                    // :486
public void ResetTrigger(string triggerName);        // :526
public void ResetTrigger(int hash);                  // :533
```

```csharp
// Trigger 必须走 NetworkAnimator，不能直接 animator.SetTrigger
GetComponent<NetworkAnimator>().SetTrigger("Attack");
```

【官方 Note】*Animator Triggers are not synced directly. Call `NetworkAnimator.SetTrigger` instead. A game object with authority can use the SetTrigger function to fire an animation trigger on other clients.*（[Network Animator](https://mirror-networking.gitbook.io/docs/manual/components/network-animator)）

【官方】*If the game object has authority on the client, you should animate it locally on the client that owns the game object. That client sends the animation state information to the server, which broadcasts it to all the other clients. … If the game object has authority on the server, then you should animate it on the server.*（同上）

【常见坑】`NetworkAnimator` 有 `syncInterval` 节流（`NetworkAnimator.cs:226`：`if (SendMessagesAllowed && syncInterval >= 0 && now > nextSendTime)`）。默认 `syncInterval = 0` → 每帧都发。**攻击/受击动画频繁的游戏必须设 `syncInterval`**。

### 9.8 NetworkRigidbody（2D 变体）

本地源码里的文件：

```
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyReliable.cs
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyReliable2D.cs
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyUnreliable.cs
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyUnreliable2D.cs
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyUnreliableCompressed.cs
```

【官方 Warning】*The Network Rigidbody classed as "Experimental" for now so please share any problems or bugs you find with it and use at your own risk if production builds.*（[Network Rigidbody](https://mirror-networking.gitbook.io/docs/manual/components/network-rigidbody)）

【官方推荐】*Network Rigidbody works best when there is also a **NetworkTransform** for the object to keep position as well as velocity in sync.*（同上）

官方提到的 Inspector 选项：**Client Authority**、**Sensitivity**（最小阈值，减少微小变化的流量）、**Clear Angular Velocity** / **Clear Velocity**、**Sync Mode = Owner Only**、**Sync Interval**。

```csharp
// 2D 项目：给有 Rigidbody2D 的玩家/敌人挂 NetworkRigidbodyReliable2D + NetworkTransformReliable
```

【常见坑】官方文档的 `Network Rigidbody` 页面只展示了 3D 的 Inspector 截图，**2D 变体的具体字段没有官方文档（未核实）**。本项目是 2D 俯视 survivor-like，绝大多数实体是**运动学移动（直接改 transform）**，不需要 `NetworkRigidbody*`；只有需要物理弹跳/击退的实体才考虑，且要考虑「物理 + 网络插值」的双重延迟。

---

## 10. Interest Management（兴趣管理 / 可见性）

### 10.1 为什么需要

【官方原文】（[Interest Management](https://mirror-networking.gitbook.io/docs/manual/interest-management)）：

> When making multiplayer games, the first obvious approach is to simply broadcast the world state to every player. **By default, that's what Mirror does when you don't use any Interest Management components.**
>
> There are a few major reasons for interest management:
> * **Scale**: imagine World of Warcraft. Sending the whole world to every single player would be insane.
> * **Visibility**: in a MOBA game like DotA/League of Legends, not everyone should see everyone else all the time.
> * **Cheating**: in games like Counter-Strike, players naturally don't see enemies behind walls because the camera wouldn't render them. But if the whole world state is known in memory, then hackers could exploit that.
>
> In other words, **interest management is almost always a good idea.**

### 10.2 类层次（本版本真实结构）

```csharp
// Assets/Mirror/Core/InterestManagementBase.cs
[DisallowMultipleComponent]
public abstract class InterestManagementBase : MonoBehaviour
{
    protected virtual void OnEnable()      // 把自己注册到 NetworkServer.aoi / NetworkClient.aoi
    {
        NetworkServer.aoi = this;
        NetworkClient.aoi = this;
    }

    [ServerCallback] public virtual void ResetState() {}
    public abstract bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver);
    [ServerCallback] public virtual void SetHostVisibility(NetworkIdentity identity, bool visible) { … }
    [ServerCallback] public virtual void OnSpawned(NetworkIdentity identity) {}
    [ServerCallback] public virtual void OnDestroyed(NetworkIdentity identity) {}
    public abstract void Rebuild(NetworkIdentity identity, bool initialize);

    protected void AddObserver(NetworkConnectionToClient connection, NetworkIdentity identity);
    protected void RemoveObserver(NetworkConnectionToClient connection, NetworkIdentity identity);
}

// Assets/Mirror/Core/InterestManagement.cs
public abstract class InterestManagement : InterestManagementBase
{
    public abstract void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers);  // :34
    [ServerCallback] protected void RebuildAll();     // :43
    public override void Rebuild(NetworkIdentity identity, bool initialize);  // :51
}
```

【重要】`OnRebuildObservers` 在本版本是 **2 个参数**（`identity`, `newObservers`），`initialize` 标志被挪到了 `Rebuild(identity, initialize)`。

【常见坑】官方 [Custom Interest Management](https://mirror-networking.gitbook.io/docs/manual/interest-management/custom) 页面里的示例签名是 **3 个参数**：

```csharp
// ❌ 官方文档里的旧签名，96.11.3 编译不过
public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers, bool initialize)
```

**本版本必须用 2 参数版**（见上）。

### 10.3 内置实现与字段

```csharp
// Assets/Mirror/Components/InterestManagement/Distance/DistanceInterestManagement.cs
public class DistanceInterestManagement : InterestManagement          // :9
public int visRange = 500;                                            // :12
public float minMoveDistance = 0.1f;                                  // :16
public float rebuildInterval = 1;                                     // :20
public byte staticRebuildInterval = 10;                               // :25
public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver);  // :85
public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers);  // :95

// Assets/Mirror/Components/InterestManagement/SpatialHashing/SpatialHashingInterestManagement.cs
public class SpatialHashingInterestManagement : InterestManagement    // :12
public int visRange = 30;                                             // :15
public int resolution => visRange / 2;                                // :27
public float rebuildInterval = 1;                                     // :30
public enum CheckMethod { … }                                         // :33
public CheckMethod checkMethod = CheckMethod.XZ_FOR_3D;               // :39
public bool showSlider;                                               // :42

// Assets/Mirror/Components/InterestManagement/Scene/SceneInterestManagement.cs
public class SceneInterestManagement : InterestManagement             // :8
public override void OnSpawned(NetworkIdentity identity);             // :21
public override void OnDestroyed(NetworkIdentity identity);           // :36
```

【官方推荐】*For better performance try **Hex Spatial Hashing** instead.*（[Spatial Hashing](https://mirror-networking.gitbook.io/docs/manual/interest-management/spatial-hashing)）

【官方推荐】*Select the **Network Manager** and add one of the built in Interest Management components.*（[Interest Management](https://mirror-networking.gitbook.io/docs/manual/interest-management)）—— **Interest Management 组件必须挂在 NetworkManager 所在物体上。**

内置系统清单（官方）：

| 系统 | 用途 | 官方链接 |
|---|---|---|
| Spatial Hashing | 全局统一 `Vis Range` | [spatial-hashing](https://mirror-networking.gitbook.io/docs/manual/interest-management/spatial-hashing) |
| Hex Spatial Hashing | 更快的空间哈希（官方推荐） | [hex-spatial-hashing](https://mirror-networking.gitbook.io/docs/manual/interest-management/hex-spatial-hashing) |
| Distance | `NetworkProximityChecker` 的平替 | [distance](https://mirror-networking.gitbook.io/docs/manual/interest-management/distance) |
| Scene | additive 场景间的视觉/物理隔离 | [scene](https://mirror-networking.gitbook.io/docs/manual/interest-management/scene) |
| Scene + Distance | 上面两者的组合 | [scene-+-distance](https://mirror-networking.gitbook.io/docs/manual/interest-management/scene-+-distance) |
| Match | 非物理的卡牌/棋牌/街机隔离 | [match](https://mirror-networking.gitbook.io/docs/manual/interest-management/match) |
| Team | 按队伍限制可见性（也可做 owner-only） | [team](https://mirror-networking.gitbook.io/docs/manual/interest-management/team) |
| Custom | 自定义模板 | [custom](https://mirror-networking.gitbook.io/docs/manual/interest-management/custom) |
| Legacy | **已废弃** | [legacy-interest-management](https://mirror-networking.gitbook.io/docs/manual/interest-management/legacy-interest-management) |

### 10.4 自定义 Interest Management

官方给的 Distance 实现（已按本版本签名修正参数个数）：

```csharp
public class MyDistanceAOI : InterestManagement
{
    public int visRange = 10;
    public float rebuildInterval = 1;
    double lastRebuildTime;

    public override bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)
    {
        return Vector3.Distance(identity.transform.position, newObserver.identity.transform.position) <= visRange;
    }

    public override void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)
    {
        Vector3 position = identity.transform.position;

        foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
            if (conn != null && conn.isAuthenticated && conn.identity != null)
                if (Vector3.Distance(conn.identity.transform.position, position) < visRange)
                    newObservers.Add(conn);
    }

    [ServerCallback]
    void Update()
    {
        if (NetworkTime.time >= lastRebuildTime + rebuildInterval)
        {
            RebuildAll();
            lastRebuildTime = NetworkTime.time;
        }
    }
}
```

（结构来自 [Custom](https://mirror-networking.gitbook.io/docs/manual/interest-management/custom)，签名按 `InterestManagement.cs:34` 修正）

【官方原文】关于三个方法的职责：

> * **`OnCheckObserver`** is called when someone spawns. Returns true if 'identity' can be seen by 'newObserver'
> * **`OnRebuildObservers`** rebuilds observers for the given Network Identity. The result is stored in `newObservers`. Mirror will automatically put `newObservers` into `identity.observers` internally.
> * **`RebuildAll`** is a helper function to rebuild every spawned Network Identity's observers. Implementations probably want to call this every interval.

【官方 Note】*Note that we only check the ones that are authenticated and have a player in the world. Connections that are still logging in or choosing characters shouldn't observe anything.*（同上）

### 10.5 Host 模式可见性

【官方原文】（[Custom](https://mirror-networking.gitbook.io/docs/manual/interest-management/custom)）：

> The **host** runs the **server**. The **server** holds the **whole world** state in memory, yet the **host player** only sees the world around him.
>
> Mirror has a virtual method `SetHostVisibility(NetworkIdentity, bool)` that enables / disables renderers in host mode. In other words, the world state is still there - the host player just doesn't see it. You can override this in your custom system to suit your needs.

```csharp
// InterestManagementBase.SetHostVisibility 默认实现会关掉：
// Renderer / Light / AudioSource / Canvas / Terrain / ParticleSystem（Emission）
public override void SetHostVisibility(NetworkIdentity identity, bool visible)
{
    base.SetHostVisibility(identity, visible);
    // 本项目额外处理：例如关掉小地图图标
}
```

【常见坑 · 性能】默认 `SetHostVisibility` 用 `GetComponentsInChildren<Renderer>()` 等 **6 次遍历**（Renderer / Light / AudioSource / Canvas / Terrain / ParticleSystem）。对 survivor-like 这种「同屏几百个实体」的游戏，**每次可见性变化都跑这 6 遍会很贵**。
【官方推荐】源码注释里解释了为什么连 Light/Audio/Terrain/Particle 都要关（手电筒、引擎声等）。如果本项目实体没有这些组件，**应该重写 `SetHostVisibility` 只关 Renderer**。

### 10.6 观察者相关 API

```csharp
// Assets/Mirror/Core/NetworkIdentity.cs
public readonly Dictionary<int, NetworkConnectionToClient> observers;   // :126
internal void AddObserver(NetworkConnectionToClient conn);              // :1484
internal void RemoveObserver(NetworkConnectionToClient conn);           // :1533
internal void ClearObservers();                                        // :1743

// Assets/Mirror/Core/NetworkConnectionToClient.cs
public readonly HashSet<NetworkIdentity> observing = new HashSet<NetworkIdentity>();  // :27
internal void AddToObserving(NetworkIdentity netIdentity);              // :180
internal void RemoveFromObserving(NetworkIdentity netIdentity, bool isDestroyed);  // :188
```

【常见坑】`AddObserver` / `RemoveObserver` / `AddToObserving` / `RemoveFromObserving` 全是 **`internal`**。应用层只能通过 `InterestManagementBase` 的 `protected AddObserver` / `RemoveObserver`（`InterestManagementBase.cs` 末尾）间接操作。

`NetworkServer.RebuildObservers` 是 public：

```csharp
NetworkServer.RebuildObservers(identity, initialize: false);   // NetworkServer.cs:2021
```

【官方原文】*`RebuildAll` … IMPORTANT: check if `NetworkServer.active` when using `Update()`!*（`InterestManagement.cs:41`）

### 10.7 一行启用示例

```csharp
// 服务器上强制刷新某个对象的观察者（例如它瞬移之后）
NetworkServer.RebuildObservers(myIdentity, false);

// 强制某个对象对所有人可见（例如 BOSS 血条 / 计分板）
myIdentity.visibility = Visibility.ForceShown;
```

---

## 11. 场景管理

### 11.1 【重要】本版本没有 `NetworkSceneManager` 类

任务清单里提到的 `NetworkSceneManager` —— **在 `Assets/Mirror/` 全目录 grep 无任何匹配**。场景管理全部在 `NetworkManager` 上。

```csharp
// Assets/Mirror/Core/NetworkManager.cs
public virtual void ServerChangeScene(string newSceneName);            // :815
internal void ClientChangeScene(string newSceneName, SceneOperation sceneOperation = SceneOperation.Normal, bool customHandling = false);  // :870  ← internal
protected void FinishLoadScene();                                      // :993
public static string networkSceneName { get; protected set; } = "";    // :806
public static AsyncOperation loadingSceneAsync;                        // :808
```

【常见坑】`ClientChangeScene` 是 **`internal`**，应用层不能调。客户端切场景只能由服务器下发 `SceneMessage` 驱动。

```csharp
// 服务器切场景（所有已连接客户端跟随）
NetworkManager.singleton.ServerChangeScene("Level1");
```

### 11.2 offlineScene / onlineScene

【官方原文】（[Network Manager](https://mirror-networking.gitbook.io/docs/manual/components/network-manager)）：

> There are two slots on the Network Manager inspector for scenes: the **Offline Scene** and the **Online Scene**. Dragging scene assets into these slots activates networked Scene Management.
>
> * When a server or host is started, the **Online Scene** is loaded. This then becomes the current network scene. Any clients that connect to that server are instructed to also load that scene. The name of this scene is stored in the `networkSceneName` property.
> * When the network is stopped, by stopping the server or host or by a client disconnecting, the **offline Scene** is loaded.
> * You can also change scenes while the game is active by calling `ServerChangeScene`. This makes all the currently connected clients change Scene too, and updates `networkSceneName` so that new clients also load the new Scene.

【官方 Note】*Note that scene change causes all the game objects in the previous scene to be destroyed.*（同上）

【官方推荐】*You should normally make sure the Network Manager persists between Scenes, otherwise the network connection is broken upon a scene change. To do this, ensure the **Don't Destroy On Load** checkbox is ticked in the inspector.*（同上）

> 对本项目（**已定死，见 `../MirrorPlan.md` §1.3**）：`Assets/Scenes/Lobby.unity`（大厅，同时是房间）→ `offlineScene`；
> **`onlineScene` 留空**（建房时玩家已在 Lobby，不需要 Mirror 自动跳场景）；`Menu` 是纯离线标题页。
> `NetworkManager` 必须勾 `dontDestroyOnLoad`。
> ⚠️ `onlineScene` 留空 ⇒ `IsServerOnlineSceneChangeNeeded()` 恒 false ⇒ `StartHost()` 不切场景，直接 `FinishStartHost()`；
> 这条路径下 `SpawnObjects()` 仍会跑，但 **`OnServerSceneChanged` 不会触发**（`NetworkManager.cs:280-283, 563`）。

### 11.3 additive scenes

`SceneOperation` 枚举（`Assets/Mirror/Core/Messages.cs:31`）：

```csharp
public enum SceneOperation : byte
{
    Normal,          // 0
    LoadAdditive,    // 1
    UnloadAdditive   // 2
}
```

`SceneMessage`（`Messages.cs:23`）：

```csharp
public struct SceneMessage : NetworkMessage
{
    public string sceneName;
    public SceneOperation sceneOperation;   // Normal = 0, LoadAdditive = 1, UnloadAdditive = 2
    public bool customHandling;
}
```

【官方】Additive 场景示例（[Additive Levels](https://mirror-networking.gitbook.io/docs/manual/examples/additive-levels)）：

> The Additive Levels example demonstrates the following:
> * Using Additive Scenes with **Scene Interest Management**.
> * Teleporting between levels via portals and respawning.
> * **Custom Scene Loading** with Fade In / Out transition.
>
> … you'll see that only players in the same level can see and collide with each other.

【官方】Setup 要求：所有场景加入 Build Settings；用 `SceneInterestManagement` 做隔离。

【常见坑】`NetworkManager` 的 `ServerChangeScene` 是**单场景**语义（`Normal`）。additive 加载需要走 `customHandling` 路径（`OnClientChangeScene(..., customHandling: true)` 时 Mirror 不会自己加载，由你的代码加载）。**本版本 additive 的具体调用入口未在源码中逐行核实**（`ServerChangeScene` 只接受 `string newSceneName`，没有公开的 additive 参数）。

### 11.4 场景对象 vs 动态生成对象

【官方原文】（[Network Identity](https://mirror-networking.gitbook.io/docs/manual/components/network-identity)）：

> **Scene-based Network Game Objects**: You can also network game objects that are saved as part of your Scene (for example, environmental props). … **When building your game, Unity disables all Scene-based game objects with Network Identity components.** When a client connects to the server, the server sends spawn messages to tell the client which Scene game objects to enable and what their most up-to-date state information is.

| | 场景对象 | 动态生成对象 |
|---|---|---|
| 身份 | `sceneId`（编辑器分配） | `assetId`（prefab GUID）+ 运行时 `netId` |
| 客户端如何对上号 | `NetworkClient.PrepareToSpawnSceneObjects()` + `spawnableObjects` | `NetworkClient.RegisterPrefab` / `NetworkManager.spawnPrefabs` |
| 需要登记吗 | 不需要（自动） | **必须登记进 `spawnPrefabs`** |
| 能否 `Instantiate` | **不能**（`NetworkIdentity.Awake` 会报错并销毁） | 必须 `Instantiate` 再 `Spawn` |
| spawn 时机 | 服务器 `SpawnObjects()` / 客户端场景加载后 | `NetworkServer.Spawn()` |

```csharp
// Assets/Mirror/Core/NetworkClient.cs
public static void PrepareToSpawnSceneObjects();   // :1302
static NetworkIdentity SpawnSceneObject(ulong sceneId);   // :1276  ← private
static NetworkIdentity GetAndRemoveSceneObject(ulong sceneId);  // :1291 ← private
```

【常见坑】客户端找不到场景对象时会报（`NetworkClient.cs:1281`）：

```
Spawn scene object not found for {sceneId:X}. Make sure that client and server use exactly
the same project. This only happens if the hierarchy gets out of sync.
```

原因基本只有一个：**客户端和服务器的构建版本不一致**（场景层级不同）。

---

## 12. Transports / 移动端 / NAT / Discovery

### 12.1 Transport 基类

```csharp
// Assets/Mirror/Core/Transport.cs
public abstract class Transport : MonoBehaviour          // :33
public static Transport active;                          // :36

public abstract bool Available();                        // :39
public virtual bool IsEncrypted => false;                // :42
public virtual string EncryptionCipher => "";            // :45

public Action OnClientConnected;                                          // :49
public Action<ArraySegment<byte>, int> OnClientDataReceived;              // :52
public Action<ArraySegment<byte>, int> OnClientDataSent;                  // :59
public Action<TransportError, string> OnClientError;                      // :62
public Action<Exception> OnClientTransportException;                      // :65
public Action OnClientDisconnected;                                       // :68
public Action<int> OnServerConnected;                                     // :74
public Action<int, string> OnServerConnectedWithAddress;                  // :77
public Action<int, ArraySegment<byte>, int> OnServerDataReceived;         // :80
public Action<int, ArraySegment<byte>, int> OnServerDataSent;             // :87
public Action<int, TransportError, string> OnServerError;                 // :91
public Action<int, Exception> OnServerTransportException;                 // :95
public Action<int> OnServerDisconnected;                                  // :98

public abstract bool ClientConnected();                                   // :102
public abstract void ClientConnect(string address);                       // :105
public virtual void ClientConnect(Uri uri);                               // :108
public abstract void ClientSend(ArraySegment<byte> segment, int channelId = Channels.Reliable);  // :117
public abstract void ClientDisconnect();                                  // :120
public abstract Uri ServerUri();                                          // :125
public abstract bool ServerActive();                                      // :128
public abstract void ServerStart();                                       // :131
public abstract void ServerSend(int connectionId, ArraySegment<byte> segment, int channelId = Channels.Reliable);  // :134
public abstract void ServerDisconnect(int connectionId);                  // :137
public abstract string ServerGetClientAddress(int connectionId);          // :141
public abstract void ServerStop();                                        // :144
public abstract int GetMaxPacketSize(int channelId = Channels.Reliable);  // :152
public virtual int GetBatchThreshold(int channelId = Channels.Reliable);  // :159
public virtual void ClientEarlyUpdate() {}                                // :193
public virtual void ServerEarlyUpdate() {}                                // :194
public virtual void ClientLateUpdate() {}                                 // :195
public virtual void ServerLateUpdate() {}                                 // :196
public abstract void Shutdown();                                          // :199
public virtual void OnApplicationQuit();                                  // :203
protected static Uri TryBuildValidUri(string scheme, string hostname, int port);  // :223
```

```csharp
// Assets/Mirror/Core/PortTransport.cs:9  —— 是 interface，不是 class！
public interface PortTransport { … }
```

### 12.2 KcpTransport（默认）

```csharp
// Assets/Mirror/Transports/KCP/KcpTransport.cs
public class KcpTransport : Transport, PortTransport      // :13
public const string Scheme = "kcp";                       // :16
public ushort port = 7777;                                // :21
public ushort Port { get => port; set => port = value; }  // :22
public bool DualMode = true;                              // :24
public bool NoDelay = true;                               // :26
public uint Interval = 10;                                // :28
public int Timeout = 10000;                               // :30
public int RecvBufferSize = 1024 * 1027 * 7;              // :32
public int SendBufferSize = 1024 * 1027 * 7;              // :34
public int FastResend = 2;                                // :38
public uint ReceiveWindowSize = 4096;                     // :42
public uint SendWindowSize = 4096;                        // :44
public uint MaxRetransmit = Kcp.DEADLINK * 2;             // :46
public bool MaximizeSocketBuffers = true;                 // :49
public bool debugLog;                                     // :71
public bool statisticsGUI;                                // :73
public bool statisticsLog;                                // :75
```

【官方】*kcp2k is the new default Transport for Mirror.* / *Works on all platforms **except WebGL**.* / *Unblock **UDP** (not TCP), Port 7777 (the default, unless you change it).*（[KCP Transport](https://mirror-networking.gitbook.io/docs/manual/transports/kcp-transport)）

【常见坑 · 移动端】KCP 走 UDP。**Android 上 UDP 会被运营商/企业 WiFi 屏蔽**的情况比 TCP 常见。跨公网联机失败时，先确认 UDP 7777 是否被挡。

【常见坑】`MaxRetransmit = Kcp.DEADLINK * 2` —— 源码注释（`:46`）：*default prematurely disconnects a lot of people (#3022). use 2x.* **不要调小**。

### 12.3 其他 Transport

| Transport | 文件 | 说明 |
|---|---|---|
| Telepathy | `Transports/Telepathy/TelepathyTransport.cs` | TCP；MMO 规模 |
| SimpleWeb | `Transports/SimpleWeb/SimpleWebTransport.cs` | WebSocket；WebGL 用 |
| LatencySimulation | `Transports/Latency/LatencySimulation.cs` | **调试用中间件**：模拟延迟/丢包/乱序 |
| MultiplexTransport | `Transports/Multiplex/MultiplexTransport.cs` | 一个服务器同时接多种 transport |
| MiddlewareTransport | `Transports/Middleware/MiddlewareTransport.cs` | 中间件基类 |
| ThreadedTransport | `Transports/Threaded/ThreadedTransport.cs` | 多线程基类 |
| EncryptionTransport | `Transports/Encryption/EncryptionTransport.cs` | 加密中间件 ⚠️ **本仓库未收录**（见 §0 的说明） |
| EdgegapKcpTransport | `Transports/Edgegap/EdgegapRelay/EdgegapKcpTransport.cs` | 中继（NAT 穿透）⚠️ **本仓库未收录** |

**LatencySimulation 用法**（【官方】[Latency Simulation Transport](https://mirror-networking.gitbook.io/docs/manual/transports/latency-simulaton-transport)）：

> Add it to NetworkManager, **wrap** it around your regular Transport, drag it into `NetworkManager.transport`. It can simulate: Latency in milliseconds / Packet loss in % / Packet scramble / reorder.

【官方 Info】*Reliable messages are ordered and guaranteed delivery by definition. Packet loss / scramble over reliable manifests itself via latency.*（同上）

【官方推荐】**上真机前必做**：用 `LatencySimulation` 包一层 KCP，模拟 150ms 延迟 + 5% 丢包，验证移动/伤害表现是否可接受。这是官方推荐的测试手段，也是本项目（Android 触屏 + WiFi）最值得做的联机验证。

### 12.4 NAT / 端口转发 / 联机方式

【官方原文】（[FAQ](https://mirror-networking.gitbook.io/docs/manual/faq)）：

**同机测试（localhost）**：`networkAddress = "localhost"`。
【官方 Note】*Please note that this won't work on mobile devices, as neither iOS or Android support running two instances of the same application side-by-side.*

**局域网（LAN）**：填主机的 LAN IP（如 `192.168.8.100`、`10.0.0.100`、`172.16.42.69`）。
【官方】*An incorrect setting, for example, is "localhost" or "203.200.110.100".*

**公网（WAN）**：填主机公网 IP。需要：
- **端口转发**：转发游戏端口（默认 **7777**），**协议要对**（KCP → UDP）。*incoming UDP connections won't work if you have it only set to accept incoming TCP connections and vice versa.*
- **PC 防火墙**放行编辑器与构建包。
- 【官方 Warning】DMZ 有安全风险：*The computer that has the IP address you specify will be exposed on the internet without the router firewall filtering bad inbound traffic.* / *DO NOT USE this DMZ option if you are running an unpatched operating system.*
- 【官方推荐】*Try from a build rather than the Unity Editor as sometimes Unity Editor can be janky.*

【官方 Warning】*This section does not cover Relays, Dedicated VPSes or Headless Features.*（同上）

【官方】Host Migration：*Host migration as of writing is **not built into Mirror**, and it is best to avoid Host Migration completely if you can.*（[FAQ](https://mirror-networking.gitbook.io/docs/manual/faq)）

【常见坑 · Android】移动端玩家之间**不能靠 localhost 联机**（不能同机双开）。移动端要么走 LAN（同一 WiFi），要么走中继/专用服务器。

### 12.5 NetworkDiscovery（局域网发现）

【官方原文】（[Network Discovery](https://mirror-networking.gitbook.io/docs/manual/components/network-discovery)）：

> To solve this problem you can use Network Discovery. When your game starts, it sends a message in your current network asking "Is there any server available?". Any server within the same network will reply and provide information about how to connect to it.
>
> Network Discovery uses a **UDP broadcast** on the LAN enabling clients to find the running server and connect to it.

快速上手（官方 9 步，摘要）：

```csharp
// 1. 场景里已有 NetworkManager
// 2. 不要加 NetworkManagerHUD（Discovery 有自己的 UI）
// 3. 给 NetworkManager 物体加 NetworkDiscoveryHUD（会自动补 NetworkDiscovery 并接线）
// 4. 配好 playerPrefab
// 5. 打包一个 standalone 版本 → Start Host
// 6. 编辑器里 Play → Find Servers
// 7. 点按钮连上
```

自定义发现（官方模板：`Assets ▸ Create ▸ Mirror ▸ Network Discovery`）：

```csharp
public class DiscoveryRequest : NetworkMessage
{
    public string language = "en";
}

public class DiscoveryResponse : NetworkMessage
{
    public Uri uri;          // 客户端据此连接
    public int TotalPlayers;
}

public class NewNetworkDiscovery : NetworkDiscoveryBase
{
    protected override void ProcessClientRequest(DiscoveryRequest request, IPEndPoint endpoint)
        => base.ProcessClientRequest(request, endpoint);

    protected override DiscoveryResponse ProcessRequest(DiscoveryRequest request, IPEndPoint endpoint)
        => new DiscoveryResponse();

    protected override DiscoveryRequest GetRequest() => new DiscoveryRequest();

    protected override void ProcessResponse(DiscoveryResponse response, IPEndPoint endpoint)
    {
        // 收到服务器回应
    }
}
```

（示例来自 [Network Discovery](https://mirror-networking.gitbook.io/docs/manual/components/network-discovery)）

【官方 Info】*Take into account any anti virus/firewall blocking, along with additional settings for your particular platform (**broadcast address iOS**, Network Discovery sharing settings to on in Windows etc).*（同上）

【常见坑 · Android】UDP broadcast 在部分 Android 机型/路由器上需要 **multicast lock** 或直接不可用（**未核实** —— 官方文档只提到 iOS broadcast address）。本项目如果是「一个玩家开房 + 其他玩家加入」的模式，建议**优先做房间码/列表服务器**，把 Discovery 当备用。

### 12.6 换 Transport 的步骤

【官方原文】（[Transports](https://mirror-networking.gitbook.io/docs/manual/transports)）：

> * Go to the game object that has the Network Manager component
> * Add another transport script via the Add Component button
> * Drag the transport script to the "Transport" field in the Network Manager
> * Remove the old transport script (optional)
>
> If you have connection issues with a transport that requires port forwarding, make sure to port forward the correct protocol (TCP / UDP).

---

## 13. 官方推荐做法与常见坑（总集）

### 13.1 官方明确的推荐做法

| # | 推荐 | 出处 |
|---|---|---|
| 1 | 用 **server authority**，除非确实需要客户端权威 | `NetworkBehaviour.cs:36` Tooltip / [Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority) |
| 2 | `NetworkTransform` **默认用 Reliable**，除非需要极低延迟 | [Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform) |
| 3 | Buffer Time Multiplier **用 3** | 同上 |
| 4 | `syncInterval` 保持默认 **0** | `NetworkBehaviour.cs:48-51` |
| 5 | SyncVar hook 用 **2 参数**（old, new），不要自己赋值 | [SyncVar Hooks](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvar-hooks) |
| 6 | SyncList 用**具体事件**（`OnAdd` 等），少用 `OnChange` | [SyncLists](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/synclists) |
| 7 | SyncList 声明为 **readonly** 并**在构造函数初始化** | 同上 |
| 8 | 自定义读写函数做成 **extension method**，命名 `ReadXxx`/`WriteXxx` | [Serialization](https://mirror-networking.gitbook.io/docs/manual/guides/serialization) |
| 9 | ScriptableObject 只传 **name/id**，对端查表 | [Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types) |
| 10 | Interest Management **几乎总是值得做** | [Interest Management](https://mirror-networking.gitbook.io/docs/manual/interest-management) |
| 11 | 用 **Hex Spatial Hashing** 代替 Spatial Hashing | [Spatial Hashing](https://mirror-networking.gitbook.io/docs/manual/interest-management/spatial-hashing) |
| 12 | `NetworkManager` 勾 **Don't Destroy On Load** | [Network Manager](https://mirror-networking.gitbook.io/docs/manual/components/network-manager) |
| 13 | 用 **LatencySimulation** 测非理想网络 | [Latency Simulation](https://mirror-networking.gitbook.io/docs/manual/transports/latency-simulaton-transport) |
| 14 | 联机测试**用构建包**，不要只信编辑器 | [FAQ](https://mirror-networking.gitbook.io/docs/manual/faq) |
| 15 | RPC 参数用**基础类型 / netId**，别用 Transform / 脚本实例 | [Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions) |
| 16 | 命名约定：`Cmd*` / `Rpc*` / `Target*` | 同上 |
| 17 | 用 `NetworkServer.Destroy` 而不是 `Destroy`（网络对象） | 源码语义 |
| 18 | 用 `NetworkServer.ReplacePlayerForConnection` 换玩家对象 | [Authority](https://mirror-networking.gitbook.io/docs/manual/guides/authority) |

### 13.2 常见坑（按危害排序）

#### P0 —— 直接导致联机不可用

**坑 1：prefab 没登记进 `spawnPrefabs`**

```
Failed to spawn server object, did you forget to add it to the NetworkManager? assetId=… netId=…
```
（`NetworkClient.cs:1272`）
→ 所有会在运行时 `NetworkServer.Spawn` 的 prefab，**必须**进 `NetworkManager.spawnPrefabs` 或调 `NetworkClient.RegisterPrefab`。
→ 用了 DDOL 的 NetworkManager，**所有场景**的 prefab 都要登记。

**坑 2：`NetworkManager` 没勾 `dontDestroyOnLoad`**

→ 切场景时连接断开。官方原文：*otherwise the network connection is broken upon a scene change*。

**坑 3：`NetworkManager` 放在带 `NetworkIdentity` 的物体上**

→ 官方原文：*Do not place the Network Manager component on a networked game object (one which has a Network Identity component), because Mirror disables these when the Scene loads.*

**坑 4：场景里有多个 `NetworkManager`**

→ `InitializeSingleton()` 会打 warning 并销毁第二个（`NetworkManager.cs:229`）：*Multiple NetworkManagers detected in the scene. Only one NetworkManager can exist at a time.*

**坑 5：嵌套 `NetworkIdentity`**

→ 官方 Danger：*Mirror does not support Network Identities on nested GameObjects. Otherwise, Mirror will emit an error.* 编辑器里由 `DisallowChildNetworkIdentities()` 拦截。

**坑 6：场景对象被 `Instantiate`**

→ `NetworkIdentity.Awake`（`:376`）报错并 `Destroy(gameObject)`。

**坑 7：打包前没保存带 `NetworkIdentity` 的场景**

→ 抛 `InvalidOperationException`（`NetworkIdentity.cs:514`）：*Scene … needs to be opened and resaved before building, because the scene object … has no valid sceneId yet.*

**坑 8：客户端 / 服务器构建版本不一致**

→ `Spawn scene object not found for {sceneId:X}`（`NetworkClient.cs:1281`）。

#### P1 —— 逻辑错误 / 难排查

**坑 9：在 `Awake` 里做网络判断**

`NetworkIdentity` 用 `[DefaultExecutionOrder(-1)]`（`NetworkIdentity.cs:57`），它的 `Awake` 会初始化 `NetworkBehaviours`。如果你给同物体的脚本设了更负的 `DefaultExecutionOrder`，或者依赖 Unity 的 `Awake` 顺序，`netIdentity` 可能还没赋值。
【官方推荐】用 `OnStartServer` / `OnStartClient` / `OnStartLocalPlayer` 做网络相关初始化。

**坑 10：在 `Awake` 里 `DontDestroyOnLoad`**

→ 官方 Danger：*Do not put objects in `DontDestroyOnLoad` (DDOL) in `Awake`. You can do that in `Start` instead.*

**坑 11：用 `authority` 判断「是不是我的角色」**

→ 见 1.2。host 模式下 `authority` 在 `syncDirection == ServerToClient` 时恒为 true。用 `isOwned` / `isLocalPlayer`。

**坑 12：`isClient` / `isServer` 当「网络是否活着」用**

→ 见 3.2。这两个标志**一旦 true 永不复位**（刻意设计）。判活跃用 `NetworkClient.active` / `NetworkServer.active`。

**坑 13：在服务器上调 `[Command]`**

→ 官方：*It's not possible to call this from a server. Use this as a wrapper around another function.*

**坑 14：`[Server]` / `[Client]` 用在逐帧方法上**

→ 错误端每次调用打一条 warning → Android 上刷日志卡死。用 `[ServerCallback]` / `[ClientCallback]`。

**坑 15：`OnStartClient` 里访问其它还没 spawn 的对象**

→ SyncVar 初值一定就位，但**别的对象**不一定。用 netId 延迟查找（见 5.6）。

**坑 16：RPC 传 `GameObject` / `NetworkIdentity` 并在对端直接用**

→ 对端如果还没 spawn 该对象 → `null`。官方推荐传 netId 自己查。

**坑 17：`StartHost()` 后立刻假设玩家已 spawn**

→ 见 2.3。`StartHost` 是异步的（有 onlineScene 时尤其）。

**坑 18：`OnStopServer` 里做对称清理**

→ Host 停止顺序是 `OnStopHost` → `OnServerDisconnect` → `OnStopClient` → `OnStopServer`（官方）。需要两端对称的清理写在 `OnStopHost`。

**坑 19：SyncVar 设为 `null`**

→ 官方：*Don't let them be null, you will get errors.*（[Attributes](https://mirror-networking.gitbook.io/docs/manual/guides/attributes)）

**坑 20：SyncVar 超过 64 个**

→ `MaxNetworkBehaviours = 64`（`NetworkIdentity.cs:219`）。含 SyncList。

#### P2 —— 编译不过（旧教程代码）

| 旧写法 | 96.11.3 正确写法 | 依据 |
|---|---|---|
| `identity.visible`（bool） | `identity.visibility`（`Visibility` 枚举） | `NetworkIdentity.cs:228-229` |
| `identity.observers` 当 `HashSet` | `Dictionary<int, NetworkConnectionToClient>` | `NetworkIdentity.cs:127` |
| `hasAuthority` | `isOwned` | `NetworkBehaviour.cs:79` |
| `syncVarHookGuard` 属性 | `GetSyncVarHookGuard()` / `SetSyncVarHookGuard()` | `NetworkBehaviour.cs:206/210` |
| `[SyncObject]` 属性 | `InitSyncObject(syncObject)` | `NetworkBehaviour.cs:280`；`Attributes.cs` 无此属性 |
| `writer.WritePackedInt32()` | `writer.WriteVarInt()` | `NetworkWriterExtensions.cs:46` |
| `reader.ReadPackedInt32()` | `reader.ReadVarInt()` | `NetworkReaderExtensions.cs` |
| `NetworkServer.SendToClient(conn, msg)` | `conn.Send(msg)` | 本版本无 `SendToClient` |
| `NetworkServer.SendToObservers(...)` | 私有，不可调；用 `SendToReadyObservers` | `NetworkServer.cs:714`（无修饰符 = private） |
| `NetworkSceneManager` | 不存在；用 `NetworkManager.ServerChangeScene` | 全目录 grep 无匹配 |
| `Channels.UnreliableSequenced` | 不存在；只有 `Reliable` / `Unreliable` | `Tools/Utils.cs:28-31` |
| `OnRebuildObservers(id, set, initialize)` | `OnRebuildObservers(id, set)` + `Rebuild(id, initialize)` | `InterestManagement.cs:34/51` |
| `OnCheckObserver` / `OnRebuildObservers` 在 `NetworkBehaviour` 上 | 在 `InterestManagement` 上 | `InterestManagementBase.cs:33` |
| `NetworkVisibility` 类 | 本版本不存在 | 全目录 grep 无匹配 |
| `NetworkIdentity.authority` | 不存在（`NetworkBehaviour.authority` 有） | `NetworkIdentity.cs` 无此成员 |
| `NetworkIdentity.spawned`（public） | `internal bool hasSpawned` | `NetworkIdentity.cs:326` |

#### P3 —— 性能坑

| 坑 | 说明 | 对策 |
|---|---|---|
| `NetworkManager.numPlayers` 每帧读 | LINQ `Count` + lambda（`NetworkManager.cs:154`） | 缓存 + 节流 |
| `SetHostVisibility` 默认实现 6 次 `GetComponentsInChildren` | `InterestManagementBase.cs` | 重写为只关 Renderer |
| `NetworkAnimator.syncInterval` 默认 0 | 每帧发（`NetworkAnimator.cs:226`） | 设 `syncInterval` |
| 每帧发 Command | 官方 Warning | 节流 / 本地预测 |
| `NetworkTransform.syncInterval = 0` + 高 `sendRate` | 每 tick 都发 | 按需调 `sendIntervalMultiplier` |
| SyncList 逐帧 `foreach` 接口 | 装箱枚举器（项目红线） | `for` + 索引器 |
| 单条消息超 MTU | 被丢弃 + LogError | 分片 |

### 13.3 连接 / 断线处理范式

```csharp
public class MyNetworkManager : NetworkManager
{
    public override void OnStartServer()  { Debug.Log("服务器启动"); }
    public override void OnStartHost()    { Debug.Log("Host 启动"); }
    public override void OnStartClient()  { Debug.Log("客户端启动"); }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"客户端接入 connId={conn.connectionId} addr={conn.address}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // 官方默认实现会清理玩家对象；重写记得调 base
        Debug.Log($"客户端断开 connId={conn.connectionId}");
        base.OnServerDisconnect(conn);
    }

    public override void OnClientDisconnect()
    {
        Debug.Log("已断开，返回主菜单");
        // 回主菜单（若配了 offlineScene，Mirror 会自动切）
    }

    public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
        => Debug.LogError($"服务器传输错误: {error} {reason}");

    public override void OnClientError(TransportError error, string reason)
        => Debug.LogError($"客户端传输错误: {error} {reason}");

    public override void OnStopHost()
    {
        // 两端对称清理放这里（Host 停止时它最先触发）
    }
}
```

【常见坑】`OnServerDisconnect` 的**默认实现会清理玩家对象**（`NetworkManager.cs:1336`）。重写时如果不调 `base`，玩家对象不会被回收，`conn.identity` 会残留。

### 13.4 连接质量 / 诊断

```csharp
// Assets/Mirror/Core/NetworkClient.cs
public static ConnectionQuality connectionQuality = ConnectionQuality.ESTIMATING;         // :139
public static ConnectionQuality lastConnectionQuality = ConnectionQuality.ESTIMATING;     // :140
public static ConnectionQualityMethod connectionQualityMethod = ConnectionQualityMethod.Simple;  // :141
public static float connectionQualityInterval = 3;                                        // :142
public static event Action<ConnectionQuality, ConnectionQuality> onConnectionQualityChanged;  // :149
```

```csharp
NetworkClient.onConnectionQualityChanged += (oldQ, newQ) => Debug.Log($"网络质量 {oldQ} -> {newQ}");
```

`ConnectionQuality` 枚举与 `ConnectionQualityMethod` 的定义在 `Assets/Mirror/Core/ConnectionQuality.cs`（**枚举成员未逐条核实**）。
官方说明页：[Connection Quality](https://mirror-networking.gitbook.io/docs/manual/general/connection-quality)。

**NetworkStatistics**（`Assets/Mirror/Components/NetworkStatistics.cs:15`：`public class NetworkStatistics : MonoBehaviour`）：

```csharp
// 挂在 NetworkManager 物体上即可
// 显示 messages/bytes per second（收发）
```

【官方】*Add this component to Network Manager object (or any object in an online scene).*（[Network Statistics](https://mirror-networking.gitbook.io/docs/manual/components/network-statistics)）

**NetworkDiagnostics**（`Assets/Mirror/Core/NetworkDiagnostics.cs`）—— 具体 API **未核实**。

---

## 14. 性能与带宽建议

### 14.1 带宽控制清单

| 手段 | 做法 | 依据 |
|---|---|---|
| 降低广播频率 | `NetworkManager.sendRate`（默认 60 → 可降到 20~30） | `NetworkManager.cs:44` → `NetworkServer.tickRate` |
| 降低单组件同步频率 | `NetworkBehaviour.syncInterval`（默认 0，按需设 0.1~0.5） | `NetworkBehaviour.cs:54` |
| 降低 NetworkTransform 频率 | 设 `syncInterval`（换算成 `sendIntervalMultiplier`） | `NetworkTransformBase.cs:79` |
| 只同步变化 | `onlySyncOnChange = true`（默认已开） | `NetworkTransformBase.cs:58` |
| 关掉不需要的分量 | `syncScale = false`（默认）；2D 评估 `syncRotation` | `NetworkTransformBase.cs:52-54` |
| 用 Reliable 变体 | 低带宽 | 官方推荐 |
| Interest Management | 只发视野内的对象 | [Interest Management](https://mirror-networking.gitbook.io/docs/manual/interest-management) |
| 高频输入用 Unreliable 通道 | `[Command(channel = Channels.Unreliable)]` | `Attributes.cs:28` |
| 避免每帧 RPC | 官方 Warning | [Remote Actions](https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions) |
| 依赖 batching | 不要自己攒帧；Mirror 每帧末尾统一 flush | [Timestamp Batching](https://mirror-networking.gitbook.io/docs/manual/general/timestamp-batching) |
| 用 `NetworkStatistics` 观测 | 挂组件看 bytes/s | [Network Statistics](https://mirror-networking.gitbook.io/docs/manual/components/network-statistics) |

### 14.2 survivor-like 的典型带宽画像（估算，非官方）

> 以下为**基于源码机制的推算**，不是官方数据，**未实测**。

- 同屏实体：100~300
- `sendRate = 30`（33ms）
- Interest Management：`SpatialHashingInterestManagement`，`visRange` 调到屏幕对角线 + 一点余量
- 玩家 `NetworkTransform`：`syncDirection = ClientToServer`（省 RPC，手感好）
- 敌人：服务器权威 `NetworkTransform` + `onlySyncOnChange = true`
- 伤害数字 / 特效：`[ClientRpc]` + 客户端本地表现，不要用 SyncVar 同步每个数字

### 14.3 官方性能相关页面

- [Timestamp Batching](https://mirror-networking.gitbook.io/docs/manual/general/timestamp-batching)
- [Network Statistics](https://mirror-networking.gitbook.io/docs/manual/components/network-statistics)
- [Remote Statistics](https://mirror-networking.gitbook.io/docs/manual/components/remote-statistics)
- [Network Profiler](https://mirror-networking.gitbook.io/docs/manual/components/network-profiler)
- [CCU](https://mirror-networking.gitbook.io/docs/manual/general/ccu)
- [Round Trip Time (RTT)](https://mirror-networking.gitbook.io/docs/manual/general/round-trip-time-rtt)
- [Connection Quality](https://mirror-networking.gitbook.io/docs/manual/general/connection-quality)
- [Spatial Hashing](https://mirror-networking.gitbook.io/docs/manual/interest-management/spatial-hashing)（提到 30x faster than distance checking）

---

## 15. 落到本项目（Survivor）的注意事项

结合 `CLAUDE.md` 的项目约定：

### 15.1 与「关闭 Domain Reload」的冲突

本项目编辑器**关闭了 Domain Reload**（`EnterPlayModeOptionsEnabled`）。Mirror 大量使用静态状态：

| 静态状态 | 文件 |
|---|---|
| `NetworkManager.singleton` | `NetworkManager.cs:151` |
| `NetworkManager.networkSceneName` | `:806` |
| `NetworkManager.startPositions` / `startPositionIndex` | `:132/133` |
| `NetworkServer.active` / `connections` / `spawned` / `aoi` | `NetworkServer.cs:96/72/81/107` |
| `NetworkClient.active` / `spawned` / `connection` / `localPlayer` / `aoi` | `NetworkClient.cs:84/62/66/77/131` |
| `NetworkIdentity.sceneIds` | `NetworkIdentity.cs:258` |
| `Transport.active` | `Transport.cs:36` |

【建议】**未核实官方对关 Domain Reload 的指引**。项目侧应自建防御（见 2.1 的 `ResetStatics` 示例），并在 Play 第二次时确认 `NetworkManager.singleton != null` 是本次的对象。

### 15.2 与「Player prefab 根节点未激活」的交互

`CLAUDE.md` 明确：Player prefab 的**根节点在资产里是未激活的**，`PlayerSpawner` 在 `Instantiate` 之后、**激活之前**注入 `playerConfig`。

**与 Mirror 的冲突点**：
- `NetworkServer.Spawn` 内部会 `identity.gameObject.SetActive(true)`（`NetworkServer.cs:1766`）。
- `NetworkIdentity.Awake`（`:368`）只在对象**激活**时跑；`InitializeNetworkBehaviours()` 也在此。
- 所以：`Instantiate` 出未激活的 prefab → 此时 `Awake` 还没跑 → **可以安全注入配置** → 再交给网络层激活 + spawn → `Awake` 跑 → `OnStartServer` 跑。

【建议】**唯一的硬约束是「注入必须早于激活」**，谁执行激活都可以。本项目 `PlayerSpawner` 走的是"自己激活"：

```csharp
GameObject player = Instantiate(playerPrefab, pos, rot);   // 未激活
InjectCharacterConfig(player, definition);                 // 必须在激活之前
player.SetActive(true);                                    // 自己激活（Awake 此刻跑）
NetworkServer.AddPlayerForConnection(conn, player);        // Mirror 接手 spawn
```

也可以省掉 `SetActive(true)`、让 `AddPlayerForConnection` 内部激活 —— 两种写法都对。
**唯一不能做的是「先激活、后注入」**：那时 `Awake` 已按「没有配置」失败过一次，
`EntityBehaviour.SetEntityConfig` 会拒绝注入，血量上限/移速停在 1f 兜底值（与 `CLAUDE.md` 描述一致）。
本项目统一采用**显式 `SetActive(true)`** 的写法，因为 `PlayerSpawner` 还要在激活前装配武器/角色，顺序更直观。

【常见坑】如果 prefab 根节点在**场景里**被拖进去（不是 `Instantiate`），Mirror 会把它当**场景对象**处理，走 `sceneId` 路径 —— 而场景对象**不能 `Instantiate`**，且 `Awake` 在场景加载时就跑了（配置注入时机没了）。这与 `CLAUDE.md` 的警告一致：*往场景里手动拖 Player prefab 时要记得它是未激活的。*

### 15.3 与「按玩家隔离状态」的交互

`CLAUDE.md` 架构原则 3：**联机兼容作为默认假设** —— 按玩家持有的状态挂 Player，遍历 `AllPlayers` 而不是只取 `LocalPlayer`。

Mirror 侧对应：

```csharp
// ❌ 只处理本地玩家
void UpdateUI() { var p = NetworkClient.localPlayer; … }

// ✅ 遍历所有玩家（本机需要为每个玩家建一份 UI）
foreach (NetworkIdentity id in NetworkClient.spawned.Values)
{
    if (!id.isOwned && !id.isLocalPlayer) continue;   // 按需过滤
    // …
}
```

【常见坑】`NetworkClient.spawned` 是 `Dictionary<uint, NetworkIdentity>`（`NetworkClient.cs:62`）。**不要每帧遍历**（几百个实体 + 字典枚举）。缓存一个玩家列表，在 `OnStartClient` / `OnStopClient` 里增量维护。

### 15.4 与「Addressables」的交互

`CLAUDE.md`：资源一律走 Addressables，禁止硬编码地址字符串。

**与 Mirror 的冲突点**：
- Mirror 的 `NetworkServer.Spawn` 要求 **prefab 是 Unity 资产引用**（`spawnPrefabs` 列表），而 Addressables 加载出来的是**运行时实例**。
- 本项目已有 `AssetReferenceGameObject` 模式。

【建议】**未核实 Mirror + Addressables 的官方组合方案**。可行路径（需实测）：
1. `NetworkManager.spawnPrefabs` 里放 **GUID 引用的 prefab**（保证 `assetId` 稳定），同时**也**把该 prefab 放进 Addressables 组；
2. 或者用 `NetworkClient.RegisterPrefab(prefab, spawnHandler, unspawnHandler)` 走自定义 spawn（`NetworkClient.cs:772`），由 handler 决定从哪加载。

> ⚠️ `CLAUDE.md` 里提到：AssetReference **同样要求目标在 Addressables 组里**，否则运行时加载失败。这条经验对 Mirror prefab 同样适用。

### 15.5 与「性能红线」的交互

| 项目红线 | Mirror 侧的对应风险 |
|---|---|
| `EntityStatModel.GetStat` 逐帧无分配 | Mirror 的 `[Server]`/`[Client]` warning、`numPlayers` LINQ、`SetHostVisibility` 的 6 次 `GetComponentsInChildren` 都是分配源 |
| 材质用 `sharedMaterial` | 官方 SyncVar hook 示例里用了 `GetComponent<Renderer>().material`（会克隆材质！）。**照抄会踩项目红线** |
| 不每帧写 Transform | `NetworkTransform` 每 tick 写 transform（这是它存在的意义）。要控制的是 `syncInterval` / `sendIntervalMultiplier` |
| 逐帧路径告警只提示一次 | 见 5.2，用 `[ServerCallback]` / `[ClientCallback]` |
| 不用 `FindObjectOfType` | Mirror 内部不依赖它；但 `NetworkClient.localPlayer` 才是正确入口 |

【常见坑 · 官方示例与项目红线冲突】官方 [SyncVar Hooks](https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvar-hooks) 的示例：

```csharp
void OnColorChanged(Color oldColor, Color newColor)
{
    if (cachedMaterial == null)
        cachedMaterial = GetComponent<Renderer>().material;   // ⚠️ 每个渲染器克隆一份材质
    cachedMaterial.color = newColor;
}
```

官方注释也承认了：*Unity makes a clone of the Material every time `GetComponent().material` is used. Cache it here and Destroy it in OnDestroy to prevent a memory leak.*

**本项目**：`SpriteRenderer.color` 是顶点色，不涉及材质实例（见 `CLAUDE.md`）。**不要照抄这个示例。**

### 15.6 与「命名约定」的交互

`CLAUDE.md`：私有字段 `_camelCase`，公开/序列化字段与方法 `PascalCase`；游戏代码**不使用 namespace**。

Mirror 侧：
- `[SyncVar]` 字段建议按项目约定命名（Mirror 不强制命名）。
- `[Command]` / `[ClientRpc]` / `[TargetRpc]` 的**方法名 Mirror 也不强制**，但官方推荐 `Cmd` / `Rpc` / `Target` 前缀。
- 游戏代码不写 namespace → 但 Mirror 的扩展方法（`Writer<T>`/`Reader<T>`）必须在**能访问到 `Mirror` 命名空间**的地方写（`using Mirror;`）。**注意**：项目约定「不使用 namespace」是针对**游戏代码**，自定义序列化扩展类可以放在全局命名空间 + `using Mirror;`。

---

## 16. 未核实 / 存疑清单

以下内容本次**没有**核实到，使用前请自行读源码或实测：

1. **`NetworkBehaviour.SendCommandInternal` 在无权限时的行为**（静默失败 / 打日志 / 抛异常）。源码位置 `NetworkBehaviour.cs:367`，未展开读。
2. **`NetworkBehaviour.SendRPCInternal` / `SendTargetRPCInternal` 在错误端的返回值与日志行为**（`NetworkBehaviour.cs:437` / `:491`）。
3. **`SnapshotInterpolationSettings` 的完整字段列表**。只确认了 `bufferTimeMultiplier`、`driftEmaDuration`、`deliveryTimeEmaDuration` 三个字段被引用。
4. **`SpatialHashingInterestManagement.CheckMethod` 的完整枚举成员名**（`SpatialHashingInterestManagement.cs:33-38`）。
5. **`HexSpatialHash2DInterestManagement` / `HexSpatialHash3DInterestManagement` 的字段与用法**（官方推荐用 Hex，但本次未读源码）。
6. **`MatchInterestManagement` / `TeamInterestManagement` / `SceneDistanceInterestManagement` 的字段与用法**。
7. **`NetworkMatch` / `NetworkTeam` 组件的 API**。
8. **`PredictedRigidbody` / `LagCompensator` 的适用场景与 API**。
9. **`NetworkRigidbodyReliable2D` / `NetworkRigidbodyUnreliable2D` 的具体字段**（官方文档只覆盖 3D）。
10. **`ConnectionQuality` 枚举成员** 与 `ConnectionQualityMethod` 成员。
11. **`NetworkDiagnostics` 的公开 API**。
12. **`NetworkAuthenticator` 的完整认证流程与 `BasicAuthenticator` 用法**（只核对了 `NetworkAuthenticator.cs` 的 public/protected 签名）。
13. **`NetworkRoomManager` / `NetworkRoomPlayer` / `NetworkLobbyManager` 的用法**。
14. **additive scene 的具体 API 入口**（`ServerChangeScene` 只接受 `string`，additive 需要 `customHandling` 路径，具体调用方式未核实）。
15. **Mirror 官方对「关闭 Domain Reload」的指引**（未找到相关文档页）。
16. **Mirror + Addressables 的官方组合方案**（未找到相关文档页）。
17. **IL2CPP 下的 Weaver 注意事项**（本地源码未找到 IL2CPP 专属分支；官方文档未找到专页）。
18. **Android 上 UDP broadcast / NetworkDiscovery 的端到端可用性**（源码里有 Android 多播锁处理 `NetworkDiscoveryBase.cs:267-291`，
    但真机行为未验证）。
19. **`NetworkClient` 是否有「所有对象 spawn 完成」的公开回调**（只找到私有的 `OnObjectSpawnFinished`，`NetworkClient.cs:1344`）。
20. **`NetworkTime.predictionErrorUnadjusted` / `predictedTime` 的具体语义与用法**。
21. **`EntityStateMessageUnreliableBaseline` / `…Delta` 与 `SyncMethod.Hybrid` 的配合细节**。
22. **`WeaverPriority` 属性的语义**（`Attributes.cs:100`，只在 `NetworkWriterExtensions.cs:46` 见到用法）。
23. **`NetworkManager.ApplyConfiguration` 之外，`NetworkServer.tickRate` 是否还能在运行时改**。
24. **`NetworkServer.Listen` / `Shutdown` 的直接调用场景**（一般由 NetworkManager 驱动）。
25. **`LatencySimulation` 的具体字段名**（只确认了它的功能，未读源码字段）。

---

## 17. 官方文档与本地源码不一致清单

| # | 官方文档说的 | 96.11.3 源码实际 | 影响 |
|---|---|---|---|
| 1 | `OnRebuildObservers(identity, newObservers, initialize)` 3 参数（[Custom IM](https://mirror-networking.gitbook.io/docs/manual/interest-management/custom)） | `OnRebuildObservers(identity, newObservers)` 2 参数；`initialize` 在 `Rebuild(identity, initialize)`（`InterestManagement.cs:34/51`） | 照抄文档**编译失败** |
| 2 | `OnRebuildObservers` / `OnSetHostVisibility` 在 `NetworkVisibility` 上（[NB Callbacks](https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkbehaviour-callbacks)） | 在 `InterestManagement` / `InterestManagementBase` 上；**`NetworkVisibility` 类不存在** | 找不到类 |
| 3 | `writer.WritePackedInt32()` / `reader.ReadPackedInt32()`（[Data types](https://mirror-networking.gitbook.io/docs/manual/guides/data-types)） | `WriteVarInt()` / `ReadVarInt()`（`NetworkWriterExtensions.cs:46`） | 照抄**编译失败** |
| 4 | NetworkTransform 只有 **两个**变体（[Network Transform](https://mirror-networking.gitbook.io/docs/manual/components/network-transform)） | 源码里有 **三个**（含 `NetworkTransformHybrid`） | 文档滞后 |
| 5 | *sync interval has moved to NetworkManager as "Send Rate"*（同上） | `syncInterval` 仍在 `NetworkBehaviour`；`NetworkTransform` 把它换算成 `sendIntervalMultiplier`（`NetworkTransformBase.cs:79`） | 语义误导 |
| 6 | `SyncEvents`（[Network Behaviour](https://mirror-networking.gitbook.io/docs/manual/components/networkbehaviour)） | 官方自己标注 *SyncEvents have been removed in version 18.0.0*；`Attributes.cs` 无 `SyncEventAttribute` | 不能用 |
| 7 | Interest Management 页面提到 `NetworkProximityChecker`（[IM](https://mirror-networking.gitbook.io/docs/manual/interest-management)） | 官方标注为 **Deprecated**（`legacy-interest-management`） | 不要用 |
| 8 | 官方 [FAQ](https://mirror-networking.gitbook.io/docs/manual/faq) 提到 `NetworkManager.singleton.numPlayers` | 存在，但是 LINQ 每帧遍历（`NetworkManager.cs:154`） | 性能陷阱 |
| 9 | 官方 SyncVar hook 示例用 `GetComponent<Renderer>().material` | 与项目 `CLAUDE.md` 材质红线冲突 | 不要照抄 |
| 10 | 官方 ScriptableObject 序列化示例用 `Resources.Load` | 与项目 `CLAUDE.md`「禁止硬编码地址 / 走 Addressables」冲突 | 不要照抄 |

> 说明：Mirror 官方 GitBook 的更新节奏落后于源码。**遇到「文档有、源码无」或签名不符，一律以 `Assets/Mirror/` 源码为准。**

---

## 18. 官方文档 URL 索引

主站：<https://mirror-networking.gitbook.io/docs>
> 💡 **技巧**：在任意 GitBook 页面 URL 后加 `.md` 可拿到该页的**原始 Markdown**（例如 `…/network-transform.md`），比抓 HTML 干净得多。
> 完整索引：<https://mirror-networking.gitbook.io/docs/llms.txt>

### 核心概念

| 主题 | URL |
|---|---|
| Authority（权威） | <https://mirror-networking.gitbook.io/docs/manual/guides/authority> |
| SyncDirection | <https://mirror-networking.gitbook.io/docs/manual/general/syncdirection> |
| IDs（assetId / sceneId / netId） | <https://mirror-networking.gitbook.io/docs/manual/guides/ids> |
| Data types | <https://mirror-networking.gitbook.io/docs/manual/guides/data-types> |
| Attributes | <https://mirror-networking.gitbook.io/docs/manual/guides/attributes> |
| Timestamp Batching | <https://mirror-networking.gitbook.io/docs/manual/general/timestamp-batching> |
| TCP and UDP | <https://mirror-networking.gitbook.io/docs/manual/general/tcp-and-udp> |
| FAQ | <https://mirror-networking.gitbook.io/docs/manual/faq> |
| Execution Order | <https://mirror-networking.gitbook.io/docs/manual/faq/execution-order> |
| Deprecations | <https://mirror-networking.gitbook.io/docs/manual/general/deprecations> |
| Script Templates | <https://mirror-networking.gitbook.io/docs/manual/general/script-templates> |

### 组件

| 主题 | URL |
|---|---|
| Network Manager | <https://mirror-networking.gitbook.io/docs/manual/components/network-manager> |
| Network Manager HUD | <https://mirror-networking.gitbook.io/docs/manual/components/network-manager-hud> |
| Network Identity | <https://mirror-networking.gitbook.io/docs/manual/components/network-identity> |
| Network Behaviour | <https://mirror-networking.gitbook.io/docs/manual/components/networkbehaviour> |
| Network Transform | <https://mirror-networking.gitbook.io/docs/manual/components/network-transform> |
| Snapshot Interpolation | <https://mirror-networking.gitbook.io/docs/manual/components/network-transform/snapshot-interpolation> |
| Network Animator | <https://mirror-networking.gitbook.io/docs/manual/components/network-animator> |
| Network Rigidbody | <https://mirror-networking.gitbook.io/docs/manual/components/network-rigidbody> |
| Network Start Position | <https://mirror-networking.gitbook.io/docs/manual/components/network-start-position> |
| Network Statistics | <https://mirror-networking.gitbook.io/docs/manual/components/network-statistics> |
| Remote Statistics | <https://mirror-networking.gitbook.io/docs/manual/components/remote-statistics> |
| Network Profiler | <https://mirror-networking.gitbook.io/docs/manual/components/network-profiler> |
| Network Discovery | <https://mirror-networking.gitbook.io/docs/manual/components/network-discovery> |
| Network Authenticators | <https://mirror-networking.gitbook.io/docs/manual/components/network-authenticators> |
| Basic Authenticator | <https://mirror-networking.gitbook.io/docs/manual/components/network-authenticators/basic-authenticator> |
| Network Room Manager | <https://mirror-networking.gitbook.io/docs/manual/components/network-room-manager> |
| Network Room Player | <https://mirror-networking.gitbook.io/docs/manual/components/network-room-player> |

### 通信

| 主题 | URL |
|---|---|
| Remote Actions（Cmd / Rpc / TargetRpc） | <https://mirror-networking.gitbook.io/docs/manual/guides/communications/remote-actions> |
| NetworkManager Callbacks | <https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkmanager-callbacks> |
| NetworkBehaviour Callbacks | <https://mirror-networking.gitbook.io/docs/manual/guides/communications/networkbehaviour-callbacks> |
| Network Messages | <https://mirror-networking.gitbook.io/docs/manual/guides/communications/network-messages> |

### 同步

| 主题 | URL |
|---|---|
| Synchronization（总览） | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization> |
| SyncVars | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvars> |
| SyncVar Hooks | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncvar-hooks> |
| SyncLists | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/synclists> |
| SyncDictionary | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncdictionary> |
| SyncHashSet | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/synchashset> |
| SyncSortedSet | <https://mirror-networking.gitbook.io/docs/manual/guides/synchronization/syncsortedset> |

### 序列化

| 主题 | URL |
|---|---|
| Serialization | <https://mirror-networking.gitbook.io/docs/manual/guides/serialization> |

### GameObject

| 主题 | URL |
|---|---|
| GameObjects（总览） | <https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects> |
| Player GameObjects | <https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/player-gameobjects> |
| Scene GameObjects | <https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/scene-gameobjects> |
| Custom Character Spawning | <https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-character-spawning> |
| Custom Spawn Functions | <https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/custom-spawnfunctions> |
| Pickups, Drops and Child Objects | <https://mirror-networking.gitbook.io/docs/manual/guides/gameobjects/pickups-drops-and-child-objects> |

### Interest Management

| 主题 | URL |
|---|---|
| Interest Management（总览） | <https://mirror-networking.gitbook.io/docs/manual/interest-management> |
| Spatial Hashing | <https://mirror-networking.gitbook.io/docs/manual/interest-management/spatial-hashing> |
| Hex Spatial Hashing | <https://mirror-networking.gitbook.io/docs/manual/interest-management/hex-spatial-hashing> |
| Distance | <https://mirror-networking.gitbook.io/docs/manual/interest-management/distance> |
| Scene | <https://mirror-networking.gitbook.io/docs/manual/interest-management/scene> |
| Scene + Distance | <https://mirror-networking.gitbook.io/docs/manual/interest-management/scene-+-distance> |
| Match | <https://mirror-networking.gitbook.io/docs/manual/interest-management/match> |
| Team | <https://mirror-networking.gitbook.io/docs/manual/interest-management/team> |
| Custom | <https://mirror-networking.gitbook.io/docs/manual/interest-management/custom> |
| Legacy（废弃） | <https://mirror-networking.gitbook.io/docs/manual/interest-management/legacy-interest-management> |

### Transports

| 主题 | URL |
|---|---|
| Transports（总览） | <https://mirror-networking.gitbook.io/docs/manual/transports> |
| KCP Transport | <https://mirror-networking.gitbook.io/docs/manual/transports/kcp-transport> |
| Telepathy Transport | <https://mirror-networking.gitbook.io/docs/manual/transports/telepathy-transport> |
| WebSockets Transport | <https://mirror-networking.gitbook.io/docs/manual/transports/websockets-transport> |
| Multiplex Transport | <https://mirror-networking.gitbook.io/docs/manual/transports/multiplex-transport> |
| Latency Simulation | <https://mirror-networking.gitbook.io/docs/manual/transports/latency-simulaton-transport> |
| Encryption Transport | <https://mirror-networking.gitbook.io/docs/manual/transports/encryption-transport> |
| Edgegap Transports | <https://mirror-networking.gitbook.io/docs/manual/transports/edgegap-transports> |
| Ignorance（第三方） | <https://mirror-networking.gitbook.io/docs/manual/transports/ignorance> |
| LiteNetLib（第三方） | <https://mirror-networking.gitbook.io/docs/manual/transports/litenetlib-transport> |

### 示例 / 进阶

| 主题 | URL |
|---|---|
| Examples（总览） | <https://mirror-networking.gitbook.io/docs/manual/examples> |
| Additive Levels | <https://mirror-networking.gitbook.io/docs/manual/examples/additive-levels> |
| Basic | <https://mirror-networking.gitbook.io/docs/manual/examples/basic> |
| Tanks | <https://mirror-networking.gitbook.io/docs/manual/examples/tanks> |
| Pong | <https://mirror-networking.gitbook.io/docs/manual/examples/pong> |
| Room | <https://mirror-networking.gitbook.io/docs/manual/examples/room> |
| Billiards | <https://mirror-networking.gitbook.io/docs/manual/examples/billiards> |
| Lag Compensation | <https://mirror-networking.gitbook.io/docs/manual/general/lag-compensation> |
| Client Side Prediction | <https://mirror-networking.gitbook.io/docs/manual/general/client-side-prediction> |
| Time Sync | <https://mirror-networking.gitbook.io/docs/manual/guides/time-sync> |
| Round Trip Time (RTT) | <https://mirror-networking.gitbook.io/docs/manual/general/round-trip-time-rtt> |
| Connection Quality | <https://mirror-networking.gitbook.io/docs/manual/general/connection-quality> |
| CCU | <https://mirror-networking.gitbook.io/docs/manual/general/ccu> |
| NetGraph | <https://mirror-networking.gitbook.io/docs/manual/general/netgraph> |
| Security Overview | <https://mirror-networking.gitbook.io/docs/security/security-overview> |
| Cheat Protection Stages | <https://mirror-networking.gitbook.io/docs/security/cheat-protection-stages> |
| Cheating | <https://mirror-networking.gitbook.io/docs/security/cheating> |
| Pragmatic Hosting Guide | <https://mirror-networking.gitbook.io/docs/hosting/pragmatic-hosting-guide> |
| Server Hosting | <https://mirror-networking.gitbook.io/docs/hosting/server-hosting> |
| Changelog（2025） | <https://mirror-networking.gitbook.io/docs/manual/general/changelog/2025-change-log> |
| Migration Guide | <https://mirror-networking.gitbook.io/docs/manual/general/migration-guide> |

### API Reference

| 说明 | URL |
|---|---|
| API 参考入口 | <https://mirror-networking.gitbook.io/docs/api-reference> |
| NetworkManager API | <https://storage.googleapis.com/mirror-api-docs/html/d7/d5a/class_mirror_1_1_network_manager.html> |
| NetworkBehaviour API | <https://storage.googleapis.com/mirror-api-docs/html/db/d21/class_mirror_1_1_network_behaviour.html> |
| NetworkIdentity API | <https://storage.googleapis.com/mirror-api-docs/html/d3/d88/class_mirror_1_1_network_identity.html> |

> ⚠️ API Reference 是**另一套生成物**，与 GitBook 正文一样可能滞后于 96.11.3 源码。**签名一律以 `Assets/Mirror/` 为准。**

---

## 附录 A：本文档核对过的源码位置汇总

| API | 文件:行 |
|---|---|
| `NetworkManager` 类 / 单例 / mode | `Core/NetworkManager.cs:17 / 151 / 174` |
| `NetworkManager` 字段 | `Core/NetworkManager.cs:24-148` |
| `NetworkManager.numPlayers` / `isNetworkActive` | `Core/NetworkManager.cs:154 / 157` |
| `NetworkManager.Awake` / `Start` / `Update` | `Core/NetworkManager.cs:223 / 240 / 265` |
| `InitializeSingleton()` | `Core/NetworkManager.cs`（`Awake` 内调用，`:226`） |
| `StartServer` / `StartClient` / `StartHost` | `Core/NetworkManager.cs:333 / 403,434 / 457` |
| `StopHost` / `StopServer` / `StopClient` | `Core/NetworkManager.cs:581 / 589 / 631` |
| `ServerChangeScene` / `ClientChangeScene` | `Core/NetworkManager.cs:815 / 870` |
| `networkSceneName` / `loadingSceneAsync` | `Core/NetworkManager.cs:806 / 808` |
| `RegisterStartPosition` / `GetStartPosition` | `Core/NetworkManager.cs:1106 / 1128` |
| `ResetStatics` | `Core/NetworkManager.cs:778` |
| `OnServerConnect` … `OnStopHost` | `Core/NetworkManager.cs:1332-1461` |
| `OnServerAddPlayer` 默认实现 | `Core/NetworkManager.cs:1358` |
| `ApplyConfiguration` | `Core/NetworkManager.cs`（`Update` 内调用） |
| `NetworkServer` 类 / 字段 | `Core/NetworkServer.cs:32 / 35-149` |
| `NetworkServer.Spawn` / `UnSpawn` / `Destroy` | `Core/NetworkServer.cs:1670,1704,1711 / 1902 / 1909` |
| `SpawnObject` 校验 | `Core/NetworkServer.cs:1717+` |
| `AddPlayerForConnection` / `ReplacePlayerForConnection` / `RemovePlayerForConnection` | `Core/NetworkServer.cs:1225,1241 / 1281-1308 / 1377,1386` |
| `ReplacePlayerOptions` / `RemovePlayerOptions` | `Core/NetworkServer.cs:9 / 21` |
| `SendToAll` / `SendToReady` / `SendToReadyObservers` | `Core/NetworkServer.cs:655 / 700 / 748,788` |
| `SendToObservers`（私有） | `Core/NetworkServer.cs:714` |
| `RegisterHandler` / `UnregisterHandler` / `ClearHandlers` | `Core/NetworkServer.cs:1090-1154 / 1162` |
| `RebuildObservers` | `Core/NetworkServer.cs:2021` |
| `NetworkClient` 类 / 字段 | `Core/NetworkClient.cs:21 / 32-149` |
| `NetworkClient.Send` | `Core/NetworkClient.cs:507` |
| `RegisterHandler` / `ReplaceHandler` / `UnregisterHandler` | `Core/NetworkClient.cs:564-641` |
| `RegisterPrefab` / `UnregisterPrefab` | `Core/NetworkClient.cs:704-949` |
| `RegisterSpawnHandler` / `UnregisterSpawnHandler` | `Core/NetworkClient.cs:977-1033` |
| `Connect` / `ConnectHost` / `Disconnect` | `Core/NetworkClient.cs:209,220 / 232 / 241` |
| `GetPrefab` / prefab 缺失报错 | `Core/NetworkClient.cs:652 / 1272` |
| `PrepareToSpawnSceneObjects` | `Core/NetworkClient.cs:1302` |
| `SpawnSceneObject` 报错 | `Core/NetworkClient.cs:1281` |
| `NetworkConnection` 类 / 字段 | `Core/NetworkConnection.cs:10 / 12-59` |
| `NetworkConnection.Send` | `Core/NetworkConnection.cs:88` |
| `NetworkConnectionToClient` 字段 | `Core/NetworkConnectionToClient.cs:18-61` |
| `NetworkIdentity` 类 | `Core/NetworkIdentity.cs:60` |
| `NetworkIdentity` 状态属性 | `Core/NetworkIdentity.cs:74-131` |
| `observers` / `visibility` / `serverOnly` | `Core/NetworkIdentity.cs:127 / 229 / 190` |
| `assetId` / `sceneId` / `netId` | `Core/NetworkIdentity.cs:155 / 136 / 131` |
| `connectionToServer` / `connectionToClient` | `Core/NetworkIdentity.cs:198 / 201` |
| `clientAuthorityCallback` | `Core/NetworkIdentity.cs:319,322` |
| `AssignClientAuthority` / `RemoveClientAuthority` | `Core/NetworkIdentity.cs:1547 / 1620` |
| `Awake` / `OnValidate` / `AssignSceneID` / `AssignAssetID` | `Core/NetworkIdentity.cs:368 / 385 / 490 / 422` |
| `MaxNetworkBehaviours` | `Core/NetworkIdentity.cs:219` |
| `HandleRemoteCall` | `Core/NetworkIdentity.cs:262` |
| `NetworkBehaviour` 类 / 枚举 | `Core/NetworkBehaviour.cs:30 / 12,15,24` |
| `syncMethod` / `syncDirection` / `syncMode` / `syncInterval` | `Core/NetworkBehaviour.cs:33 / 37 / 42 / 54` |
| `authority` | `Core/NetworkBehaviour.cs:96` |
| `isServer` … `isOwned` | `Core/NetworkBehaviour.cs:60-79` |
| `netId` / `connectionToServer` / `connectionToClient` | `Core/NetworkBehaviour.cs:112 / 116 / 119` |
| `netIdentity` / `ComponentIndex` | `Core/NetworkBehaviour.cs:135 / 138` |
| `syncVarDirtyBits` / `GetSyncVarHookGuard` / `SetSyncVarHookGuard` | `Core/NetworkBehaviour.cs:153 / 206 / 210` |
| `SetSyncVarDirtyBit` / `SetDirty` / `IsDirty` / `ClearAllDirtyBits` | `Core/NetworkBehaviour.cs:229 / 244 / 249 / 263` |
| `InitSyncObject` | `Core/NetworkBehaviour.cs:280` |
| `SendCommandInternal` / `SendRPCInternal` / `SendTargetRPCInternal` | `Core/NetworkBehaviour.cs:367 / 437 / 491` |
| `OnSerialize` / `OnDeserialize` | `Core/NetworkBehaviour.cs:1225 / 1232` |
| `OnStartServer` … `OnStopAuthority` | `Core/NetworkBehaviour.cs:1466-1487` |
| `Attributes`（全部） | `Core/Attributes.cs:15-100` |
| `RemoteCalls` | `Core/RemoteCalls.cs:8,11,31` |
| `Messages`（全部） | `Core/Messages.cs:12-206` |
| `Channels` | `Core/Tools/Utils.cs:28-31` |
| `NetworkWriter` 常量 | `Core/NetworkWriter.cs:14,19`（`:26` 是 `Capacity` 属性，不是常量） |
| `NetworkWriterExtensions` | `Core/NetworkWriterExtensions.cs`（75 个 Write） |
| `NetworkReaderExtensions` | `Core/NetworkReaderExtensions.cs`（49 个 Read） |
| `WriteVarInt` | `Core/NetworkWriterExtensions.cs:46` |
| `Transport` 基类 | `Core/Transport.cs:33-223` |
| `PortTransport`（interface） | `Core/PortTransport.cs:9` |
| `NetworkTime` | `Core/NetworkTime.cs:17-148` |
| `WeaverFuse` | `Core/WeaverFuse.cs` |
| `InterestManagementBase` | `Core/InterestManagementBase.cs`（全文） |
| `InterestManagement.OnRebuildObservers` / `RebuildAll` / `Rebuild` | `Core/InterestManagement.cs:34 / 43 / 51` |
| `DistanceInterestManagement` | `Components/InterestManagement/Distance/DistanceInterestManagement.cs:9-95` |
| `SpatialHashingInterestManagement` | `Components/InterestManagement/SpatialHashing/SpatialHashingInterestManagement.cs:12-77` |
| `SceneInterestManagement` | `Components/InterestManagement/Scene/SceneInterestManagement.cs:8-106` |
| `NetworkTransformBase` | `Components/NetworkTransform/NetworkTransformBase.cs:25-549` |
| `NetworkTransformReliable` | `Components/NetworkTransform/NetworkTransformReliable.cs:9-475` |
| `NetworkTransformUnreliable` | `Components/NetworkTransform/NetworkTransformUnreliable.cs:8-486` |
| `NetworkTransformHybrid` | `Components/NetworkTransform/NetworkTransformHybrid.cs:10-510` |
| `NetworkAnimator` | `Components/NetworkAnimator.cs:20-533` |
| `NetworkStatistics` | `Components/NetworkStatistics.cs:15` |
| `NetworkStartPosition` | `Core/NetworkStartPosition.cs`（全文） |
| `NetworkAuthenticator` | `Core/NetworkAuthenticator.cs:16-65` |
| `KcpTransport` | `Transports/KCP/KcpTransport.cs:13-355` |

---

## 附录 B：最短上手清单（本项目）

```csharp
// 1. 场景里放一个空物体 "NetworkManager"，挂：
//    - NetworkManager（勾 dontDestroyOnLoad）
//    - KcpTransport（默认）
//    - NetworkManagerHUD（仅调试）
//    - 可选：SpatialHashingInterestManagement（2D 记得改 CheckMethod）
//    - 可选：NetworkStatistics（看带宽）
//    offlineScene = Menu, onlineScene = Level0
//    playerPrefab = 未激活的 Player prefab
//    spawnPrefabs = 所有会 Spawn 的 prefab（敌人、子弹、掉落物…）

// 2. 玩家脚本
public class PlayerController : NetworkBehaviour
{
    public override void OnStartServer()      { /* 权威状态初始化 */ }
    public override void OnStartClient()      { /* SyncVar 初值已就位 */ }
    public override void OnStartLocalPlayer() { /* 相机 / 输入 */ }
    public override void OnStopLocalPlayer()  { /* 卸输入 */ }

    void Update()
    {
        if (!isOwned) return;                       // ← 用 isOwned，不是 authority
        Vector2 dir = ReadInput();
        if (dir.sqrMagnitude > 0.01f) CmdMove(dir);
    }

    [Command(channel = Channels.Unreliable)]        // 高频输入走不可靠通道
    void CmdMove(Vector2 dir) { /* 服务器校验 + 移动 */ }
}

// 3. 服务器切场景
NetworkManager.singleton.ServerChangeScene("Level0");

// 4. 断线回主菜单（offlineScene 自动切）
public override void OnClientDisconnect() => Debug.Log("断线，回主菜单");
```

---

*（文档结束。第 16 章列出的未核实项，请在真正动手前逐条确认；第 17 章的差异表在升级 Mirror 后要重新核对。）*
