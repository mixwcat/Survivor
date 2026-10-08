# Mirror 96.11.3 API 速查表（逐条核对本地源码）

> **来源**：仅依据 `D:\unity\proj\Survivor\Assets\Mirror\` 本地 vendored 源码逐条核对，签名与行号均为原文。
> **环境**：Unity 6000.0.44f1 / C# 9 / .NET Standard 2.1 / Mirror 96.11.3。
> **阅读约定**：
> - 每条格式为 `签名` —— 说明（`文件:行号`）。
> - 路径省略统一前缀 `Assets/Mirror/`。
> - **坑** = 容易踩的语义陷阱。
> - 源码中确实没有的成员，标注 `未在源码中找到`，不臆造。

---

## 0. 先说 10 个最容易踩的坑（跨章节汇总）

| # | 坑 | 依据 |
|---|---|---|
| 1 | `NetworkIdentity` 上的 `OnStartServer/OnStartClient/OnStartAuthority...` 全部是 **`internal`**，游戏代码不能重写；可重写版本在 `NetworkBehaviour`。 | `Core/NetworkIdentity.cs:735,755,775,801,1702,1722` |
| 2 | `NetworkIdentity` **没有** public `spawned` 属性，只有 `internal bool hasSpawned`；判断"已生成"用 `netId != 0` 或查 `NetworkServer.spawned` / `NetworkClient.spawned`。 | `Core/NetworkIdentity.cs:326` |
| 3 | `NetworkIdentity.visible` 已改名 `visibility`（类型 `Visibility`），旧名只保留在 `[FormerlySerializedAs]` 里。 | `Core/NetworkIdentity.cs:227-229` |
| 4 | `NetworkBehaviour.componentIndex`（小写 c）**不存在**，实际是 `ComponentIndex`（大写 C，`byte`）。 | `Core/NetworkBehaviour.cs:138` |
| 5 | `NetworkClient.localConnection` **不存在**；只有 `NetworkServer.localConnection`。客户端侧用 `NetworkClient.connection`（宿主模式下它是 `LocalConnectionToServer`）。 | `Core/NetworkServer.cs:69`；`Core/NetworkClient.cs:66` |
| 6 | `NetworkConnection.playerController` **已在 96 中移除**（只在注释里出现）；`connectionId` / `address` 只声明在 `NetworkConnectionToClient`，不在基类 `NetworkConnection`。 | `Core/NetworkConnectionToClient.cs:23,18` |
| 7 | `NetworkServer.SendToClient` / `SendToObservers` / `GetSpawnedObjects` **都不存在**。定向发送要走 `NetworkConnectionToClient.Send<T>()`；`SendToObservers` 是 `static`（private，非 public）。 | `Core/NetworkServer.cs:714` |
| 8 | `NetworkServer.RegisterMessageHandlers()` 是 **`internal`**，游戏代码调不到；自定义消息用 `RegisterHandler<T>`。 | `Core/NetworkServer.cs:331` |
| 9 | `[NetworkMessage]` **不是特性**；`NetworkMessage` 是空接口，消息结构体写 `struct X : NetworkMessage`。`[InterestManagement]` / `[SyncObject]` / `[SceneInterestManagement]` 特性同样不存在（分别是基类/组件）。 | `Core/NetworkMessage.cs:3` |
| 10 | `[Command]` 的 `requiresAuthority` 默认 **true**；`[ClientRpc]` 的 `includeOwner` 默认 **true**；两者 `channel` 默认 `Channels.Reliable`（=0）。 | `Core/Attributes.cs:27,28,37,38,47` |

---

## 1. `NetworkManager`（`Core/NetworkManager.cs`，共 1472 行）

### 1.1 顶部枚举

| 签名 | 说明 | 位置 |
|---|---|---|
| `public enum PlayerSpawnMethod { Random, RoundRobin }` | 出生点选择策略。 | `Core/NetworkManager.cs:10` |
| `public enum NetworkManagerMode { Offline, ServerOnly, ClientOnly, Host }` | 当前运行模式。 | `Core/NetworkManager.cs:11` |
| `public enum HeadlessStartOptions { DoNothing, AutoStartServer, AutoStartClient }` | 无头构建自动启动策略。 | `Core/NetworkManager.cs:12` |

### 1.2 类声明与序列化字段

| 签名 | 说明 | 位置 |
|---|---|---|
| `[DisallowMultipleComponent] [AddComponentMenu("Network/Network Manager")] public class NetworkManager : MonoBehaviour` | 组合根 MonoBehaviour。 | `Core/NetworkManager.cs:17` |
| `public bool dontDestroyOnLoad = true` | 跨场景保留。 | `Core/NetworkManager.cs:24` |
| `public bool runInBackground = true` | 后台运行避免网络超时。 | `Core/NetworkManager.cs:29` |
| `public HeadlessStartOptions headlessStartMode = HeadlessStartOptions.DoNothing` | 无头构建自动启动。 | `Core/NetworkManager.cs:35` |
| `public bool editorAutoStart` | 编辑器里也走 headless 自动启动。 | `Core/NetworkManager.cs:38` |
| `public int sendRate = 60` | 服务端/客户端发送频率（Hz）。 | `Core/NetworkManager.cs:44` |
| `public int unreliableBaselineRate = 1` | 不可靠同步的完整基线发送频率。 | `Core/NetworkManager.cs:48` |
| `public bool unreliableRedundancy = false` | 不可靠消息重发两次，双倍带宽换低延迟。 | `Core/NetworkManager.cs:54` |
| `public Transport transport` | **坑**：必须挂 Transport 组件，否则 Listen/Connect 直接失败。 | `Core/NetworkManager.cs:65` |
| `public string networkAddress = "localhost"` | 客户端连接地址；服务端不使用。 | `Core/NetworkManager.cs:70` |
| `public int maxConnections = 100` | 最大并发连接数。 | `Core/NetworkManager.cs:75` |
| `public bool disconnectInactiveConnections` | 是否自动踢掉不活跃连接。 | `Core/NetworkManager.cs:82` |
| `public float disconnectInactiveTimeout = 60f` | 不活跃超时（秒）。 | `Core/NetworkManager.cs:85` |
| `public bool exceptionsDisconnect = true` | **坑**：默认 true，网络回调里抛异常会直接断连（安全默认值）。 | `Core/NetworkManager.cs:88` |
| `public NetworkAuthenticator authenticator` | 认证组件；为空则连接即刻认证通过。 | `Core/NetworkManager.cs:92` |
| `public string offlineScene = ""` | 停机时切换的场景。 | `Core/NetworkManager.cs:98` |
| `public string onlineScene = ""` | 服务端启动时切换的场景。 | `Core/NetworkManager.cs:104` |
| `public float offlineSceneLoadDelay = 0` | 断线到加载 offlineScene 的延迟。 | `Core/NetworkManager.cs:107` |
| `public GameObject playerPrefab` | 玩家 prefab，**必须有 NetworkIdentity**。 | `Core/NetworkManager.cs:115` |
| `public bool autoCreatePlayer = true` | 是否自动 AddPlayer。 | `Core/NetworkManager.cs:120` |
| `public PlayerSpawnMethod playerSpawnMethod` | 出生点随机/轮询。 | `Core/NetworkManager.cs:125` |
| `[FormerlySerializedAs("m_SpawnPrefabs"), HideInInspector] public List<GameObject> spawnPrefabs = new List<GameObject>()` | **坑**：Inspector 里隐藏，但仍是 public；用于预注册可 spawn prefab。 | `Core/NetworkManager.cs:129` |
| `public SnapshotInterpolationSettings snapshotSettings = new SnapshotInterpolationSettings()` | 快照插值参数。 | `Core/NetworkManager.cs:136` |
| `public ConnectionQualityMethod evaluationMethod` | 连接质量评估方式。 | `Core/NetworkManager.cs:140` |
| `public float evaluationInterval = 3` | 评估间隔（秒），0 = 关闭。 | `Core/NetworkManager.cs:145` |
| `public bool timeInterpolationGui = false` | 仅 Editor/Dev Build 的插值调试 UI。 | `Core/NetworkManager.cs:148` |

### 1.3 静态与运行时状态

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static NetworkManager singleton { get; internal set; }` | **坑**：`internal set`，只能在 Mirror 内部赋值；业务代码只读。 | `Core/NetworkManager.cs:151` |
| `public int numPlayers => NetworkServer.connections.Count(kv => kv.Value.identity != null)` | **坑**：仅服务端有意义；含 LINQ，逐帧调用有分配。 | `Core/NetworkManager.cs:154` |
| `public bool isNetworkActive => NetworkServer.active \|\| NetworkClient.active` | 任一活跃即为 true。 | `Core/NetworkManager.cs:157` |
| `protected bool clientLoadedScene` | 客户端连接时是否已加载场景，`OnClientConnect` 里读。 | `Core/NetworkManager.cs:166` |
| `public NetworkManagerMode mode { get; private set; }` | 当前模式；`StopServer` 会置回 `Offline`。 | `Core/NetworkManager.cs:174` |
| `public static List<Transform> startPositions = new List<Transform>()` | 出生点列表，按 sibling index 排序。 | `Core/NetworkManager.cs:132` |
| `public static int startPositionIndex` | 轮询出生点的游标。 | `Core/NetworkManager.cs:133` |
| `public static string networkSceneName { get; protected set; } = ""` | 当前网络场景名。 | `Core/NetworkManager.cs:806` |
| `public static AsyncOperation loadingSceneAsync` | 场景加载句柄。 | `Core/NetworkManager.cs:808` |
| `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] public static void ResetStatics()` | 进入播放前重置静态状态。 | `Core/NetworkManager.cs:778` |

### 1.4 生命周期与启动/停止

| 签名 | 说明 | 位置 |
|---|---|---|
| `public virtual void OnValidate()` | 校验并同步 `sendRate` 到 `NetworkServer`。 | `Core/NetworkManager.cs:177` |
| `public virtual void Reset()` | Inspector 重置时补引用。 | `Core/NetworkManager.cs:205` |
| `public virtual void Awake()` | 初始化单例与传输层。 | `Core/NetworkManager.cs:223` |
| `public virtual void Start()` | 处理 headless 自动启动。 | `Core/NetworkManager.cs:240` |
| `public virtual void Update()` | 驱动 NetworkServer/NetworkClient 更新。 | `Core/NetworkManager.cs:265` |
| `public virtual void LateUpdate()` | 驱动 LateUpdate 阶段。 | `Core/NetworkManager.cs:271` |
| `public void StartServer()` | **坑**：异步（onlineScene 时先加载场景再 `SpawnObjects`）；已启动会 `LogWarning` 并返回。 | `Core/NetworkManager.cs:333` |
| `public void StartClient()` | 用 `networkAddress` 连接；地址为空报错返回。 | `Core/NetworkManager.cs:403` |
| `public void StartClient(Uri uri)` | 用 Uri 连接，并把 `networkAddress = uri.Host`。 | `Core/NetworkManager.cs:434` |
| `public void StartHost()` | **坑**：服务端先起、必要时先切 onlineScene，宿主客户端才连；整体异步。 | `Core/NetworkManager.cs:457` |
| `public void StopHost()` | 依次 `OnStopHost()` → `StopClient()` → `StopServer()`。 | `Core/NetworkManager.cs:581` |
| `public void StopServer()` | 非活跃直接 return；会先把 NM 移出 DDOL 再切 offlineScene，并清 `networkSceneName`。 | `Core/NetworkManager.cs:589` |
| `public void StopClient()` | **坑**：`mode == Offline` 时直接 return；Host 模式会先补一次 `OnServerDisconnect(localConnection)`。 | `Core/NetworkManager.cs:631` |
| `public virtual void OnApplicationQuit()` | 退出时清理。 | `Core/NetworkManager.cs:660` |
| `public virtual ConfigureHeadlessFrameRate()` | 无头模式帧率配置。 | `Core/NetworkManager.cs:685` |
| `public virtual void OnDestroy()` | 销毁清理。 | `Core/NetworkManager.cs:796` |
| `public virtual void ServerChangeScene(string newSceneName)` | 服务端切场景并广播 `SceneMessage`。 | `Core/NetworkManager.cs:815` |
| `protected void FinishLoadScene()` | 场景加载完成后的收尾（含 SpawnObjects/AddPlayer）。 | `Core/NetworkManager.cs:993` |

### 1.5 出生点 API

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static void RegisterStartPosition(Transform start)` | 注册出生点，并按 `GetSiblingIndex()` 重排列表。 | `Core/NetworkManager.cs:1106` |
| `public static void UnRegisterStartPosition(Transform start)` | 注销出生点。 | `Core/NetworkManager.cs:1121` |
| `public virtual Transform GetStartPosition()` | **坑**：列表为空返回 **null**（`OnServerAddPlayer` 需自行兜底）；Random 用 `UnityEngine.Random.Range`，RoundRobin 用 `startPositionIndex` 取模递增。 | `Core/NetworkManager.cs:1128` |

### 1.6 全部可重写回调（准确签名）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public virtual void OnServerConnect(NetworkConnectionToClient conn) { }` | 服务端：新客户端连接。 | `Core/NetworkManager.cs:1332` |
| `public virtual void OnServerDisconnect(NetworkConnectionToClient conn)` | 服务端：客户端断开；默认调用 `NetworkServer.DestroyPlayerForConnection(conn)`。 | `Core/NetworkManager.cs:1336` |
| `public virtual void OnServerReady(NetworkConnectionToClient conn)` | 服务端：客户端场景加载完成；默认 `NetworkServer.SetClientReady(conn)`。 | `Core/NetworkManager.cs:1346` |
| `public virtual void OnServerAddPlayer(NetworkConnectionToClient conn)` | **坑**：`conn.identity != null` 时报错返回；默认用 `playerPrefab` + `GetStartPosition()` 实例化并 `AddPlayerForConnection`。 | `Core/NetworkManager.cs:1358` |
| `public virtual void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason) { }` | 服务端传输错误。 | `Core/NetworkManager.cs:1378` |
| `public virtual void OnServerTransportException(NetworkConnectionToClient conn, Exception exception) { }` | 服务端传输异常。 | `Core/NetworkManager.cs:1381` |
| `public virtual void OnServerChangeScene(string newSceneName) { }` | 服务端切场景前。 | `Core/NetworkManager.cs:1384` |
| `public virtual void OnServerSceneChanged(string sceneName) { }` | 服务端切场景后。 | `Core/NetworkManager.cs:1387` |
| `public virtual void OnClientConnect()` | 客户端连接成功；默认在 `!clientLoadedScene` 时 `NetworkClient.Ready()` + （`autoCreatePlayer`）`AddPlayer()`。 | `Core/NetworkManager.cs:1390` |
| `public virtual void OnClientDisconnect() { }` | 客户端断开。 | `Core/NetworkManager.cs:1408` |
| `public virtual void OnClientError(TransportError error, string reason) { }` | 客户端传输错误。 | `Core/NetworkManager.cs:1411` |
| `public virtual void OnClientTransportException(Exception exception) { }` | 客户端传输异常。 | `Core/NetworkManager.cs:1414` |
| `public virtual void OnClientNotReady() { }` | 服务端通知客户端"不再 ready"。 | `Core/NetworkManager.cs:1417` |
| `public virtual void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling) { }` | 客户端切场景前。 | `Core/NetworkManager.cs:1421` |
| `public virtual void OnClientSceneChanged()` | 客户端场景加载完成；默认 `Ready()`，且仅在 `SceneOperation.Normal` 时 `AddPlayer()`。 | `Core/NetworkManager.cs:1427` |
| `public virtual void OnStartHost() { }` | 三种 Start 版本统一入口回调。 | `Core/NetworkManager.cs:1446` |
| `public virtual void OnStartServer() { }` | 服务端启动后（此时 `NetworkServer.active == true`，可 `Spawn`）。 | `Core/NetworkManager.cs:1449` |
| `public virtual void OnStartClient() { }` | 客户端启动。 | `Core/NetworkManager.cs:1452` |
| `public virtual void OnStopServer() { }` | 服务端停止。 | `Core/NetworkManager.cs:1455` |
| `public virtual void OnStopClient() { }` | 客户端停止。 | `Core/NetworkManager.cs:1458` |
| `public virtual void OnStopHost() { }` | 宿主停止。 | `Core/NetworkManager.cs:1461` |

### 1.7 `NetworkManagerHUD`（`Core/NetworkManagerHUD.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[RequireComponent(typeof(NetworkManager))] public class NetworkManagerHUD : MonoBehaviour` | 调试用 HUD。 | `Core/NetworkManagerHUD.cs:10` |
| `public int offsetX` | HUD 横向偏移。 | `Core/NetworkManagerHUD.cs:14` |
| `public int offsetY` | HUD 纵向偏移。 | `Core/NetworkManagerHUD.cs:15` |

---

## 2. `NetworkIdentity`（`Core/NetworkIdentity.cs`，共 1752 行）

### 2.1 类型与枚举

| 签名 | 说明 | 位置 |
|---|---|---|
| `public enum Visibility { Default, ForceHidden, ForceShown }` | 覆盖兴趣管理结果。 | `Core/NetworkIdentity.cs:24` |
| `public struct NetworkIdentitySerialization` | 每对象复用的序列化 writer 缓存。 | `Core/NetworkIdentity.cs:26` |
| `[DisallowMultipleComponent] [DefaultExecutionOrder(-1)] public sealed class NetworkIdentity : MonoBehaviour` | **坑**：`sealed`，不能继承；**坑**：子物体上不允许再挂 NetworkIdentity（Editor 会报错）。 | `Core/NetworkIdentity.cs:60` |
| `public static uint AssetGuidToUint(Guid guid) => (uint)guid.GetHashCode()` | GUID → assetId 映射（运行时也可用）。 | `Core/NetworkIdentity.cs:400` |

### 2.2 public 属性 / 字段

| 签名 | 说明 | 位置 |
|---|---|---|
| `public bool isClient { get; internal set; }` | **坑**：`internal set`，只读使用。 | `Core/NetworkIdentity.cs:74` |
| `public bool isServer { get; internal set; }` | 同上。 | `Core/NetworkIdentity.cs:89` |
| `public bool isHost => isServer && isClient` | 宿主模式。 | `Core/NetworkIdentity.cs:92` |
| `public bool isLocalPlayer { get; internal set; }` | 是否本机玩家。 | `Core/NetworkIdentity.cs:106` |
| `public bool isServerOnly => isServer && !isClient` | 仅服务端存在。 | `Core/NetworkIdentity.cs:109` |
| `public bool isClientOnly => isClient && !isServer` | 仅客户端存在。 | `Core/NetworkIdentity.cs:112` |
| `public bool isOwned { get; internal set; }` | 客户端侧：该对象是否属于本机连接。 | `Core/NetworkIdentity.cs:116` |
| `public readonly Dictionary<int, NetworkConnectionToClient> observers = new(...)` | key 是 `connectionId`；**坑**：只在服务端填充。 | `Core/NetworkIdentity.cs:127` |
| `public uint netId { get; internal set; }` | 运行时唯一 id；未 spawn 时为 0。 | `Core/NetworkIdentity.cs:131` |
| `[FormerlySerializedAs("m_SceneId"), HideInInspector] public ulong sceneId` | 场景对象持久 id；prefab 强制为 0。 | `Core/NetworkIdentity.cs:136` |
| `public uint assetId { get; internal set; }` | prefab 资产 id（见 2.4）。 | `Core/NetworkIdentity.cs:155` |
| `[FormerlySerializedAs("m_ServerOnly")] public bool serverOnly` | 只在服务端存在/启用。 | `Core/NetworkIdentity.cs:190` |
| `public NetworkConnection connectionToServer { get; internal set; }` | 客户端侧到服务端的连接。 | `Core/NetworkIdentity.cs:198` |
| `public NetworkConnectionToClient connectionToClient { get; internal set; }` | 服务端侧该对象的所属连接；setter 内部维护 `owned` 集合。 | `Core/NetworkIdentity.cs:201` |
| `public NetworkBehaviour[] NetworkBehaviours { get; private set; }` | 组件缓存；**坑**：上限 64（`MaxNetworkBehaviours`）。 | `Core/NetworkIdentity.cs:214` |
| `[FormerlySerializedAs("visible")] public Visibility visibility = Visibility.Default` | **坑**：旧字段名 `visible` 已废弃。 | `Core/NetworkIdentity.cs:229` |
| `public bool SpawnedFromInstantiate { get; private set; }` | 是否由 Instantiate 生成（会被立刻销毁）。 | `Core/NetworkIdentity.cs:327` |
| `[SerializeField, HideInInspector] internal bool hasSpawned` | **坑**：`internal`，游戏代码访问不到。 | `Core/NetworkIdentity.cs:326` |

### 2.3 权威与静态工具

| 签名 | 说明 | 位置 |
|---|---|---|
| `public delegate void ClientAuthorityCallback(NetworkConnectionToClient conn, NetworkIdentity identity, bool authorityState)` | 权威变更回调签名。 | `Core/NetworkIdentity.cs:319` |
| `public static event ClientAuthorityCallback clientAuthorityCallback` | **坑**：只在 `AssignClientAuthority` / `RemoveClientAuthority` 成功时触发。 | `Core/NetworkIdentity.cs:322` |
| `public static NetworkIdentity GetSceneIdentity(ulong id) => sceneIds[id]` | 按 sceneId 查场景对象。 | `Core/NetworkIdentity.cs:310` |
| `public static void ResetNextNetworkId() => nextNetworkId = 1` | 重置 netId 计数器（测试用）。 | `Core/NetworkIdentity.cs:316` |
| `public bool AssignClientAuthority(NetworkConnectionToClient conn)` | **坑**：非服务端调用报错返回 false；`conn == null` 报错；已有 owner 且不同则报错；成功后 `SendChangeOwnerMessage` + 触发回调。 | `Core/NetworkIdentity.cs:1547` |
| `public void RemoveClientAuthority()` | **坑**：非服务端报错；玩家对象不能移除（报错）。 | `Core/NetworkIdentity.cs:1620` |

### 2.4 `assetId` 分配机制（Editor 何时分配 / 运行时怎么取）

| 环节 | 说明 | 位置 |
|---|---|---|
| `internal void OnValidate()` → `#if UNITY_EDITOR { DisallowChildNetworkIdentities(); SetupIDs(); }` | 每次 Inspector 变更触发；同时把 `hasSpawned = false`。 | `Core/NetworkIdentity.cs:386-395` |
| `void SetupIDs()` | prefab → `sceneId = 0` + `AssignAssetID(gameObject)`；prefab stage → 同样赋值；否则走场景对象分支分配 `sceneId`。 | `Core/NetworkIdentity.cs:577` |
| `void AssignAssetID(string path)` | `Undo.RecordObject(this, "Assigned AssetId")` 后 `assetId = AssetGuidToUint(new Guid(AssetDatabase.AssetPathToGUID(path)))`。 | `Core/NetworkIdentity.cs:422-443` |
| `public uint assetId` getter | **坑**：`#if UNITY_EDITOR` 下若 `_assetId == 0` 会**现场调用 `SetupIDs()` 补分配**（防止 OnValidate 没跑到）。 | `Core/NetworkIdentity.cs:155-167` |
| setter | **坑**：赋 0 会 `LogError("Can not set AssetId to empty guid ...")` 并忽略。 | `Core/NetworkIdentity.cs:169-184` |
| `Editor/NetworkScenePostProcess.cs:96` `identity.SetSceneIdSceneHashPartInternal()` | 构建时给场景对象补 sceneId 哈希部分；`sceneId == 0` 会报 `"...has no valid sceneId yet."`。 | `Editor/NetworkScenePostProcess.cs:85,96` |
| 运行时取法 | 客户端反查 prefab：`NetworkClient.GetPrefab(assetId, out GameObject prefab)`。 | `Core/NetworkClient.cs:652` |

### 2.5 内部生命周期（说明为什么不能重写）

| 签名 | 说明 | 位置 |
|---|---|---|
| `internal void HandleRemoteCall(byte componentIndex, ushort functionHash, RemoteCallType remoteCallType, NetworkReader reader, NetworkConnectionToClient senderConnection = null)` | 分发 Cmd/Rpc。 | `Core/NetworkIdentity.cs:262` |
| `internal void OnStartServer()` / `OnStopServer()` / `OnStartClient()` / `OnStopClient()` | 逐个转发给 `NetworkBehaviours`。 | `Core/NetworkIdentity.cs:735,755,775,801` |
| `internal void OnStartAuthority()` / `OnStopAuthority()` | 权威变更时转发。 | `Core/NetworkIdentity.cs:1702,1722` |
| `internal void AddObserver(NetworkConnectionToClient conn)` / `RemoveObserver(...)` / `ClearObservers()` | 观察者集合维护。 | `Core/NetworkIdentity.cs:1484,1533,1743` |
| `internal void ResetState()` | 反初始化（unspawn 时）。 | `Core/NetworkIdentity.cs:1659` |

---

## 3. `NetworkBehaviour`（`Core/NetworkBehaviour.cs`，共 1494 行）

### 3.1 枚举

| 签名 | 说明 | 位置 |
|---|---|---|
| `public enum SyncMethod { Reliable, Hybrid }` | 同步方式。 | `Core/NetworkBehaviour.cs:12` |
| `public enum SyncMode { Observers, Owner }` | 同步给全部观察者 / 仅 owner。 | `Core/NetworkBehaviour.cs:15` |
| `public enum SyncDirection { ServerToClient, ClientToServer }` | 权威方向。 | `Core/NetworkBehaviour.cs:24` |

### 3.2 类与可配置字段

| 签名 | 说明 | 位置 |
|---|---|---|
| `public abstract class NetworkBehaviour : MonoBehaviour` | **坑**：`OnValidate` 会在 Editor 校验父级是否有 NetworkIdentity。 | `Core/NetworkBehaviour.cs:30` |
| `[HideInInspector] public SyncMethod syncMethod = SyncMethod.Reliable` | 默认 Reliable。 | `Core/NetworkBehaviour.cs:33` |
| `[HideInInspector] public SyncDirection syncDirection = SyncDirection.ServerToClient` | 默认服务端权威。 | `Core/NetworkBehaviour.cs:37` |
| `[HideInInspector] public SyncMode syncMode = SyncMode.Observers` | 默认广播给所有观察者。 | `Core/NetworkBehaviour.cs:42` |
| `[Range(0,2)] [HideInInspector] public float syncInterval = 0` | 0 = 每 tick 变更即发；**坑**：只影响状态同步，不影响 Cmd/Rpc。 | `Core/NetworkBehaviour.cs:54` |

### 3.3 身份/状态属性

| 签名 | 说明 | 位置 |
|---|---|---|
| `public bool isServer => netIdentity.isServer` | 服务端。 | `Core/NetworkBehaviour.cs:60` |
| `public bool isClient => netIdentity.isClient` | 客户端。 | `Core/NetworkBehaviour.cs:63` |
| `public bool isHost => isServer && isClient` | 宿主。 | `Core/NetworkBehaviour.cs:66` |
| `public bool isLocalPlayer => netIdentity.isLocalPlayer` | 本机玩家。 | `Core/NetworkBehaviour.cs:69` |
| `public bool isServerOnly => netIdentity.isServerOnly` | 仅服务端。 | `Core/NetworkBehaviour.cs:72` |
| `public bool isClientOnly => netIdentity.isClientOnly` | 仅客户端。 | `Core/NetworkBehaviour.cs:75` |
| `public bool isOwned => netIdentity.isOwned` | 本机拥有。 | `Core/NetworkBehaviour.cs:79` |
| `public bool authority { get; }` | **坑**：**逐组件**判定，不等于 `isOwned`：host 下 `ServerToClient \|\| isOwned`；纯客户端 `ClientToServer && isOwned`；纯服务端 `ServerToClient`。 | `Core/NetworkBehaviour.cs:96-109` |
| `public uint netId => netIdentity.netId` | 对象 netId。 | `Core/NetworkBehaviour.cs:112` |
| `public NetworkConnection connectionToServer => netIdentity.connectionToServer` | 客户端连接。 | `Core/NetworkBehaviour.cs:116` |
| `public NetworkConnectionToClient connectionToClient => netIdentity.connectionToClient` | 服务端连接。 | `Core/NetworkBehaviour.cs:119` |
| `public NetworkIdentity netIdentity { get; internal set; }` | 所属 identity。 | `Core/NetworkBehaviour.cs:135` |
| `public byte ComponentIndex { get; internal set; }` | **坑**：名字是大写 C 的 `ComponentIndex`，不是 `componentIndex`。 | `Core/NetworkBehaviour.cs:138` |

### 3.4 脏位与 SyncObject

| 签名 | 说明 | 位置 |
|---|---|---|
| `protected ulong syncVarDirtyBits` | 64 位 SyncVar 脏位。 | `Core/NetworkBehaviour.cs:153` |
| `internal ulong syncObjectDirtyBits` | 集合脏位。 | `Core/NetworkBehaviour.cs:157` |
| `ulong syncVarHookGuard` | **坑**：**private**，外部不可见；用途是防止 hook 内改 SyncVar 造成死锁。 | `Core/NetworkBehaviour.cs:164` |
| `protected bool GetSyncVarHookGuard(ulong dirtyBit)` | 供 Weaver 生成代码使用。 | `Core/NetworkBehaviour.cs:206` |
| `protected void SetSyncVarHookGuard(ulong dirtyBit, bool value)` | 供 Weaver 生成代码使用。 | `Core/NetworkBehaviour.cs:210` |
| `protected readonly List<SyncObject> syncObjects = new List<SyncObject>()` | 本组件的同步集合。 | `Core/NetworkBehaviour.cs:122` |
| `protected void InitSyncObject(SyncObject syncObject)` | **坑**：SyncList/Dictionary 必须由 Weaver 生成的构造函数调用它完成注册。 | `Core/NetworkBehaviour.cs:280` |
| `[MethodImpl(AggressiveInlining)] public void SetSyncVarDirtyBit(ulong dirtyBit)` | 标记脏。 | `Core/NetworkBehaviour.cs:229` |
| `[MethodImpl(AggressiveInlining)] public void SetDirty() => SetSyncVarDirtyBit(ulong.MaxValue)` | 全部标脏。 | `Core/NetworkBehaviour.cs:244` |
| `[MethodImpl(AggressiveInlining)] public bool IsDirty()` | 脏位与 `syncInterval` 都满足才 true。 | `Core/NetworkBehaviour.cs:249` |
| `[MethodImpl(AggressiveInlining)] public bool IsDirty_BitsOnly()` | 只看脏位不看时间。 | `Core/NetworkBehaviour.cs:258` |
| `public void ClearAllDirtyBits(bool clearSyncTime = true)` | 清脏位。 | `Core/NetworkBehaviour.cs:263` |
| `protected virtual void OnValidate()` | Editor 校验。 | `Core/NetworkBehaviour.cs:176` |

### 3.5 同步序列化

| 签名 | 说明 | 位置 |
|---|---|---|
| `public virtual void OnSerialize(NetworkWriter writer, bool initialState)` | 重写需调 `base`。 | `Core/NetworkBehaviour.cs:1225` |
| `public virtual void OnDeserialize(NetworkReader reader, bool initialState)` | 同上。 | `Core/NetworkBehaviour.cs:1232` |
| `protected virtual void SerializeSyncVars(NetworkWriter writer, bool initialState)` | Weaver 生成。 | `Core/NetworkBehaviour.cs:1261` |
| `protected virtual void DeserializeSyncVars(NetworkReader reader, bool initialState)` | Weaver 生成。 | `Core/NetworkBehaviour.cs:1273` |
| `public void SerializeObjectsAll(NetworkWriter writer)` | 全量序列化同步集合。 | `Core/NetworkBehaviour.cs:1284` |
| `public void SerializeObjectsDelta(NetworkWriter writer)` | 增量序列化同步集合。 | `Core/NetworkBehaviour.cs:1293` |
| `[EditorBrowsable(Never)] public virtual bool Weaved() => false` | Weaver 覆写为 true。 | `Core/NetworkBehaviour.cs:1492` |

### 3.6 SyncVar 生成器 API（Weaver 调用，手写也可用）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[MethodImpl(AggressiveInlining)] public void GeneratedSyncVarSetter<T>(T value, ref T field, ulong dirtyBit, Action<T, T> OnChanged)` | 泛型 SyncVar setter。 | `Core/NetworkBehaviour.cs:567` |
| `public void GeneratedSyncVarSetter_GameObject(GameObject value, ref GameObject field, ulong dirtyBit, Action<GameObject, GameObject> OnChanged, ref uint netIdField)` | GameObject 专用（带 netId 引用）。 | `Core/NetworkBehaviour.cs:596` |
| `public void GeneratedSyncVarSetter_NetworkIdentity(NetworkIdentity value, ref NetworkIdentity field, ulong dirtyBit, Action<NetworkIdentity, NetworkIdentity> OnChanged, ref uint netIdField)` | NetworkIdentity 专用。 | `Core/NetworkBehaviour.cs:625` |
| `public void GeneratedSyncVarSetter_NetworkBehaviour<T>(T value, ref T field, ulong dirtyBit, Action<T, T> OnChanged, ref NetworkBehaviourSyncVar netIdField) where T : NetworkBehaviour` | NetworkBehaviour 专用。 | `Core/NetworkBehaviour.cs:654` |
| `public void GeneratedSyncVarDeserialize<T>(ref T field, Action<T, T> OnChanged, T value)` | 泛型反序列化。 | `Core/NetworkBehaviour.cs:813` |
| `public void GeneratedSyncVarDeserialize_GameObject(ref GameObject field, Action<GameObject, GameObject> OnChanged, NetworkReader reader, ref uint netIdField)` | GameObject 反序列化。 | `Core/NetworkBehaviour.cs:891` |
| `public void GeneratedSyncVarDeserialize_NetworkIdentity(ref NetworkIdentity field, Action<NetworkIdentity, NetworkIdentity> OnChanged, NetworkReader reader, ref uint netIdField)` | NetworkIdentity 反序列化。 | `Core/NetworkBehaviour.cs:969` |
| `public void GeneratedSyncVarDeserialize_NetworkBehaviour<T>(ref T field, Action<T, T> OnChanged, NetworkReader reader, ref NetworkBehaviourSyncVar netIdField) where T : NetworkBehaviour` | NetworkBehaviour 反序列化。 | `Core/NetworkBehaviour.cs:1048` |
| `[EditorBrowsable(Never)] public static bool SyncVarGameObjectEqual(GameObject newGameObject, uint netIdField)` | GameObject 相等比较。 | `Core/NetworkBehaviour.cs:685` |
| `[EditorBrowsable(Never)] public static bool SyncVarNetworkIdentityEqual(NetworkIdentity newIdentity, uint netIdField)` | NetworkIdentity 相等比较。 | `Core/NetworkBehaviour.cs:753` |

### 3.7 `GetSyncVar*` / `SetSyncVar*`（protected，供 Weaver）

| 签名 | 说明 | 位置 |
|---|---|---|
| `protected void SetSyncVar<T>(T value, ref T fieldValue, ulong dirtyBit)` | 泛型设置。 | `Core/NetworkBehaviour.cs:1212` |
| `protected static bool SyncVarEqual<T>(T value, ref T fieldValue)` | 泛型比较。 | `Core/NetworkBehaviour.cs:1202` |
| `protected void SetSyncVarGameObject(GameObject newGameObject, ref GameObject gameObjectField, ulong dirtyBit, ref uint netIdField)` | GameObject 设置。 | `Core/NetworkBehaviour.cs:705` |
| `protected GameObject GetSyncVarGameObject(uint netId, ref GameObject gameObjectField)` | GameObject 获取。 | `Core/NetworkBehaviour.cs:732` |
| `protected void SetSyncVarNetworkIdentity(NetworkIdentity newIdentity, ref NetworkIdentity identityField, ulong dirtyBit, ref uint netIdField)` | NetworkIdentity 设置。 | `Core/NetworkBehaviour.cs:1083` |
| `protected NetworkIdentity GetSyncVarNetworkIdentity(uint netId, ref NetworkIdentity identityField)` | NetworkIdentity 获取。 | `Core/NetworkBehaviour.cs:1107` |
| `protected static bool SyncVarNetworkBehaviourEqual<T>(T newBehaviour, NetworkBehaviourSyncVar syncField) where T : NetworkBehaviour` | NB 比较。 | `Core/NetworkBehaviour.cs:1123` |
| `protected void SetSyncVarNetworkBehaviour<T>(T newBehaviour, ref T behaviourField, ulong dirtyBit, ref NetworkBehaviourSyncVar syncField) where T : NetworkBehaviour` | NB 设置。 | `Core/NetworkBehaviour.cs:1143` |
| `protected T GetSyncVarNetworkBehaviour<T>(NetworkBehaviourSyncVar syncNetBehaviour, ref T behaviourField) where T : NetworkBehaviour` | NB 获取。 | `Core/NetworkBehaviour.cs:1172` |

### 3.8 远程调用内部发送（protected）

| 签名 | 说明 | 位置 |
|---|---|---|
| `protected void SendCommandInternal(string functionFullName, ushort functionHashCode, NetworkWriter writer, int channelId, bool requiresAuthority = true)` | Weaver 生成的 `[Command]` 调用此方法。 | `Core/NetworkBehaviour.cs:367` |
| `protected void SendRPCInternal(string functionFullName, ushort functionHashCode, NetworkWriter writer, int channelId, bool includeOwner)` | `[ClientRpc]` 用。 | `Core/NetworkBehaviour.cs:437` |
| `protected void SendTargetRPCInternal(NetworkConnection conn, string functionFullName, ushort functionHashCode, NetworkWriter writer, int channelId)` | `[TargetRpc]` 用。 | `Core/NetworkBehaviour.cs:491` |

### 3.9 生命周期回调全家桶（全部 `public virtual`，空实现）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public virtual void OnStartServer() {}` | 服务端生成。 | `Core/NetworkBehaviour.cs:1466` |
| `public virtual void OnStopServer() {}` | 服务端反生成。 | `Core/NetworkBehaviour.cs:1469` |
| `public virtual void OnStartClient() {}` | 客户端生成。 | `Core/NetworkBehaviour.cs:1472` |
| `public virtual void OnStopClient() {}` | 客户端反生成。 | `Core/NetworkBehaviour.cs:1475` |
| `public virtual void OnStartLocalPlayer() {}` | 本机玩家生成（**坑**：只在本地玩家对象上触发）。 | `Core/NetworkBehaviour.cs:1478` |
| `public virtual void OnStopLocalPlayer() {}` | 本机玩家反生成。 | `Core/NetworkBehaviour.cs:1481` |
| `public virtual void OnStartAuthority() {}` | 获得权威。 | `Core/NetworkBehaviour.cs:1484` |
| `public virtual void OnStopAuthority() {}` | 失去权威。 | `Core/NetworkBehaviour.cs:1487` |

---

## 4. 特性 `Attributes`（`Core/Attributes.cs`，共约 100 行）

| 签名 | 可配置参数（默认值） | 说明 | 位置 |
|---|---|---|---|
| `[AttributeUsage(AttributeTargets.Field)] public class SyncVarAttribute : PropertyAttribute` | `public string hook;`（默认 null） | 字段同步；`hook` 指向方法名，签名必须 `(T oldValue, T newValue)`。 | `Core/Attributes.cs:15,17` |
| `[AttributeUsage(AttributeTargets.Method)] public class CommandAttribute : Attribute` | `public int channel = Channels.Reliable;` `public bool requiresAuthority = true;` | 客户端→服务端调用。**坑**：`requiresAuthority` 默认 true，客户端权威对象上调用会被拒。 | `Core/Attributes.cs:25,27,28` |
| `[AttributeUsage(AttributeTargets.Method)] public class ClientRpcAttribute : Attribute` | `public int channel = Channels.Reliable;` `public bool includeOwner = true;` | 服务端→所有客户端。**坑**：`includeOwner=false` 时 owner 客户端收不到。 | `Core/Attributes.cs:35,37,38` |
| `[AttributeUsage(AttributeTargets.Method)] public class TargetRpcAttribute : Attribute` | `public int channel = Channels.Reliable;` | 服务端→指定 `NetworkConnectionToClient`。 | `Core/Attributes.cs:45,47` |
| `[AttributeUsage(AttributeTargets.Method)] public class ServerAttribute : Attribute {}` | 无 | 仅服务端执行，非服务端**静默跳过**。 | `Core/Attributes.cs:55` |
| `[AttributeUsage(AttributeTargets.Method)] public class ServerCallbackAttribute : Attribute {}` | 无 | 仅服务端执行，**无警告**。 | `Core/Attributes.cs:62` |
| `[AttributeUsage(AttributeTargets.Method)] public class ClientAttribute : Attribute {}` | 无 | 仅客户端执行；服务端或未激活客户端执行会 **打印警告**。 | `Core/Attributes.cs:69` |
| `[AttributeUsage(AttributeTargets.Method)] public class ClientCallbackAttribute : Attribute {}` | 无 | 仅客户端执行，**无警告**。 | `Core/Attributes.cs:76` |
| `public class SceneAttribute : PropertyAttribute {}` | 无 | Inspector 里把字符串当场景选择器。 | `Core/Attributes.cs:81` |
| `[AttributeUsage(AttributeTargets.Field)] public class ShowInInspectorAttribute : Attribute {}` | 无 | 让私有 SyncList 在 Inspector 可见。 | `Core/Attributes.cs:88` |
| `[AttributeUsage(AttributeTargets.Field \| AttributeTargets.Property)] public class ReadOnlyAttribute : PropertyAttribute {}` | 无 | Inspector 只读。 | `Core/Attributes.cs:94` |
| `[AttributeUsage(AttributeTargets.Method)] public class WeaverPriorityAttribute : Attribute {}` | 无 | 同一类型有多个 Reader/Writer 时指定 Weaver 用哪个。 | `Core/Attributes.cs:100` |

**明确不存在的特性（不要写）**：

| 期望 | 实际 | 依据 |
|---|---|---|
| `[NetworkMessage]` | **不存在**；`public interface NetworkMessage {}`，消息写 `struct X : NetworkMessage`。 | `Core/NetworkMessage.cs:3` |
| `[SyncObject]` | **不存在**；`SyncObject` 是抽象基类。 | `Core/SyncObject.cs:13` |
| `[InterestManagement]` | **不存在**；`InterestManagement` / `InterestManagementBase` 是抽象基类。 | `Core/InterestManagement.cs:11`、`Core/InterestManagementBase.cs:10` |
| `[SceneInterestManagement]` | **不存在**；`SceneInterestManagement` 是 `InterestManagement` 的具体实现组件。 | `Components/InterestManagement/Scene/SceneInterestManagement.cs:8` |

---

## 5. `NetworkServer`（`Core/NetworkServer.cs`，共 2365 行，`public static partial class`）

### 5.1 枚举与静态状态

| 签名 | 说明 | 位置 |
|---|---|---|
| `public enum ReplacePlayerOptions { KeepAuthority, KeepActive, Unspawn, Destroy }` | 替换玩家时的处理方式。 | `Core/NetworkServer.cs:9` |
| `public enum RemovePlayerOptions { KeepActive, Unspawn, Destroy }` | 移除玩家时的处理方式。 | `Core/NetworkServer.cs:21` |
| `public static partial class NetworkServer` | 全静态。 | `Core/NetworkServer.cs:32` |
| `public static int maxConnections` | 由 `NetworkManager` 写入。 | `Core/NetworkServer.cs:35` |
| `public static int tickRate = 60` | 服务端 tick 频率。 | `Core/NetworkServer.cs:39` |
| `public static int unreliableBaselineRate = 1` | 基线发送频率。 | `Core/NetworkServer.cs:59` |
| `public static bool unreliableRedundancy = false` | 不可靠冗余。 | `Core/NetworkServer.cs:66` |
| `public static LocalConnectionToClient localConnection { get; private set; }` | **坑**：仅 Host 模式非 null；server-only 时为 null。 | `Core/NetworkServer.cs:69` |
| `public static Dictionary<int, NetworkConnectionToClient> connections = new(...)` | key = `connectionId`。 | `Core/NetworkServer.cs:72` |
| `public static readonly Dictionary<uint, NetworkIdentity> spawned = new(...)` | key = `netId`；**坑**：这是"已生成对象"的权威来源（不是 `GetSpawnedObjects()`）。 | `Core/NetworkServer.cs:81` |
| `public static bool listen` | 是否监听（替代旧 `dontListen`，语义相反）。 | `Core/NetworkServer.cs:85` |
| `[Obsolete(...)] public static bool dontListen { get; set; }` | 已废弃。 | `Core/NetworkServer.cs:89` |
| `public static bool active { get; internal set; }` | **坑**：`internal set`，只能读。 | `Core/NetworkServer.cs:96` |
| `public static bool activeHost => localConnection != null` | 是否 Host。 | `Core/NetworkServer.cs:100` |
| `public static bool isLoadingScene` | 正在切场景。 | `Core/NetworkServer.cs:103` |
| `public static InterestManagementBase aoi` | 由 IM 组件在 `OnEnable` 写入。 | `Core/NetworkServer.cs:107` |
| `public static float disconnectInactiveTimeout = 60` | 不活跃超时。 | `Core/NetworkServer.cs:122` |
| `public static int actualTickRate` | 实测 tick 率。 | `Core/NetworkServer.cs:138` |

### 5.2 事件（`Action` 字段，非 event）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static Action<NetworkConnectionToClient> OnConnectedEvent` | 新连接。 | `Core/NetworkServer.cs:129` |
| `public static Action<NetworkConnectionToClient> OnDisconnectedEvent` | 断开。 | `Core/NetworkServer.cs:130` |
| `public static Action<NetworkConnectionToClient, TransportError, string> OnErrorEvent` | 传输错误。 | `Core/NetworkServer.cs:131` |
| `public static Action<NetworkConnectionToClient, Exception> OnTransportExceptionEvent` | 传输异常。 | `Core/NetworkServer.cs:132` |
| `internal static void OnConnected(NetworkConnectionToClient conn)` | **坑**：`internal`，游戏代码只能订阅上面的事件字段。 | `Core/NetworkServer.cs:853` |
| `internal static void OnTransportData(int connectionId, ArraySegment<byte> data, int channelId)` | **坑**：`internal`，由 Transport 回调。 | `Core/NetworkServer.cs:897` |
| `internal static void OnTransportDisconnected(int connectionId)` | `internal`。 | `Core/NetworkServer.cs:1023` |

### 5.3 启动/关闭/连接管理

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static void Listen(int maxConns)` | 启动监听并注册默认 handler。 | `Core/NetworkServer.cs:152` |
| `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] public static void Shutdown()` | 关闭并清理。 | `Core/NetworkServer.cs:240` |
| `internal static void RegisterMessageHandlers()` | **坑**：`internal`，游戏代码不可调用。 | `Core/NetworkServer.cs:331` |
| `public static bool AddConnection(NetworkConnectionToClient conn)` | 加入 `connections`。 | `Core/NetworkServer.cs:599` |
| `public static bool RemoveConnection(int connectionId) => connections.Remove(connectionId)` | 移除。 | `Core/NetworkServer.cs:613` |
| `public static bool HasExternalConnections()` | 是否有真实外部连接。 | `Core/NetworkServer.cs:638` |
| `public static void DisconnectAll()` | 断开所有连接（**坑**：不会把 `active` 置 false）。 | `Core/NetworkServer.cs:1177` |

### 5.4 发送 API

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static void SendToAll<T>(T message, int channelId = Channels.Reliable, bool sendToReadyOnly = false) where T : struct, NetworkMessage` | 发给所有连接（含未 ready）。**坑**：`!active` 时 `LogWarning` 并丢弃。 | `Core/NetworkServer.cs:655` |
| `public static void SendToReady<T>(T message, int channelId = Channels.Reliable) where T : struct, NetworkMessage` | 只发给已 ready 的连接（内部 `SendToAll(..., true)`）。 | `Core/NetworkServer.cs:700` |
| `public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, bool includeOwner = true, int channelId = Channels.Reliable) where T : struct, NetworkMessage` | 发给该对象的 ready 观察者。 | `Core/NetworkServer.cs:748` |
| `public static void SendToReadyObservers<T>(NetworkIdentity identity, T message, int channelId) where T : struct, NetworkMessage` | 同上，`includeOwner` 默认 true。 | `Core/NetworkServer.cs:788` |
| `static void SendToObservers<T>(NetworkIdentity identity, T message, int channelId = Channels.Reliable) where T : struct, NetworkMessage` | **坑**：**非 public**（仅内部用于 ObjectDestroy）。 | `Core/NetworkServer.cs:714` |
| `public static void SendToClient` | **未在源码中找到**（96 中已移除）；定向发送请用 `NetworkConnectionToClient.Send<T>()`。 | — |
| `public static ... GetSpawnedObjects` | **未在源码中找到**；用 `NetworkServer.spawned` 字典。 | — |

### 5.5 消息 handler 注册

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | **坑**：默认要求已认证。 | `Core/NetworkServer.cs:1090` |
| `public static void RegisterHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 带 channelId 版本。 | `Core/NetworkServer.cs:1107` |
| `public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 覆盖已有 handler。 | `Core/NetworkServer.cs:1123` |
| `public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 覆盖。 | `Core/NetworkServer.cs:1130` |
| `public static void ReplaceHandler<T>(Action<NetworkConnectionToClient, T, int> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 覆盖。 | `Core/NetworkServer.cs:1142` |
| `public static void UnregisterHandler<T>() where T : struct, NetworkMessage` | 反注册。 | `Core/NetworkServer.cs:1154` |
| `public static void ClearHandlers() => handlers.Clear()` | 清空（**坑**：会清掉 Mirror 内置 handler）。 | `Core/NetworkServer.cs:1162` |

### 5.6 玩家管理

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static void DestroyPlayerForConnection(NetworkConnectionToClient conn)` | 销毁该连接的玩家对象。 | `Core/NetworkServer.cs:1073` |
| `public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId)` | 覆盖 assetId 后转调下一个重载。 | `Core/NetworkServer.cs:1225` |
| `public static bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player)` | **坑**：`conn.identity != null` 时返回 false；**坑**：会**自动 spawn**，不要再手动 `Spawn`；**坑**：会把客户端置 ready。 | `Core/NetworkServer.cs:1241` |
| `[Obsolete] public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, bool keepAuthority = false)` | 已废弃。 | `Core/NetworkServer.cs:1281` |
| `[Obsolete] public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, bool keepAuthority = false)` | 已废弃。 | `Core/NetworkServer.cs:1291` |
| `public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, ReplacePlayerOptions replacePlayerOptions)` | 推荐重载。 | `Core/NetworkServer.cs:1298` |
| `public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, ReplacePlayerOptions replacePlayerOptions)` | 推荐重载。 | `Core/NetworkServer.cs:1308` |
| `[Obsolete] public static void RemovePlayerForConnection(NetworkConnectionToClient conn, bool destroyServerObject)` | 已废弃。 | `Core/NetworkServer.cs:1377` |
| `public static void RemovePlayerForConnection(NetworkConnectionToClient conn, RemovePlayerOptions removeOptions = RemovePlayerOptions.KeepActive)` | 推荐重载。 | `Core/NetworkServer.cs:1386` |
| `public static void SetClientReady(NetworkConnectionToClient conn)` | 置 ready 并重建观察者。 | `Core/NetworkServer.cs:1416` |
| `public static void SetClientNotReady(NetworkConnectionToClient conn)` | 置未 ready。 | `Core/NetworkServer.cs:1499` |
| `public static void SetAllClientsNotReady()` | 全部置未 ready（切场景用）。 | `Core/NetworkServer.cs:1510` |

### 5.7 Spawn / UnSpawn / Destroy

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static bool SpawnObjects()` | 生成场景中所有 `NetworkIdentity` 场景对象；由 `StartServer`/`FinishLoadScene` 调用。 | `Core/NetworkServer.cs:1621` |
| `public static void Spawn(GameObject obj, GameObject ownerPlayer)` | **坑**：`ownerPlayer` 必须是玩家对象（有 NetworkIdentity 且 `connectionToClient != null`），否则 `LogError` 返回；等价于 `AssignClientAuthority`。 | `Core/NetworkServer.cs:1670` |
| `public static void Spawn(GameObject obj, NetworkConnectionToClient ownerConnection = null)` | 主重载；**坑**：返回 `void`，不返回 GameObject；**坑**：只在服务端有效（`!active` 时静默失败/警告）。 | `Core/NetworkServer.cs:1704` |
| `public static void Spawn(GameObject obj, uint assetId, NetworkConnectionToClient ownerConnection = null)` | 强制指定 assetId 后 spawn。 | `Core/NetworkServer.cs:1711` |
| `public static void UnSpawn(GameObject obj)` | 反生成但**不销毁**服务端对象（可复用）；内部 `resetState: true`。 | `Core/NetworkServer.cs:1902` |
| `public static void Destroy(GameObject obj)` | **坑**：`!active` 时只 `LogWarning` 返回；**坑**：场景对象（`sceneId != 0`）实际走 UnSpawn+ResetState，不会被真销毁。 | `Core/NetworkServer.cs:1909` |
| `public static void RebuildObservers(NetworkIdentity identity, bool initialize)` | 重建观察者（`initialize` 表示首次）。 | `Core/NetworkServer.cs:2021` |
| `internal static void NetworkEarlyUpdate()` / `NetworkLateUpdate()` | 主循环驱动，`internal`。 | `Core/NetworkServer.cs:2289,2309` |

---

## 6. `NetworkClient`（`Core/NetworkClient.cs`，共 2091 行，`public static partial class`）

### 6.1 枚举与静态状态

| 签名 | 说明 | 位置 |
|---|---|---|
| `public enum ConnectState { None, Connecting, Connected, Disconnecting, Disconnected }` | 连接状态机。 | `Core/NetworkClient.cs:9-18` |
| `public static int sendRate => NetworkServer.sendRate` | **坑**：客户端 sendRate 是服务端的镜像，不能单独设。 | `Core/NetworkClient.cs:32` |
| `public static float sendInterval => sendRate < int.MaxValue ? 1f / sendRate : 0` | 发送间隔。 | `Core/NetworkClient.cs:33` |
| `public static int unreliableBaselineRate => NetworkServer.unreliableBaselineRate` | 镜像。 | `Core/NetworkClient.cs:38` |
| `public static float unreliableBaselineInterval => NetworkServer.unreliableBaselineInterval` | 镜像。 | `Core/NetworkClient.cs:39` |
| `public static bool unreliableRedundancy => NetworkServer.unreliableRedundancy` | 镜像。 | `Core/NetworkClient.cs:45` |
| `public static bool exceptionsDisconnect = true` | 回调异常即断连。 | `Core/NetworkClient.cs:54` |
| `public static readonly Dictionary<uint, NetworkIdentity> spawned = new(...)` | key = `netId`。 | `Core/NetworkClient.cs:62` |
| `public static NetworkConnectionToServer connection { get; internal set; }` | **坑**：**没有** `NetworkClient.localConnection`；宿主模式下该对象实际是 `LocalConnectionToServer`。 | `Core/NetworkClient.cs:66` |
| `public static bool ready` | 是否已 Ready。 | `Core/NetworkClient.cs:74` |
| `public static NetworkIdentity localPlayer { get; internal set; }` | **坑**：未 AddPlayer 前为 null。 | `Core/NetworkClient.cs:77` |
| `public static bool active => connectState == Connecting \|\| connectState == Connected` | 活跃。 | `Core/NetworkClient.cs:84` |
| `public static bool activeHost => connection is LocalConnectionToServer` | 是否宿主客户端。 | `Core/NetworkClient.cs:89` |
| `public static bool isConnecting => connectState == ConnectState.Connecting` | 连接中。 | `Core/NetworkClient.cs:92` |
| `public static bool isConnected => connectState == ConnectState.Connected` | 已连接。 | `Core/NetworkClient.cs:95` |
| `public static readonly Dictionary<uint, GameObject> prefabs = new(...)` | 已注册 prefab（key = assetId）。 | `Core/NetworkClient.cs:108` |
| `public static InterestManagementBase aoi` | 由 IM 组件写入。 | `Core/NetworkClient.cs:131` |
| `public static bool isLoadingScene` | 正在加载场景。 | `Core/NetworkClient.cs:134` |
| `public static ConnectionQuality connectionQuality = ConnectionQuality.ESTIMATING` | 当前质量。 | `Core/NetworkClient.cs:139` |
| `public static ConnectionQuality lastConnectionQuality = ConnectionQuality.ESTIMATING` | 上次质量。 | `Core/NetworkClient.cs:140` |
| `public static ConnectionQualityMethod connectionQualityMethod = ConnectionQualityMethod.Simple` | 评估方式。 | `Core/NetworkClient.cs:141` |
| `public static float connectionQualityInterval = 3` | 评估间隔。 | `Core/NetworkClient.cs:142` |
| `public static event Action<ConnectionQuality, ConnectionQuality> onConnectionQualityChanged` | 质量变化事件。 | `Core/NetworkClient.cs:149` |

### 6.2 事件

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static Action OnConnectedEvent` | 连接成功。 | `Core/NetworkClient.cs:102` |
| `public static Action OnDisconnectedEvent` | 断开。 | `Core/NetworkClient.cs:103` |
| `public static Action<TransportError, string> OnErrorEvent` | 传输错误。 | `Core/NetworkClient.cs:104` |
| `public static Action<Exception> OnTransportExceptionEvent` | 传输异常。 | `Core/NetworkClient.cs:105` |
| `internal static void OnTransportData(ArraySegment<byte> data, int channelId)` | **坑**：`internal`，不是公开事件。 | `Core/NetworkClient.cs:326` |
| `internal static void OnTransportDisconnected()` | `internal`。 | `Core/NetworkClient.cs:454` |

### 6.3 连接 / 发送 / handler / prefab

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static void Connect(string address)` | 连接指定地址。 | `Core/NetworkClient.cs:209` |
| `public static void Connect(Uri uri)` | 用 Uri 连接。 | `Core/NetworkClient.cs:220` |
| `public static void ConnectHost()` | Host 模式本地连接。 | `Core/NetworkClient.cs:232` |
| `public static void Disconnect()` | 主动断开。 | `Core/NetworkClient.cs:241` |
| `public static void Send<T>(T message, int channelId = Channels.Reliable) where T : struct, NetworkMessage` | 发消息给服务端。 | `Core/NetworkClient.cs:507` |
| `public static void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 注册。 | `Core/NetworkClient.cs:564` |
| `public static void RegisterHandler<T>(Action<T, int> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 带 channelId。 | `Core/NetworkClient.cs:585` |
| `public static void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 覆盖。 | `Core/NetworkClient.cs:607` |
| `public static void ReplaceHandler<T>(Action<T, int> handler, bool requireAuthentication = true) where T : struct, NetworkMessage` | 覆盖。 | `Core/NetworkClient.cs:625` |
| `public static bool UnregisterHandler<T>() where T : struct, NetworkMessage` | 反注册，返回是否成功。 | `Core/NetworkClient.cs:641` |
| `public static bool GetPrefab(uint assetId, out GameObject prefab)` | 按 assetId 反查 prefab。 | `Core/NetworkClient.cs:652` |
| `public static void RegisterPrefab(GameObject prefab, uint newAssetId)` | 注册并覆盖 assetId。 | `Core/NetworkClient.cs:704` |
| `public static void RegisterPrefab(GameObject prefab)` | 按 `NetworkIdentity.assetId` 注册。 | `Core/NetworkClient.cs:736` |
| `public static void RegisterPrefab(GameObject prefab, uint newAssetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)` | 旧式自定义生成器。 | `Core/NetworkClient.cs:758` |
| `public static void RegisterPrefab(GameObject prefab, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)` | 旧式。 | `Core/NetworkClient.cs:772` |
| `public static void RegisterPrefab(GameObject prefab, uint newAssetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)` | 新式生成器。 | `Core/NetworkClient.cs:813` |
| `public static void RegisterPrefab(GameObject prefab, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)` | 新式。 | `Core/NetworkClient.cs:885` |
| `public static void UnregisterPrefab(GameObject prefab)` | 反注册。 | `Core/NetworkClient.cs:949` |
| `public static void RegisterSpawnHandler(uint assetId, SpawnDelegate spawnHandler, UnSpawnDelegate unspawnHandler)` | 按 assetId 注册生成器。 | `Core/NetworkClient.cs:977` |
| `public static void RegisterSpawnHandler(uint assetId, SpawnHandlerDelegate spawnHandler, UnSpawnDelegate unspawnHandler)` | 新式。 | `Core/NetworkClient.cs:995` |
| `public static void UnregisterSpawnHandler(uint assetId)` | 反注册。 | `Core/NetworkClient.cs:1033` |
| `public static void ClearSpawners()` | 清空生成器。 | `Core/NetworkClient.cs:1040` |
| `public static bool Ready()` | 通知服务端本客户端 ready。 | `Core/NetworkClient.cs:1063` |
| `public static bool AddPlayer()` | 请求服务端为自己创建玩家。 | `Core/NetworkClient.cs:1114` |
| `public static void PrepareToSpawnSceneObjects()` | 场景对象生成预处理。 | `Core/NetworkClient.cs:1302` |
| `public static void DestroyAllClientObjects()` | 销毁所有客户端对象。 | `Core/NetworkClient.cs:1900` |
| `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] public static void Shutdown()` | 清理静态状态。 | `Core/NetworkClient.cs:2005` |
| `public static void OnGUI()` | 调试 UI 钩子。 | `Core/NetworkClient.cs:2063` |

---

## 7. 连接类

### 7.1 `NetworkConnection`（抽象基类，`Core/NetworkConnection.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[Serializable] public abstract class NetworkConnection` | 基类。**坑**：**没有** `connectionId` / `address` / `playerController`。 | `Core/NetworkConnection.cs:10` |
| `public const int LocalConnectionId = 0` | 本地连接固定 id。 | `Core/NetworkConnection.cs:12` |
| `public bool isAuthenticated` | 是否已认证。 | `Core/NetworkConnection.cs:15` |
| `[NonSerialized] public object authenticationData` | 认证阶段挂载的任意数据。 | `Core/NetworkConnection.cs:19` |
| `public bool isReady` | 是否 ready。 | `Core/NetworkConnection.cs:25` |
| `public float lastMessageTime` | 最后收包时间（超时判断）。 | `Core/NetworkConnection.cs:28` |
| `public NetworkIdentity identity { get; internal set; }` | **坑**：服务端侧代表"该连接的玩家对象"，未 AddPlayer 时为 null。 | `Core/NetworkConnection.cs:31` |
| `public readonly HashSet<NetworkIdentity> owned = new(...)` | 该连接拥有的对象。 | `Core/NetworkConnection.cs:39` |
| `protected Dictionary<int, Batcher> batches = new(...)` | 按 channel 的批处理器。 | `Core/NetworkConnection.cs:50` |
| `public double remoteTimeStamp { get; internal set; }` | 对端时间戳。 | `Core/NetworkConnection.cs:59` |
| `protected Batcher GetBatchForChannelId(int channelId)` | 取批处理器。 | `Core/NetworkConnection.cs:70` |
| `public void Send<T>(T message, int channelId = Channels.Reliable) where T : struct, NetworkMessage` | **坑**：这是定向发送的正解（`NetworkServer.SendToClient` 不存在）。 | `Core/NetworkConnection.cs:88` |
| `protected abstract void SendToTransport(ArraySegment<byte> segment, int channelId = Channels.Reliable)` | 子类实现。 | `Core/NetworkConnection.cs:143` |
| `public abstract void Disconnect()` | 子类实现。 | `Core/NetworkConnection.cs:195` |
| `public virtual void Cleanup()` | 清理。 | `Core/NetworkConnection.cs:201` |
| `public ... playerController` | **未在源码中找到**（96 已移除）。 | — |

### 7.2 `NetworkConnectionToClient`（`Core/NetworkConnectionToClient.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[Serializable] public class NetworkConnectionToClient : NetworkConnection` | 服务端侧连接。 | `Core/NetworkConnectionToClient.cs:9` |
| `public virtual string address { get; private set; }` | 客户端地址。 | `Core/NetworkConnectionToClient.cs:18` |
| `public readonly int connectionId` | 连接 id（key of `NetworkServer.connections`）。 | `Core/NetworkConnectionToClient.cs:23` |
| `public readonly HashSet<NetworkIdentity> observing = new(...)` | 该连接能看到哪些对象（**坑**：与 `identity.observers` 互为反向索引，由 Mirror 自动维护）。 | `Core/NetworkConnectionToClient.cs:27` |
| `public Unbatcher unbatcher = new Unbatcher()` | 拆批器。 | `Core/NetworkConnectionToClient.cs:30` |
| `public double remoteTimeline` | 对端时间线。 | `Core/NetworkConnectionToClient.cs:40` |
| `public double remoteTimescale` | 对端时间缩放。 | `Core/NetworkConnectionToClient.cs:41` |
| `public double remoteTimelineScaled` | 缩放后时间线。 | `Core/NetworkConnectionToClient.cs:50` |
| `public int snapshotBufferSizeLimit = 64` | 快照缓冲上限。 | `Core/NetworkConnectionToClient.cs:53` |
| `public double rtt => _rtt.Value` | 往返时延。 | `Core/NetworkConnectionToClient.cs:61` |
| `public NetworkConnectionToClient(int networkConnectionId, string clientAddress = "localhost") : base()` | 构造。 | `Core/NetworkConnectionToClient.cs:65` |
| `public override string ToString() => $"connection({connectionId})"` | 调试串。 | `Core/NetworkConnectionToClient.cs:80` |
| `public void OnTimeSnapshot(TimeSnapshot snapshot)` | 时间快照回调。 | `Core/NetworkConnectionToClient.cs:82` |
| `public void UpdateTimeInterpolation()` | 时间插值更新。 | `Core/NetworkConnectionToClient.cs:118` |
| `protected virtual void UpdatePing()` | ping 维护。 | `Core/NetworkConnectionToClient.cs:142` |
| `public override void Disconnect()` | 断开。 | `Core/NetworkConnectionToClient.cs:164` |

### 7.3 `NetworkConnectionToServer`（`Core/NetworkConnectionToServer.cs`，共 24 行）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class NetworkConnectionToServer : NetworkConnection` | 客户端侧连接；**坑**：除 `Disconnect()` 外没有额外 public 成员。 | `Core/NetworkConnectionToServer.cs:6` |
| `public override void Disconnect()` | 置 `isReady=false`、`NetworkClient.ready=false`，再 `Transport.active.ClientDisconnect()`。 | `Core/NetworkConnectionToServer.cs:14` |

### 7.4 本地连接（Host 模式）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class LocalConnectionToClient : NetworkConnectionToClient` | 服务端侧本地连接。 | `Core/LocalConnectionToClient.cs:8` |
| `public LocalConnectionToClient() : base(LocalConnectionId) {}` | 用 `connectionId = 0`。 | `Core/LocalConnectionToClient.cs:15` |
| `protected override void UpdatePing() {}` | 本地无 ping。 | `Core/LocalConnectionToClient.cs:34` |
| `public override void Disconnect()` | 断开。 | `Core/LocalConnectionToClient.cs:74` |
| `public class LocalConnectionToServer : NetworkConnectionToServer` | 客户端侧本地连接。 | `Core/LocalConnectionToServer.cs:9` |
| `public override void Disconnect()` | 断开。 | `Core/LocalConnectionToServer.cs:96` |

---

## 8. `NetworkWriter` / `NetworkReader` / 对象池 / 序列化扩展

### 8.1 `NetworkWriter`（`Core/NetworkWriter.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class NetworkWriter` | 非池化基类。 | `Core/NetworkWriter.cs:10` |
| `public const ushort MaxStringLength = ushort.MaxValue - 1` | **坑**：字符串长度用 ushort 前缀，上限 65534。 | `Core/NetworkWriter.cs:14` |
| `public const int DefaultCapacity = 1500` | 默认缓冲。 | `Core/NetworkWriter.cs:19` |
| `public int Position` | 当前写入位置。 | `Core/NetworkWriter.cs:23` |
| `public int Capacity => buffer.Length` | 容量。 | `Core/NetworkWriter.cs:26` |
| `[MethodImpl(AggressiveInlining)] public void Reset()` | 归零 Position（归还池时必须）。 | `Core/NetworkWriter.cs:42` |
| `public byte[] ToArray()` | 拷贝出 byte[]（有分配）。 | `Core/NetworkWriter.cs:62` |
| `[MethodImpl(AggressiveInlining)] public ArraySegment<byte> ToArraySegment()` | 零拷贝视图（**坑**：归还池后失效）。 | `Core/NetworkWriter.cs:71` |
| `[MethodImpl(AggressiveInlining)] public static implicit operator ArraySegment<byte>(NetworkWriter w)` | 隐式转换。 | `Core/NetworkWriter.cs:76` |
| `public void WriteByte(byte value) => WriteBlittable(value)` | 单字节。 | `Core/NetworkWriter.cs:190` |
| `public void WriteBytes(byte[] array, int offset, int count)` | 原始字节。 | `Core/NetworkWriter.cs:194` |
| `public unsafe bool WriteBytes(byte* ptr, int offset, int size)` | 指针版。 | `Core/NetworkWriter.cs:202` |
| `public void Write<T>(T value)` | **坑**：找不到 writer 时 `LogError("No writer found for ...")` 且**不写入**。 | `Core/NetworkWriter.cs:222` |
| `public static class Writer<T>` | Weaver 填充的静态泛型类。 | `Core/NetworkWriter.cs:245` |
| `public static Action<NetworkWriter, T> write` | **坑**：自定义类型必须赋值此委托，否则 Weaver/运行时找不到 writer。 | `Core/NetworkWriter.cs:247` |

### 8.2 `NetworkReader`（`Core/NetworkReader.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class NetworkReader` | 非池化基类。 | `Core/NetworkReader.cs:18` |
| `public int Position` | 当前读取位置。 | `Core/NetworkReader.cs:28` |
| `public int Remaining => buffer.Count - Position` | 剩余字节。 | `Core/NetworkReader.cs:31` |
| `public int Capacity => buffer.Count` | 容量。 | `Core/NetworkReader.cs:34` |
| `public const int AllocationLimit = 1024 * 1024 * 16` | **坑**：读取集合/字符串的单次分配上限 16MB，超了会报错。 | `Core/NetworkReader.cs:58` |
| `public NetworkReader(ArraySegment<byte> segment)` | 构造。 | `Core/NetworkReader.cs:60` |
| `public NetworkReader(byte[] bytes)` | 构造。 | `Core/NetworkReader.cs:67` |
| `[MethodImpl(AggressiveInlining)] public void SetBuffer(ArraySegment<byte> segment)` | 换缓冲。 | `Core/NetworkReader.cs:76` |
| `public void SetBuffer(byte[] bytes)` | 换缓冲。 | `Core/NetworkReader.cs:84` |
| `public byte ReadByte() => ReadBlittable<byte>()` | 单字节。 | `Core/NetworkReader.cs:182` |
| `public byte[] ReadBytes(byte[] bytes, int count)` | 读入已有数组。 | `Core/NetworkReader.cs:186` |
| `public ArraySegment<byte> ReadBytesSegment(int count)` | 零拷贝视图。 | `Core/NetworkReader.cs:208` |
| `public T Read<T>()` | **坑**：找不到 reader 时 `LogError` 并返回 default。 | `Core/NetworkReader.cs:226` |
| `public static class Reader<T>` | Weaver 填充。 | `Core/NetworkReader.cs:245` |
| `public static Func<NetworkReader, T> read` | **坑**：自定义类型必须赋值此委托。 | `Core/NetworkReader.cs:247` |

### 8.3 内置类型读写清单（`Core/NetworkWriterExtensions.cs` / `Core/NetworkReaderExtensions.cs`）

> 写入方均为 `public static void WriteXxx(this NetworkWriter writer, T value)`；读取方为 `public static T ReadXxx(this NetworkReader reader)`。下面只列类型与行号（W = Writer 扩展行号，R = Reader 扩展行号）。

| 类型 | W | R | 备注 |
|---|---|---|---|
| `byte` / `byte?` | 11 / 12 | 12 / 13 | 可空版成对存在 |
| `sbyte` / `sbyte?` | 14 / 15 | 15 | |
| `char` / `char?` | 18 / 19 | 19 | 以 ushort 传输 |
| `bool` / `bool?` | 22 / 23 | 23 / 24 | 以 byte 传输 |
| `short` / `short?` | 25 / 26 | 30 | |
| `ushort` / `ushort?` | 28 / 29 | 33 | |
| `int` / `int?` | 31 / 32 | 36 / 37 | |
| `uint` / `uint?` | 34 / 35 | 39 | |
| `long` / `long?` | 37 / 38 | 42 | |
| `ulong` / `ulong?` | 40 / 41 | 45 | |
| `float` / `float?` | 51 / 52 | 56 | |
| `double` / `double?` | 54 / 55 | 59 | |
| `decimal` / `decimal?` | 57 / 58 | 62 | |
| `Half` | 60 | 65 | |
| `string` | 62 | 68 | **坑**：null → 长度 0；上限 `MaxStringLength` |
| `byte[]`（带长度） | 96 / 104 | 91 / 108 | `WriteBytesAndSize` / `ReadBytesAndSize` |
| `ArraySegment<byte>`（带长度） | 124 | 122 | |
| `ArraySegment<T>` | 130 | — | 读取侧用 `ReadArray<T>` |
| `Vector2` / `Vector2?` | 150 / 151 | 135 | |
| `Vector3` / `Vector3?` | 153 / 154 | 138 / 139 | |
| `Vector4` / `Vector4?` | 156 / 157 | 141 | |
| `Vector2Int` / `Vector2Int?` | 159 / 160 | 144 | |
| `Vector3Int` / `Vector3Int?` | 162 / 163 | 147 | |
| `Color` / `Color?` | 165 / 166 | 150 | |
| `Color32` / `Color32?` | 168 / 169 | 153 | |
| `Quaternion` / `Quaternion?` | 171 / 172 | 156 | |
| `Rect` / `Rect?` | 175 / 180 | 160 | |
| `Plane` / `Plane?` | 188 / 193 | 164 | |
| `Ray` / `Ray?` | 201 / 206 | 168 | |
| `LayerMask` / `LayerMask?` | 214 / 219 | 172 | |
| `Matrix4x4` / `Matrix4x4?` | 226 / 227 | 183 | |
| `Guid` / `Guid?` | 229 / 244 | 186 | |
| `NetworkIdentity` | 251 | 205 | **坑**：未 spawn 的对象序列化为 0，对端得到 null |
| `NetworkBehaviour` | 272 | 218 | 同上；`ReadNetworkBehaviour<T>` 在 R:246 |
| `NetworkBehaviourSyncVar` | — | 251 | Weaver 内部用 |
| `Transform` | 299 | 265 | **坑**：走 GameObject 通道，未 spawn 会警告 |
| `GameObject` | 319 | 272 | 同上 |
| `List<T>` | 340 | 283 | |
| `HashSet<T>` | 368 | 316 | |
| `T[]` | 388 | 345 | |
| `Uri` | 412 | 378 | |
| `Texture2D` | 417 | 384 | |
| `Sprite` | 443 | 415 | |
| `DateTime` / `DateTime?` | 459 / 464 | 426 | |
| 变长整数 `WriteVarInt/VarUInt/VarLong/VarULong` | 46-49 | 51-54 | 带 `[WeaverPriority]`，压缩更省带宽 |

**未在源码中找到**：`NetworkTime` 的专用读写扩展；`NetworkConnection` 的读写扩展。

### 8.4 自定义类型序列化注册方式

```csharp
// 1) 手写扩展方法（Weaver 自动发现，最常用）
public static void WriteMyType(this NetworkWriter writer, MyType value) { writer.WriteInt(value.x); }
public static MyType ReadMyType(this NetworkReader reader) => new MyType(reader.ReadInt());

// 2) 直接赋值静态委托（Weaver 也会填充同名委托）
Writer<MyType>.write = (writer, value) => writer.WriteInt(value.x);   // Core/NetworkWriter.cs:247
Reader<MyType>.read  = reader => new MyType(reader.ReadInt());        // Core/NetworkReader.cs:247
```

- `[WeaverPriority]`（`Core/Attributes.cs:100`）用于同类型多 Reader/Writer 时指定优先。
- **坑**：`Writer<T>.write` / `Reader<T>.read` 未赋值时运行时报 `No writer found for ...`（`Core/NetworkWriter.cs:227`）。
- **坑**：类型还必须能被 Weaver 识别（出现在 `[SyncVar]`/`[Command]`/`[ClientRpc]`/消息结构体里，或有 `WriteXxx`/`ReadXxx` 扩展），否则 Weaver 不会生成。

### 8.5 对象池

| 签名 | 说明 | 位置 |
|---|---|---|
| `public static class NetworkWriterPool` | Writer 池。 | `Core/NetworkWriterPool.cs:7` |
| `public static int Count => Pool.Count` | 池内数量。 | `Core/NetworkWriterPool.cs:22` |
| `public static NetworkWriterPooled Get()` | 取；**内部已 `Reset()`**。 | `Core/NetworkWriterPool.cs:25` |
| `[MethodImpl(AggressiveInlining)] public static void Return(NetworkWriterPooled writer)` | 归还。 | `Core/NetworkWriterPool.cs:35` |
| `public sealed class NetworkWriterPooled : NetworkWriter, IDisposable` | 池化类型。 | `Core/NetworkWriterPooled.cs:6` |
| `public void Dispose() => NetworkWriterPool.Return(this)` | **坑**：配合 `using` 自动归还。 | `Core/NetworkWriterPooled.cs:8` |
| `public static class NetworkReaderPool` | Reader 池。 | `Core/NetworkReaderPool.cs:8` |
| `public static int Count => Pool.Count` | 池内数量。 | `Core/NetworkReaderPool.cs:21` |
| `public static NetworkReaderPooled Get(byte[] bytes)` | 取并设缓冲。 | `Core/NetworkReaderPool.cs:24` |
| `public static NetworkReaderPooled Get(ArraySegment<byte> segment)` | 取并设缓冲。 | `Core/NetworkReaderPool.cs:33` |
| `[MethodImpl(AggressiveInlining)] public static void Return(NetworkReaderPooled reader)` | 归还。 | `Core/NetworkReaderPool.cs:43` |

**归还规则（源码语义）**：
- **坑**：`Get()` 之后必须 `Return()`，且**必须在同一帧/下一次 `Get()` 之前**归还，否则池会不断新建对象（池初始容量 1000，`Core/NetworkWriterPool.cs:18`）。
- **坑**：`ToArraySegment()` / `ReadBytesSegment()` 返回的是池对象内部缓冲的视图，**归还后即失效**，不能跨帧持有。
- **坑**：归还后不要继续使用该实例（`Reset()` 只重置 Position，缓冲内容仍在）。
- 推荐写法：`using (NetworkWriterPooled w = NetworkWriterPool.Get()) { ... NetworkWriterPool.Return(w); }`——注意 `Dispose` 已自动归还，**不要重复 Return**。

---

## 9. 内置消息类型

### 9.1 消息机制本体

| 签名 | 说明 | 位置 |
|---|---|---|
| `public interface NetworkMessage {}` | **坑**：是接口不是特性；所有消息必须 `struct X : NetworkMessage`。 | `Core/NetworkMessage.cs:3` |
| `public static class NetworkMessageId<T> where T : struct, NetworkMessage` | 按类型算 id。 | `Core/NetworkMessages.cs:12` |
| `public static readonly ushort Id = CalculateId()` | id 由类型名哈希得出。 | `Core/NetworkMessages.cs:20` |
| `public static class NetworkMessages` | 打包/解包工具。 | `Core/NetworkMessages.cs:35` |
| `public const int IdSize = sizeof(ushort)` | 消息头 2 字节。 | `Core/NetworkMessages.cs:38` |
| `public static readonly Dictionary<ushort, Type> Lookup = new(...)` | id → 类型，便于调试。 | `Core/NetworkMessages.cs:42` |
| `public static void LogTypes()` | 打印所有已注册消息类型。 | `Core/NetworkMessages.cs:46` |
| `public static int MaxContentSize(int channelId)` | 单条内容上限。 | `Core/NetworkMessages.cs:64` |
| `public static int MaxMessageSize(int channelId) => MaxContentSize(channelId) + IdSize` | 含头总上限。 | `Core/NetworkMessages.cs:72` |
| `[MethodImpl(AggressiveInlining)] public static ushort GetId<T>() where T : struct, NetworkMessage` | 取 id。 | `Core/NetworkMessages.cs:83` |
| `[MethodImpl(AggressiveInlining)] public static void Pack<T>(T message, NetworkWriter writer) where T : struct, NetworkMessage` | 写入 id + 内容。 | `Core/NetworkMessages.cs:90` |
| `public static bool UnpackId(NetworkReader reader, out ushort messageId)` | 读出 id。 | `Core/NetworkMessages.cs:99` |
| `public static class Channels` | 通道常量。 | `Core/Tools/Utils.cs:28` |
| `public const int Reliable = 0` | 有序可靠。 | `Core/Tools/Utils.cs:30` |
| `public const int Unreliable = 1` | 无序不可靠。 | `Core/Tools/Utils.cs:31` |

### 9.2 全部内置消息（`Core/Messages.cs`，共 222 行）

| 消息类型 | public 字段 | 位置 |
|---|---|---|
| `public struct TimeSnapshotMessage : NetworkMessage` | `public double scaledTime;` | `Core/Messages.cs:12-15` |
| `public struct ReadyMessage : NetworkMessage {}` | 无 | `Core/Messages.cs:17` |
| `public struct NotReadyMessage : NetworkMessage {}` | 无 | `Core/Messages.cs:19` |
| `public struct AddPlayerMessage : NetworkMessage {}` | 无 | `Core/Messages.cs:21` |
| `public struct SceneMessage : NetworkMessage` | `public string sceneName;` `public SceneOperation sceneOperation;` `public bool customHandling;` | `Core/Messages.cs:23-29` |
| `public enum SceneOperation : byte { Normal, LoadAdditive, UnloadAdditive }` | — | `Core/Messages.cs:31-36` |
| `public struct CommandMessage : NetworkMessage` | `public uint netId;` `public byte componentIndex;` `public ushort functionHash;` `public ArraySegment<byte> payload;` | `Core/Messages.cs:38-46` |
| `public struct RpcMessage : NetworkMessage` | 同 CommandMessage 字段 | `Core/Messages.cs:48-56` |
| `[Flags] public enum SpawnFlags : byte { None=0, isOwner=1<<0, isLocalPlayer=1<<1 }` | — | `Core/Messages.cs:58-63` |
| `public struct SpawnMessage : NetworkMessage` | `public uint netId;` `public SpawnFlags spawnFlags;` `public ulong sceneId;` `public uint assetId;` `public Vector3 position;` `public Quaternion rotation;` `public Vector3 scale;` `public ArraySegment<byte> payload;` + 兼容属性 `public bool isOwner {get;set;}` `public bool isLocalPlayer {get;set;}` | `Core/Messages.cs:65-103` |
| `public struct ChangeOwnerMessage : NetworkMessage` | `public uint netId;` `public SpawnFlags spawnFlags;` + 兼容属性 `isOwner` / `isLocalPlayer` | `Core/Messages.cs:105-130` |
| `public struct ObjectSpawnStartedMessage : NetworkMessage {}` | 无 | `Core/Messages.cs:132` |
| `public struct ObjectSpawnFinishedMessage : NetworkMessage {}` | 无 | `Core/Messages.cs:134` |
| `public struct ObjectDestroyMessage : NetworkMessage` | `public uint netId;` | `Core/Messages.cs:136-139` |
| `public struct ObjectHideMessage : NetworkMessage` | `public uint netId;` | `Core/Messages.cs:141-144` |
| `public struct EntityStateMessage : NetworkMessage` | `public uint netId;` `public ArraySegment<byte> payload;` | `Core/Messages.cs:147-153` |
| `public struct EntityStateMessageUnreliableBaseline : NetworkMessage` | `public byte baselineTick;` `public uint netId;` `public ArraySegment<byte> payload;` | `Core/Messages.cs:157-169` |
| `public struct EntityStateMessageUnreliableDelta : NetworkMessage` | `public byte baselineTick;` `public uint netId;` `public ArraySegment<byte> payload;` | `Core/Messages.cs:173-185` |
| `public struct NetworkPingMessage : NetworkMessage` | `public double localTime;` `public double predictedTimeAdjusted;` + 构造函数 `(double, double)` | `Core/Messages.cs:188-202` |
| `public struct NetworkPongMessage : NetworkMessage` | `public double localTime;` `public double predictionErrorUnadjusted;` `public double predictionErrorAdjusted;` + 构造函数 `(double, double, double)` | `Core/Messages.cs:206-221` |

**自定义消息注册（跨引用，详见 5.5 / 6.3）**：
- 服务端：`NetworkServer.RegisterHandler<T>(Action<NetworkConnectionToClient, T> handler, bool requireAuthentication = true)`（`Core/NetworkServer.cs:1090`）
- 客户端：`NetworkClient.RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true)`（`Core/NetworkClient.cs:564`）

---

## 10. NetworkTransform 家族（`Components/NetworkTransform/`）

### 10.1 枚举与基类

| 签名 | 说明 | 位置 |
|---|---|---|
| `public enum CoordinateSpace { Local, World }` | 坐标系。 | `NetworkTransform/NetworkTransformBase.cs:25` |
| `public enum UpdateMethod { Update, FixedUpdate, LateUpdate }` | 驱动方式。 | `NetworkTransform/NetworkTransformBase.cs:26` |
| `public abstract class NetworkTransformBase : NetworkBehaviour` | 三实现共同基类。 | `NetworkTransform/NetworkTransformBase.cs:28` |
| `protected bool IsClientWithAuthority => isClient && authority` | 是否有权改 transform。 | `NetworkTransform/NetworkTransformBase.cs:44` |
| `public readonly SortedList<double, TransformSnapshot> clientSnapshots = new(16)` | 客户端快照缓冲。 | `NetworkTransform/NetworkTransformBase.cs:47` |
| `public readonly SortedList<double, TransformSnapshot> serverSnapshots = new(16)` | 服务端快照缓冲。 | `NetworkTransform/NetworkTransformBase.cs:48` |

### 10.2 基类 public 字段（含默认值）

| 签名 | 默认 | 说明 | 位置 |
|---|---|---|---|
| `public Transform target` | null | 要同步的 Transform（可为子物体）。**坑**：留空则用自身。 | `NetworkTransformBase.cs:36` |
| `public UpdateMethod updateMethod` | `Update` | **坑**：非运动学刚体应选 `FixedUpdate`。 | `NetworkTransformBase.cs:40` |
| `public bool syncPosition` | `true` | **坑**：注释明确"do not change at runtime"。 | `NetworkTransformBase.cs:52` |
| `public bool syncRotation` | `true` | 同上。 | `NetworkTransformBase.cs:53` |
| `public bool syncScale` | `false` | **坑**：默认关闭（省带宽）。 | `NetworkTransformBase.cs:54` |
| `public bool onlySyncOnChange` | `true` | 只在超过敏感度时发送。 | `NetworkTransformBase.cs:58` |
| `public bool compressRotation` | `true` | smallest-three 四元数压缩，**有损**。 | `NetworkTransformBase.cs:60` |
| `public bool interpolatePosition` | `true` | 关掉则位置瞬移。 | `NetworkTransformBase.cs:66` |
| `public bool interpolateRotation` | `true` | 关掉则旋转瞬移。 | `NetworkTransformBase.cs:68` |
| `public bool interpolateScale` | `true` | **坑**：精灵 -X/+X 翻转时建议关掉。 | `NetworkTransformBase.cs:70` |
| `public CoordinateSpace coordinateSpace` | `Local` | 层级变化/根节点非 NT 时用 `World`。 | `NetworkTransformBase.cs:75` |
| `public bool timelineOffset` | `true` | 补偿 NetworkTime 与快照到达的错位。 | `NetworkTransformBase.cs:111` |
| `public bool showGizmos` | false | 调试。 | `NetworkTransformBase.cs:136` |
| `public bool showOverlay` | false | 调试。 | `NetworkTransformBase.cs:137` |
| `public Color overlayColor` | `new Color(0,0,0,0.5f)` | 调试。 | `NetworkTransformBase.cs:138` |
| `public uint sendIntervalMultiplier { get; }` | 由 `syncInterval` 换算 | **坑**：只读派生属性，改 `syncInterval` 才生效。 | `NetworkTransformBase.cs:79-107` |
| `public Vector3 velocity { get; private set; }` | — | 估算速度。 | `NetworkTransformBase.cs:131` |
| `public Vector3 angularVelocity { get; private set; }` | — | 估算角速度。 | `NetworkTransformBase.cs:132` |

**注意**：`syncDirection`、`syncInterval`、`syncMethod`、`syncMode` **不在 NetworkTransformBase 上**，它们继承自 `NetworkBehaviour`（`Core/NetworkBehaviour.cs:33,37,42,54`）。

### 10.3 基类可重写方法

| 签名 | 说明 | 位置 |
|---|---|---|
| `protected override void OnValidate()` | 自动补 `target = transform`。 | `NetworkTransformBase.cs:140` |
| `protected virtual void Configure()` | 初始化缓存。 | `NetworkTransformBase.cs:156` |
| `protected virtual void Awake()` | 缓存。 | `NetworkTransformBase.cs:167` |
| `protected virtual Vector3 GetPosition()` | 按 `coordinateSpace` 取。 | `NetworkTransformBase.cs:176` |
| `protected virtual Quaternion GetRotation()` | 同上。 | `NetworkTransformBase.cs:180` |
| `protected virtual Vector3 GetScale()` | 同上（World 用 `lossyScale`）。 | `NetworkTransformBase.cs:184` |
| `protected virtual void SetPosition(Vector3 position)` | 写回。 | `NetworkTransformBase.cs:188` |
| `protected virtual void SetRotation(Quaternion rotation)` | 写回。 | `NetworkTransformBase.cs:197` |
| `protected virtual void SetScale(Vector3 scale)` | 写回。 | `NetworkTransformBase.cs:206` |
| `protected virtual TransformSnapshot Construct()` | 采当前快照。 | `NetworkTransformBase.cs:218` |
| `protected void AddSnapshot(SortedList<double, TransformSnapshot> snapshots, double timeStamp, Vector3? position, Quaternion? rotation, Vector3? scale)` | 入缓冲。 | `NetworkTransformBase.cs:231` |
| `protected virtual void Apply(TransformSnapshot interpolated, TransformSnapshot endGoal)` | 应用插值结果。 | `NetworkTransformBase.cs:271` |
| `protected virtual void OnTeleport(Vector3 destination)` | 传送钩子。 | `NetworkTransformBase.cs:386` |
| `protected virtual void OnTeleport(Vector3 destination, Quaternion rotation)` | 传送钩子。 | `NetworkTransformBase.cs:410` |
| `public virtual void ResetState()` | 复位。 | `NetworkTransformBase.cs:434` |
| `public virtual void Reset()` | Inspector 复位。 | `NetworkTransformBase.cs:446` |
| `protected virtual void OnEnable()` / `OnDisable()` | 注册/反注册到 NetworkClient 缓冲。 | `NetworkTransformBase.cs:456,464` |
| `protected virtual void OnGUI()` | 调试叠层。 | `NetworkTransformBase.cs:492` |
| `protected virtual void DrawGizmos(...)` / `OnDrawGizmos()` | Gizmo。 | `NetworkTransformBase.cs:521,549` |

### 10.4 传送 API（三种实现共有）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[Command] public void CmdTeleport(Vector3 destination)` | **坑**：`[Command]` 默认 `requiresAuthority=true`，无权威客户端调用会被拒。 | `NetworkTransformBase.cs:299` |
| `[Command] public void CmdTeleport(Vector3 destination, Quaternion rotation)` | 同上。 | `NetworkTransformBase.cs:321` |
| `[ClientRpc] public void RpcTeleport(Vector3 destination)` | 服务端→所有客户端。 | `NetworkTransformBase.cs:343` |
| `[ClientRpc] public void RpcTeleport(Vector3 destination, Quaternion rotation)` | 同上。 | `NetworkTransformBase.cs:359` |
| `[Server] public void ServerTeleport(Vector3 destination, Quaternion rotation)` | **坑**：`[Server]` 在非服务端静默跳过。 | `NetworkTransformBase.cs:373` |

### 10.5 三种实现的差异

| 实现 | 类声明 | 独有 public 字段（默认） | 适用 |
|---|---|---|---|
| `NetworkTransformReliable` | `public class NetworkTransformReliable : NetworkTransformBase`（`Reliable.cs:9`） | `onlySyncOnChangeCorrectionMultiplier = 2`（:16）、`rotationSensitivity = 0.01f`（:20）、`positionPrecision = 0.01f`（:31）、`scalePrecision = 0.01f`（:35） | 走可靠通道 + 位置/缩放量化 + 增量压缩；**带宽最省**，推荐大多数项目（含本项目这类 2D 联机）。 |
| `NetworkTransformUnreliable` | `public class NetworkTransformUnreliable : NetworkTransformBase`（`Unreliable.cs:8`） | `bufferResetMultiplier = 3`（:16）、`positionSensitivity = 0.01f`（:19）、`rotationSensitivity = 0.01f`（:20）、`scaleSensitivity = 0.01f`（:21） | 每 tick 不可靠发送，用敏感度阈值过滤；**最低延迟**，适合硬核竞技；不做量化/增量压缩，带宽最高。 |
| `NetworkTransformHybrid` | `public class NetworkTransformHybrid : NetworkTransformBase`（`Hybrid.cs:10`） | `onlySyncOnChangeCorrectionMultiplier = 2`（:14）、`rotationSensitivity = 0.01f`（:18）、`positionPrecision = 0.01f`（:29）、`rotationPrecision = 0.001f`（:33）、`scalePrecision = 0.01f`（:37）、`debugDraw = false`（:40） | 不可靠通道 + 量化 + 增量压缩（"quake 风格"）；延迟与带宽折中，配置最复杂。 |

**共同点**：都继承 `NetworkTransformBase`，都用 `clientSnapshots`/`serverSnapshots` 做快照插值，都有 `OnSerialize`/`OnDeserialize` 重写与 `OnClientToServerSync`/`OnServerToClientSync` 虚方法。

### 10.6 2D 使用注意事项（**源码实际说了什么**）

- 源码里**没有**任何"2D 专用"的 NetworkTransform 说明或 `#if` 分支；NetworkTransform 同步的是 `Transform`（`NetworkTransformBase.cs:176-206`），**不是** `Rigidbody2D`。
- 与 2D 物理共存要改用 **`NetworkRigidbodyReliable2D` / `NetworkRigidbodyUnreliable2D`**：它们继承 NetworkTransform 并在 `FixedUpdate` 里把无权威方的 `Rigidbody2D.bodyType` 设为 `Kinematic`（`NetworkRigidbodyReliable2D.cs:64-118`），只同步 Transform。
- `interpolateScale` 的 Tooltip 明确提到精灵 -X/+X 翻转场景（`NetworkTransformBase.cs:70`）——2D 精灵翻转建议 `interpolateScale = false`。
- 用 2D 物理时 `updateMethod` 应设 `FixedUpdate`（Tooltip 原文："Select FixedUpdate for non-kinematic rigidbodies."，`NetworkTransformBase.cs:39`）。
- `syncScale` 默认 false（`NetworkTransformBase.cs:54`）——2D 通常不需要同步缩放。
- **未在源码中找到**：关于"Z 轴必须为 0"或"2D 只同步 X/Y"的任何源码注释/逻辑。

---

## 11. `NetworkAnimator` 与 `NetworkRigidbody 2D`

### 11.1 `NetworkAnimator`（`Components/NetworkAnimator.cs`）

| 签名 | 默认 | 说明 | 位置 |
|---|---|---|---|
| `[AddComponentMenu("Network/Network Animator")] public class NetworkAnimator : NetworkBehaviour` | — | 同步 Animator 参数。 | `Components/NetworkAnimator.cs:20` |
| `[Tooltip("Obsolete - use Sync Direction instead")] public bool clientAuthority` | false | **坑**：已废弃，改用继承来的 `syncDirection`。 | `Components/NetworkAnimator.cs:27` |
| `public Animator animator` | null | 被同步的 Animator。 | `Components/NetworkAnimator.cs:35` |
| `protected override void OnValidate()` | — | Editor 校验（自动取 Animator）。 | `Components/NetworkAnimator.cs:101` |
| `public virtual void Reset()` | — | Inspector 复位。 | `Components/NetworkAnimator.cs:119` |
| `public override void OnSerialize(NetworkWriter writer, bool initialState)` | — | 序列化参数。 | `Components/NetworkAnimator.cs:417` |
| `public override void OnDeserialize(NetworkReader reader, bool initialState)` | — | 反序列化。 | `Components/NetworkAnimator.cs:443` |
| `public void SetTrigger(string triggerName)` | — | **坑**：名字不存在时会 `LogWarning`。 | `Components/NetworkAnimator.cs:477` |
| `public void SetTrigger(int hash)` | — | 同上。 | `Components/NetworkAnimator.cs:486` |
| `public void ResetTrigger(string triggerName)` | — | 复位触发器。 | `Components/NetworkAnimator.cs:526` |
| `public void ResetTrigger(int hash)` | — | 复位触发器。 | `Components/NetworkAnimator.cs:533` |

**说明**：源码中没有 `syncInterval` / `onlySyncOnChange` 等字段——同步节奏继承自 `NetworkBehaviour`（`syncInterval` 默认 0）。

### 11.2 `NetworkRigidbodyReliable2D`（`Components/NetworkRigidbody/NetworkRigidbodyReliable2D.cs`，共 135 行）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[AddComponentMenu("Network/Network Rigidbody 2D (Reliable)")] public class NetworkRigidbodyReliable2D : NetworkTransformReliable` | 2D 刚体 + 可靠同步。 | `NetworkRigidbodyReliable2D.cs:7` |
| `bool clientAuthority => syncDirection == SyncDirection.ClientToServer` | **private**（非 public 字段）。 | `NetworkRigidbodyReliable2D.cs:9` |
| `protected override void OnValidate()` | **坑**：`target` 上没有 `Rigidbody2D` 时 `LogWarning`（不是 `[RequireComponent]`）。 | `NetworkRigidbodyReliable2D.cs:14-26` |
| `protected override void Awake()` | **坑**：`target` 缺 `Rigidbody2D` 时 `LogError` 并提前返回（后续 `rb` 为 null 会 NRE）。 | `NetworkRigidbodyReliable2D.cs:29-45` |
| `public override void OnStopServer()` / `OnStopClient()` | 还原原始 `bodyType`（Unity 6000 分支用 `RigidbodyType2D`）。 | `NetworkRigidbodyReliable2D.cs:53,54` |
| `protected override void OnTeleport(Vector3 destination)` | 同步 `rb.position`。 | `NetworkRigidbodyReliable2D.cs:120` |
| `protected override void OnTeleport(Vector3 destination, Quaternion rotation)` | 同步 `rb.position` + `rb.rotation`（取 `eulerAngles.z`）。 | `NetworkRigidbodyReliable2D.cs:127` |

**public 字段：无**（全部继承自 `NetworkTransformReliable` / `NetworkTransformBase` / `NetworkBehaviour`）。

### 11.3 `NetworkRigidbodyUnreliable2D`（`Components/NetworkRigidbody/NetworkRigidbodyUnreliable2D.cs`，共 136 行）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[AddComponentMenu("Network/Network Rigidbody 2D (Unreliable)")] public class NetworkRigidbodyUnreliable2D : NetworkTransformUnreliable` | 2D 刚体 + 不可靠同步。 | `NetworkRigidbodyUnreliable2D.cs:7` |
| 其余成员与 Reliable2D **完全对应**（`OnValidate`/`Awake`/`OnStopServer`/`OnStopClient`/`OnTeleport`×2） | 仅基类不同。 | `NetworkRigidbodyUnreliable2D.cs:14,30,55,56,121,128` |

**选择建议（基于源码）**：两者行为逻辑一致，唯一差别是基类走可靠/不可靠通道——按 10.5 的带宽/延迟取舍选。2D 项目**不要**用 3D 版 `NetworkRigidbodyReliable`/`NetworkRigidbodyUnreliable`（它们操作 `Rigidbody`/`isKinematic`）。

---

## 12. Interest Management（`Core/InterestManagement*.cs` + `Components/InterestManagement/`）

### 12.1 基类

| 签名 | 说明 | 位置 |
|---|---|---|
| `[DisallowMultipleComponent] public abstract class InterestManagementBase : MonoBehaviour` | 低层基类（自己做空间哈希时继承它）。 | `Core/InterestManagementBase.cs:10` |
| `protected virtual void OnEnable()` | **坑**：在这里把 `this` 写入 `NetworkServer.aoi` / `NetworkClient.aoi`，所以同一时刻只能有一个 IM 生效。 | `Core/InterestManagementBase.cs:16-23` |
| `[ServerCallback] public virtual void ResetState() {}` | 复位。 | `Core/InterestManagementBase.cs:26` |
| `public abstract bool OnCheckObserver(NetworkIdentity identity, NetworkConnectionToClient newObserver)` | 必须实现：该连接能否看到该对象。 | `Core/InterestManagementBase.cs:33` |
| `[ServerCallback] public virtual void SetHostVisibility(NetworkIdentity identity, bool visible)` | Host 下用 Renderer/Light/AudioSource/Canvas/Terrain/ParticleSystem 的 enable 来"隐藏"。 | `Core/InterestManagementBase.cs:45` |
| `[ServerCallback] public virtual void OnSpawned(NetworkIdentity identity) {}` | 生成钩子。 | `Core/InterestManagementBase.cs:83` |
| `[ServerCallback] public virtual void OnDestroyed(NetworkIdentity identity) {}` | 销毁钩子。 | `Core/InterestManagementBase.cs:88` |
| `public abstract void Rebuild(NetworkIdentity identity, bool initialize)` | 必须实现。 | `Core/InterestManagementBase.cs:90` |
| `protected void AddObserver(NetworkConnectionToClient connection, NetworkIdentity identity)` | 双向登记。 | `Core/InterestManagementBase.cs:93` |
| `protected void RemoveObserver(NetworkConnectionToClient connection, NetworkIdentity identity)` | 双向移除。 | `Core/InterestManagementBase.cs:100` |
| `public abstract class InterestManagement : InterestManagementBase` | 高层基类（大多数实现继承它）。 | `Core/InterestManagement.cs:11` |
| `public abstract void OnRebuildObservers(NetworkIdentity identity, HashSet<NetworkConnectionToClient> newObservers)` | 子类实现重建逻辑。 | `Core/InterestManagement.cs:34` |
| `[ServerCallback] protected void RebuildAll()` | 全量重建（**坑**：注释提醒在 `Update` 中调用前先判 `NetworkServer.active`）。 | `Core/InterestManagement.cs:43` |
| `public override void Rebuild(NetworkIdentity identity, bool initialize)` | **坑**：`ForceHidden` 会跳过 `OnRebuildObservers`；**坑**：总会把 `identity.connectionToClient` 加进去（玩家永远看得到自己）；**坑**：只把 `isReady` 的连接加入。 | `Core/InterestManagement.cs:51-144` |

**未在源码中找到**：`[InterestManagement]` 特性。

### 12.2 各实现 public 字段

| 实现 | public 字段（默认值） | 位置 |
|---|---|---|
| `DistanceInterestManagement : InterestManagement` | `visRange = 500`（:12）、`minMoveDistance = 0.1f`（Range 0.1-100，:16）、`rebuildInterval = 1`（Range 1-60，:20）、`staticRebuildInterval = 10`（byte，Range 1-60，:25） | `Distance/DistanceInterestManagement.cs` |
| `DistanceInterestManagementCustomRange : NetworkBehaviour` | `visRange = 100`（:13） | `Distance/DistanceInterestManagementCustomRange.cs` |
| `SceneInterestManagement : InterestManagement` | 无 public 字段 | `Scene/SceneInterestManagement.cs:8` |
| `SceneDistanceInterestManagement : InterestManagement` | 同 Distance：`visRange = 500`（:12）、`minMoveDistance = 0.1f`（:16）、`rebuildInterval = 1`（:20）、`staticRebuildInterval = 10`（:25） | `SceneDistance/SceneDistanceInterestManagement.cs` |
| `SpatialHashingInterestManagement : InterestManagement` | `visRange = 30`（:15）、`public int resolution => visRange / 2`（:27）、`rebuildInterval = 1`（:30）、`checkMethod = CheckMethod.XZ_FOR_3D`（:39）、`showSlider = false`（:42） | `SpatialHashing/SpatialHashingInterestManagement.cs` |
| `SpatialHashing3DInterestManagement : InterestManagement` | `visRange = 30`（:15）、`resolution => visRange / 2`（:27）、`rebuildInterval = 1`（:30）、`showSlider = false`（:34） | `SpatialHashing/SpatialHashing3DInterestManagement.cs` |
| `HexSpatialHash2DInterestManagement : InterestManagement` | `rebuildInterval = 1`（byte，Range 1-60，:11）、`staticRebuildInterval = 10`（:14）、`visRange = 1100`（ushort，Range 10-5000，:17）、`minMoveDistance = 1`（ushort，Range 1-100，:20）、`checkMethod = CheckMethod.XZ_FOR_3D`（:23） | `SpatialHashing/HexSpatialHash2DInterestManagement.cs` |
| `HexSpatialHash3DInterestManagement : InterestManagement` | `rebuildInterval = 1`（:11）、`staticRebuildInterval = 10`（:14）、`visRange = 1100`（:17）、`cellHeight = 500`（:20）、`minMoveDistance = 1`（:23） | `SpatialHashing/HexSpatialHash3DInterestManagement.cs` |
| `MatchInterestManagement : InterestManagement` | 无 public 字段 | `Match/MatchInterestManagement.cs:8` |
| `NetworkMatch : NetworkBehaviour` | `public Guid matchId { get; set; }`（:21） | `Match/NetworkMatch.cs` |
| `TeamInterestManagement : InterestManagement` | 无 public 字段 | `Team/TeamInterestManagement.cs:7` |
| `NetworkTeam : NetworkBehaviour` | `public string teamId { get; set; }`（:16）、`forceShown = false`（:37） | `Team/NetworkTeam.cs` |

### 12.3 选择建议（基于源码字段与菜单路径）

| 场景 | 推荐 | 依据 |
|---|---|---|
| 2D 俯视、玩家附近小范围可见（本项目最贴近） | `SpatialHashingInterestManagement`，并把 `checkMethod` 改成 2D（`XY_FOR_2D`） | 菜单名 "Grid Spatial Hash (2D)"，`checkMethod` 有 2D/3D 两档（`SpatialHashingInterestManagement.cs:33-39`） |
| 六边形网格地图 | `HexSpatialHash2DInterestManagement` | 类名与 `checkMethod` 支持 XY（`HexSpatialHash2DInterestManagement.cs:8,23`） |
| 只要简单距离判定、对象数量少 | `DistanceInterestManagement` | `visRange` + `minMoveDistance` + `rebuildInterval` 三个参数即可用 |
| 需要按子场景隔离（Additive 场景） | `SceneInterestManagement` 或 `SceneDistanceInterestManagement` | 后者 = 场景 + 距离（`SceneDistance/SceneDistanceInterestManagement.cs:9`） |
| 分队伍只看到队友 | `TeamInterestManagement` + `NetworkTeam` | `NetworkTeam.forceShown` 用于"玩家对象永远可见"（`Team/NetworkTeam.cs:37`） |
| 按比赛/房间隔离 | `MatchInterestManagement` + `NetworkMatch` | `NetworkMatch.matchId`（`Match/NetworkMatch.cs:21`） |
| 自己写空间索引 | 继承 `InterestManagementBase`（低层、更快） | 源码注释：low level base class 3-5x faster（`Core/InterestManagementBase.cs:1-3`） |

**坑**：`OnEnable` 直接覆盖 `NetworkServer.aoi`/`NetworkClient.aoi`，所以**同一场景不要启用两个 IM 组件**。

---

## 13. Transport：`KcpTransport` / `LatencySimulation`

### 13.1 `KcpTransport`（`Transports/KCP/KcpTransport.cs`）

| 签名 | 默认 | 说明 | 位置 |
|---|---|---|---|
| `[DisallowMultipleComponent] public class KcpTransport : Transport, PortTransport` | — | 默认传输。 | `KCP/KcpTransport.cs:13` |
| `public const string Scheme = "kcp"` | — | URI scheme。 | `KCP/KcpTransport.cs:16` |
| `public ushort port = 7777` | `7777` | **坑**：默认端口 7777。 | `KCP/KcpTransport.cs:21` |
| `public ushort Port { get => port; set => port=value; }` | — | `PortTransport` 接口实现。 | `KCP/KcpTransport.cs:22` |
| `public bool DualMode = true` | `true` | IPv6+IPv4 同时监听。 | `KCP/KcpTransport.cs:24` |
| `public bool NoDelay = true` | `true` | 降延迟、防缓冲打满。 | `KCP/KcpTransport.cs:26` |
| `public uint Interval = 10` | `10` | KCP 内部 tick（ms）。 | `KCP/KcpTransport.cs:28` |
| `public int Timeout = 10000` | `10000` | 超时（ms）。 | `KCP/KcpTransport.cs:30` |
| `public int RecvBufferSize = 1024 * 1027 * 7` | 约 7.3MB | 接收缓冲。 | `KCP/KcpTransport.cs:32` |
| `public int SendBufferSize = 1024 * 1027 * 7` | 约 7.3MB | 发送缓冲。 | `KCP/KcpTransport.cs:34` |
| `public int FastResend = 2` | `2` | 快速重传。 | `KCP/KcpTransport.cs:38` |
| `public uint ReceiveWindowSize = 4096` | `4096` | **坑**：直接影响最大消息尺寸。 | `KCP/KcpTransport.cs:42` |
| `public uint SendWindowSize = 4096` | `4096` | 发送窗口。 | `KCP/KcpTransport.cs:44` |
| `public uint MaxRetransmit = Kcp.DEADLINK * 2` | 2×DEADLINK | 重传上限。 | `KCP/KcpTransport.cs:46` |
| `public bool MaximizeSocketBuffers = true` | `true` | 自动把 socket 缓冲调到 OS 上限。 | `KCP/KcpTransport.cs:49` |
| `[ReadOnly] public int ReliableMaxMessageSize = 0` | 0（OnValidate 填充） | 展示用。 | `KCP/KcpTransport.cs:53` |
| `[ReadOnly] public int UnreliableMaxMessageSize = 0` | 0 | 展示用。 | `KCP/KcpTransport.cs:55` |
| `public bool debugLog` | false | 调试日志。 | `KCP/KcpTransport.cs:71` |
| `public bool statisticsGUI` | false | 统计面板。 | `KCP/KcpTransport.cs:73` |
| `public bool statisticsLog` | false | 统计日志。 | `KCP/KcpTransport.cs:75` |
| `public static int FromKcpChannel(KcpChannel channel)` | — | 通道换算。 | `KCP/KcpTransport.cs:78` |
| `public static KcpChannel ToKcpChannel(int channel)` | — | 通道换算。 | `KCP/KcpTransport.cs:81` |
| `public static TransportError ToTransportError(ErrorCode error)` | — | 错误换算。 | `KCP/KcpTransport.cs:84` |
| `public override bool Available()` | — | **坑**：`UNITY_WEBGL` 恒 false。 | `KCP/KcpTransport.cs:149` |
| `public override int GetMaxPacketSize(int channelId = Channels.Reliable)` | — | 单包上限。 | `KCP/KcpTransport.cs:221` |
| `public override int GetBatchThreshold(int channelId)` | — | 批阈值。 | `KCP/KcpTransport.cs:243` |
| `public long GetAverageMaxSendRate()` / `GetAverageMaxReceiveRate()` | — | 统计。 | `KCP/KcpTransport.cs:249,253` |
| `public static string PrettyBytes(long bytes)` | — | 格式化。 | `KCP/KcpTransport.cs:270` |
| `public override string ToString() => $"KCP [{port}]"` | — | 调试。 | `KCP/KcpTransport.cs:355` |

### 13.2 `LatencySimulation`（`Transports/Latency/LatencySimulation.cs`）

| 签名 | 默认 | 说明 | 位置 |
|---|---|---|---|
| `[DisallowMultipleComponent] public class LatencySimulation : Transport, PortTransport` | — | 包一层 `wrap` 模拟弱网。 | `Latency/LatencySimulation.cs:34` |
| `public Transport wrap` | null | **坑**：必须填真实传输（如 KcpTransport），否则全部转发调用 NRE。 | `Latency/LatencySimulation.cs:36` |
| `public ushort Port { get; set; }` | 转发给 wrap | 端口透传。 | `Latency/LatencySimulation.cs:40` |
| `[Range(0,10000)] public float latency = 100` | `100` | 延迟（**毫秒**，1000 = 1 秒）。 | `Latency/LatencySimulation.cs:67` |
| `[Range(0,1)] public float jitter = 0.02f` | `0.02f` | 抖动比例，用 `perlin(Time * jitterSpeed) * jitter` 计算。 | `Latency/LatencySimulation.cs:71` |
| `public float jitterSpeed = 1` | `1` | 抖动变化速度。 | `Latency/LatencySimulation.cs:75` |
| `[Range(0,100)] public float unreliableLoss = 2` | `2` | **仅不可靠通道**丢包百分比。 | `Latency/LatencySimulation.cs:84` |
| `[Range(0,100)] public float unreliableScramble = 2` | `2` | **仅不可靠通道**乱序百分比。 | `Latency/LatencySimulation.cs:87` |
| `public void Awake()` | — | 初始化。 | `Latency/LatencySimulation.cs:101` |
| `protected virtual float Noise(float time) => Mathf.PerlinNoise(time, time)` | — | 噪声源，可重写。 | `Latency/LatencySimulation.cs:112` |
| `public override bool Available()` | 转发 | — | `Latency/LatencySimulation.cs:192` |
| `public override string ToString() => $"{nameof(LatencySimulation)} {wrap}"` | — | 调试。 | `Latency/LatencySimulation.cs:317` |

**启用/禁用方式**：把 `NetworkManager.transport` 从 KcpTransport 换成 LatencySimulation，并把 `wrap` 指向原 KcpTransport（源码通过 `wrap` 转发所有调用）。**坑**：源码没有内置的运行时开关字段，运行时改参数直接改 `latency` / `unreliableLoss` 等 public 字段即可。

### 13.3 `Transport` 基类（`Core/Transport.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public abstract class Transport : MonoBehaviour` | 传输基类。 | `Core/Transport.cs:33` |
| `public static Transport active` | **坑**：当前激活传输，由 NetworkManager 设置。 | `Core/Transport.cs:36` |
| `public abstract bool Available()` | 平台可用性。 | `Core/Transport.cs:39` |
| `public virtual bool IsEncrypted => false` / `public virtual string EncryptionCipher => ""` | 加密信息。 | `Core/Transport.cs:42,45` |
| `public Action OnClientConnected` / `OnClientDisconnected` | 客户端事件。 | `Core/Transport.cs:49,68` |
| `public Action<ArraySegment<byte>, int> OnClientDataReceived` / `OnClientDataSent` | 客户端数据事件。 | `Core/Transport.cs:52,59` |
| `public Action<TransportError, string> OnClientError` / `public Action<Exception> OnClientTransportException` | 客户端错误。 | `Core/Transport.cs:62,65` |
| `[Obsolete] public Action<int> OnServerConnected` | 已废弃，改用带地址版。 | `Core/Transport.cs:74` |
| `public Action<int, string> OnServerConnectedWithAddress` | 服务端连接（含地址）。 | `Core/Transport.cs:77` |
| `public Action<int, ArraySegment<byte>, int> OnServerDataReceived` / `OnServerDataSent` | 服务端数据。 | `Core/Transport.cs:80,87` |
| `public Action<int, TransportError, string> OnServerError` / `public Action<int, Exception> OnServerTransportException` | 服务端错误。 | `Core/Transport.cs:91,95` |
| `public Action<int> OnServerDisconnected` | 服务端断开。 | `Core/Transport.cs:98` |
| `public abstract bool ClientConnected()` / `ClientConnect(string address)` / `ClientSend(ArraySegment<byte>, int)` / `ClientDisconnect()` | 客户端抽象。 | `Core/Transport.cs:102,105,117,120` |
| `public virtual void ClientConnect(Uri uri)` | 默认实现。 | `Core/Transport.cs:108` |
| `public abstract Uri ServerUri()` / `ServerActive()` / `ServerStart()` / `ServerSend(int, ArraySegment<byte>, int)` / `ServerDisconnect(int)` / `ServerGetClientAddress(int)` / `ServerStop()` | 服务端抽象。 | `Core/Transport.cs:125,128,131,134,137,141,144` |
| `public abstract int GetMaxPacketSize(int channelId = Channels.Reliable)` | 单包上限。 | `Core/Transport.cs:152` |
| `public virtual int GetBatchThreshold(int channelId = Channels.Reliable)` | 批阈值。 | `Core/Transport.cs:159` |
| `public void Update() {}` / `public void LateUpdate() {}` | **坑**：空实现，防 Unity 报未使用。 | `Core/Transport.cs:177,178` |
| `public virtual void ClientEarlyUpdate() {}` / `ServerEarlyUpdate() {}` / `ClientLateUpdate() {}` / `ServerLateUpdate() {}` | 更新钩子。 | `Core/Transport.cs:193-196` |
| `public abstract void Shutdown()` | 关闭。 | `Core/Transport.cs:199` |
| `public virtual void OnApplicationQuit()` | 退出。 | `Core/Transport.cs:203` |
| `protected static Uri TryBuildValidUri(string scheme, string hostname, int port)` | URI 构造工具。 | `Core/Transport.cs:223` |

---

## 14. `NetworkDiscovery`（`Components/Discovery/`）

| 签名 | 默认 | 说明 | 位置 |
|---|---|---|---|
| `[DisallowMultipleComponent] public abstract class NetworkDiscoveryBase<Request, Response> : MonoBehaviour where Request : NetworkMessage where Response : NetworkMessage` | — | 基类。 | `Discovery/NetworkDiscoveryBase.cs:20` |
| `public static bool SupportedOnThisPlatform { get; }` | — | **坑**：`WebGLPlayer` 返回 false。 | `Discovery/NetworkDiscoveryBase.cs:24` |
| `public bool enableActiveDiscovery = true` | `true` | 定期广播发现请求。 | `Discovery/NetworkDiscoveryBase.cs:28` |
| `public string BroadcastAddress = ""` | `""` | **坑**：iOS 可能需要填局域网 IP。 | `Discovery/NetworkDiscoveryBase.cs:33` |
| `protected int serverBroadcastListenPort = 47777` | `47777` | **坑**：`protected`，Inspector 可见但代码外部不可访问。 | `Discovery/NetworkDiscoveryBase.cs:37` |
| `public Transport transport` | null | 要广播的传输。 | `Discovery/NetworkDiscoveryBase.cs:45` |
| `public ServerFoundUnityEvent<Response> OnServerFound` | — | 发现服务器事件。 | `Discovery/NetworkDiscoveryBase.cs:48` |
| `[HideInInspector] public long secretHandshake` | 0 | 握手密钥。 | `Discovery/NetworkDiscoveryBase.cs:53` |
| `public long ServerId { get; private set; }` | — | 本机服务器 id。 | `Discovery/NetworkDiscoveryBase.cs:55` |
| `public virtual void OnValidate()` | — | 校验。 | `Discovery/NetworkDiscoveryBase.cs:61` |
| `public virtual void Start()` | — | 启动。 | `Discovery/NetworkDiscoveryBase.cs:77` |
| `public static long RandomLong()` | — | 随机密钥。 | `Discovery/NetworkDiscoveryBase.cs:94` |
| `public void AdvertiseServer()` | — | 开始广播自己。 | `Discovery/NetworkDiscoveryBase.cs:159` |
| `public async Task ServerListenAsync()` | — | 服务端监听循环。 | `Discovery/NetworkDiscoveryBase.cs:179` |
| `protected virtual void ProcessClientRequest(Request request, IPEndPoint endpoint)` | — | 处理请求。 | `Discovery/NetworkDiscoveryBase.cs:228` |
| `protected abstract Response ProcessRequest(Request request, IPEndPoint endpoint)` | — | 子类实现。 | `Discovery/NetworkDiscoveryBase.cs:265` |
| `public void StartDiscovery()` | — | 开始搜索服务器。 | `Discovery/NetworkDiscoveryBase.cs:310` |
| `public void StopDiscovery()` | — | 停止搜索。 | `Discovery/NetworkDiscoveryBase.cs:342` |
| `public async Task ClientListenAsync()` | — | 客户端监听循环。 | `Discovery/NetworkDiscoveryBase.cs:352` |
| `public void BroadcastDiscoveryRequest()` | — | 广播一次请求。 | `Discovery/NetworkDiscoveryBase.cs:386` |
| `protected virtual Request GetRequest() => default` | — | 子类覆写。 | `Discovery/NetworkDiscoveryBase.cs:440` |
| `protected abstract void ProcessResponse(Response response, IPEndPoint endpoint)` | — | 子类实现。 | `Discovery/NetworkDiscoveryBase.cs:469` |
| `[Serializable] public class ServerFoundUnityEvent<TResponseType> : UnityEvent<TResponseType> {}` | — | 事件类型。 | `Discovery/NetworkDiscovery.cs:9` |
| `[DisallowMultipleComponent] public class NetworkDiscovery : NetworkDiscoveryBase<ServerRequest, ServerResponse>` | — | 具体实现。 | `Discovery/NetworkDiscovery.cs:13` |
| `protected override ServerResponse ProcessRequest(ServerRequest request, IPEndPoint endpoint)` | — | 返回本机信息。 | `Discovery/NetworkDiscovery.cs:27` |
| `protected override ServerRequest GetRequest() => new ServerRequest()` | — | 空请求。 | `Discovery/NetworkDiscovery.cs:62` |
| `protected override void ProcessResponse(ServerResponse response, IPEndPoint endpoint)` | — | 触发 `OnServerFound`。 | `Discovery/NetworkDiscovery.cs:73` |
| `[RequireComponent(typeof(NetworkDiscovery))] public class NetworkDiscoveryHUD : MonoBehaviour` | — | 调试 HUD。 | `Discovery/NetworkDiscoveryHUD.cs:11` |
| `public NetworkDiscovery networkDiscovery` | — | HUD 引用。 | `Discovery/NetworkDiscoveryHUD.cs:16` |
| `public void OnDiscoveredServer(ServerResponse info)` | — | 回调。 | `Discovery/NetworkDiscoveryHUD.cs:138` |

**典型用法**：服务端 `AdvertiseServer()`；客户端 `StartDiscovery()` + 订阅 `OnServerFound`；找到后把 `NetworkManager.networkAddress` 设为 `response.EndPoint.Address` 再 `StartClient()`。

**Android 支持**：源码**有** Android 专属分支 —— `NetworkDiscoveryBase.cs:267-291,295` 里有
「Android Multicast fix」：`#if UNITY_ANDROID` + `RuntimePlatform.Android` 下获取 `WifiManager.MulticastLock`
（并配 `Editor/AndroidManifestHelper.cs:9,17,23` 处理清单权限）；`NetworkReader.cs:149` / `NetworkWriter.cs:153` 也有 `#if UNITY_ANDROID` 分支。
另有 iOS 的 broadcast address 处理（`NetworkDiscoveryBase.cs:30-32`）。**坑**：Android 上缺 `ACCESS_WIFI_STATE` / `CHANGE_WIFI_MULTICAST_STATE`
权限时多播收不到包，这属工程配置而不是源码能兜住的。

---

## 15. 同步集合

### 15.1 `SyncObject`（`Core/SyncObject.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public abstract class SyncObject` | 所有同步集合基类。 | `Core/SyncObject.cs:13` |
| `public Action OnDirty` | 变脏回调（Weaver 生成的构造函数会挂）。 | `Core/SyncObject.cs:19` |
| `public Func<bool> IsRecording = () => true` | 是否记录变更。 | `Core/SyncObject.cs:29` |
| `public Func<bool> IsWritable = () => true` | **坑**：`IsReadOnly` 属性即取反此委托。 | `Core/SyncObject.cs:35` |
| `public abstract void ClearChanges()` | 清变更记录。 | `Core/SyncObject.cs:39` |
| `public abstract void OnSerializeAll(NetworkWriter writer)` | 全量序列化。 | `Core/SyncObject.cs:42` |
| `public abstract void OnSerializeDelta(NetworkWriter writer)` | 增量序列化。 | `Core/SyncObject.cs:45` |
| `public abstract void OnDeserializeAll(NetworkReader reader)` | 全量反序列化。 | `Core/SyncObject.cs:48` |
| `public abstract void OnDeserializeDelta(NetworkReader reader)` | 增量反序列化。 | `Core/SyncObject.cs:51` |
| `public abstract void Reset()` | 复位。 | `Core/SyncObject.cs:54` |

### 15.2 `SyncList<T>`（`Core/SyncList.cs`，共 524 行）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class SyncList<T> : SyncObject, IList<T>, IReadOnlyList<T>` | 声明。 | `Core/SyncList.cs:7` |
| `public Action<int> OnAdd` | 添加后（新索引）。 | `Core/SyncList.cs:10` |
| `public Action<int> OnInsert` | 插入后（索引）。 | `Core/SyncList.cs:13` |
| `public Action<int, T> OnSet` | 赋值后（索引 + **旧值**）。 | `Core/SyncList.cs:16` |
| `public Action<int, T> OnRemove` | 移除后（索引 + **旧值**）。 | `Core/SyncList.cs:19` |
| `public Action OnClear` | **坑**：清空**之前**触发，可遍历。 | `Core/SyncList.cs:22` |
| `public enum Operation : byte { OP_ADD, OP_SET, OP_INSERT, OP_REMOVEAT, OP_CLEAR }` | 操作码。 | `Core/SyncList.cs:24-31` |
| `public Action<Operation, int, T> OnChange` | 所有变更（三参）。 | `Core/SyncList.cs:40` |
| `public Action<Operation, int, T, T> Callback` | 所有变更（四参：op, index, oldItem, newItem）。 | `Core/SyncList.cs:48` |
| `public int Count => objects.Count` | 数量。 | `Core/SyncList.cs:53` |
| `public bool IsReadOnly => !IsWritable()` | 只读。 | `Core/SyncList.cs:54` |
| `public SyncList()` / `public SyncList(IEqualityComparer<T> comparer)` / `public SyncList(IList<T> objects, IEqualityComparer<T> comparer = null)` | 构造。 | `Core/SyncList.cs:75,77,83` |
| `public override void ClearChanges()` / `public override void Reset()` | 覆写。 | `Core/SyncList.cs:91,93` |
| `public void Add(T item)` | 添加。 | `Core/SyncList.cs:366` |
| `public void AddRange(IEnumerable<T> range)` | 批量添加。 | `Core/SyncList.cs:372` |
| `public void Clear()` | 清空。 | `Core/SyncList.cs:378` |
| `public bool Contains(T item)` | 包含。 | `Core/SyncList.cs:386` |
| `public void CopyTo(T[] array, int index)` | 拷贝。 | `Core/SyncList.cs:388` |
| `public int IndexOf(T item)` | 索引。 | `Core/SyncList.cs:390` |
| `public int FindIndex(Predicate<T> match)` | 查找。 | `Core/SyncList.cs:398` |
| `public T Find(Predicate<T> match)` | 查找。 | `Core/SyncList.cs:406` |
| `public List<T> FindAll(Predicate<T> match)` | **坑**：返回新 `List<T>`，有分配。 | `Core/SyncList.cs:412` |
| `public void Insert(int index, T item)` | 插入。 | `Core/SyncList.cs:421` |
| `public void InsertRange(int index, IEnumerable<T> range)` | 批量插入。 | `Core/SyncList.cs:427` |
| `public bool Remove(T item)` | 移除。 | `Core/SyncList.cs:436` |
| `public void RemoveAt(int index)` | 按索引移除。 | `Core/SyncList.cs:446` |
| `public int RemoveAll(Predicate<T> match)` | 批量移除。 | `Core/SyncList.cs:453` |
| `public T this[int i] { get; set; }` | 索引器。 | `Core/SyncList.cs:466` |
| `public Enumerator GetEnumerator() => new Enumerator(this)` | **坑**：自定义 struct 枚举器（无装箱）。 | `Core/SyncList.cs:480` |
| `public struct Enumerator : IEnumerator<T>` | 枚举器。 | `Core/SyncList.cs:496` |

### 15.3 `SyncDictionary`（`Core/SyncDictionary.cs`）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class SyncIDictionary<TKey, TValue> : SyncObject, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>` | 基类。 | `Core/SyncDictionary.cs:7` |
| `public Action<TKey> OnAdd` | 添加后。 | `Core/SyncDictionary.cs:10` |
| `public Action<TKey, TValue> OnSet` | 赋值后。 | `Core/SyncDictionary.cs:13` |
| `public Action<TKey, TValue> OnRemove` | 移除后（旧值）。 | `Core/SyncDictionary.cs:16` |
| `public Action OnClear` | 清空前。 | `Core/SyncDictionary.cs:19` |
| `public enum Operation : byte` | 操作码（同 SyncList 风格）。 | `Core/SyncDictionary.cs:21` |
| `public Action<Operation, TKey, TValue> OnChange` | 所有变更。 | `Core/SyncDictionary.cs:35` |
| `public SyncIDictionary(IDictionary<TKey, TValue> objects)` | 构造。 | `Core/SyncDictionary.cs:39` |
| `public int Count => objects.Count` | 数量。 | `Core/SyncDictionary.cs:44` |
| `public bool IsReadOnly => !IsWritable()` | 只读。 | `Core/SyncDictionary.cs:45` |
| `public ICollection<TKey> Keys => objects.Keys` / `public ICollection<TValue> Values => objects.Values` | 键/值。 | `Core/SyncDictionary.cs:67,69` |
| `public TValue this[TKey i] { get; set; }` | 索引器。 | `Core/SyncDictionary.cs:263` |
| `public bool TryGetValue(TKey key, out TValue value)` | 查询。 | `Core/SyncDictionary.cs:282` |
| `public bool ContainsKey(TKey key)` | 查询。 | `Core/SyncDictionary.cs:284` |
| `public bool Contains(KeyValuePair<TKey, TValue> item)` | 查询。 | `Core/SyncDictionary.cs:286` |
| `public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)` | 拷贝。 | `Core/SyncDictionary.cs:288` |
| `public void Add(KeyValuePair<TKey, TValue> item)` / `public void Add(TKey key, TValue value)` | 添加。 | `Core/SyncDictionary.cs:304,306` |
| `public bool Remove(TKey key)` / `public bool Remove(KeyValuePair<TKey, TValue> item)` | 移除。 | `Core/SyncDictionary.cs:312,322` |
| `public void Clear()` | 清空。 | `Core/SyncDictionary.cs:331` |
| `public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()` | 枚举。 | `Core/SyncDictionary.cs:416` |
| `public class SyncDictionary<TKey, TValue> : SyncIDictionary<TKey, TValue>` | 常用子类。 | `Core/SyncDictionary.cs:421` |
| `public SyncDictionary()` / `(IEqualityComparer<TKey> eq)` / `(IDictionary<TKey, TValue> d)` | 构造。 | `Core/SyncDictionary.cs:423,424,425` |
| `public new Dictionary<TKey, TValue>.ValueCollection Values` / `public new Dictionary<TKey, TValue>.KeyCollection Keys` / `public new Dictionary<TKey, TValue>.Enumerator GetEnumerator()` | **坑**：`new` 隐藏基类成员，用具体类型变量才有无分配枚举器。 | `Core/SyncDictionary.cs:426,427,428` |

### 15.4 `SyncSet` / `SyncHashSet` / `SyncSortedSet`（全部在 `Core/SyncSet.cs`，共 417 行）

| 签名 | 说明 | 位置 |
|---|---|---|
| `public class SyncSet<T> : SyncObject, ISet<T>` | 基类。 | `Core/SyncSet.cs:7` |
| `public Action<T> OnAdd` | 添加后（新值）。 | `Core/SyncSet.cs:10` |
| `public Action<T> OnRemove` | 移除后（旧值）。 | `Core/SyncSet.cs:13` |
| `public Action OnClear` | 清空前。 | `Core/SyncSet.cs:16` |
| `public enum Operation : byte { OP_ADD, OP_REMOVE, OP_CLEAR }` | 操作码。 | `Core/SyncSet.cs:18-23` |
| `public Action<Operation, T> OnChange` | 所有变更。 | `Core/SyncSet.cs:31` |
| `protected readonly ISet<T> objects` | 底层集合。 | `Core/SyncSet.cs:33` |
| `public int Count => objects.Count` / `public bool IsReadOnly => !IsWritable()` | 属性。 | `Core/SyncSet.cs:35,36` |
| `public SyncSet(ISet<T> objects)` | 构造。 | `Core/SyncSet.cs:57` |
| `public bool Add(T item)` | 添加，返回是否真的加入。 | `Core/SyncSet.cs:293` |
| `public void Clear()` | 清空。 | `Core/SyncSet.cs:309` |
| `public bool Contains(T item) => objects.Contains(item)` | 包含。 | `Core/SyncSet.cs:317` |
| `public void CopyTo(T[] array, int index)` | 拷贝。 | `Core/SyncSet.cs:319` |
| `public class SyncHashSet<T> : SyncSet<T>` | 无序集合。 | `Core/SyncSet.cs:400` |
| `public SyncHashSet()` / `public SyncHashSet(IEqualityComparer<T> comparer)` | 构造。 | `Core/SyncSet.cs:402,403` |
| `public new HashSet<T>.Enumerator GetEnumerator()` | 无分配枚举器。 | `Core/SyncSet.cs:406` |
| `public class SyncSortedSet<T> : SyncSet<T>` | 有序集合。 | `Core/SyncSet.cs:409` |
| `public SyncSortedSet()` / `public SyncSortedSet(IComparer<T> comparer)` | 构造。 | `Core/SyncSet.cs:411,412` |
| `public new SortedSet<T>.Enumerator GetEnumerator()` | 无分配枚举器。 | `Core/SyncSet.cs:415` |

### 15.5 声明方式与坑

```csharp
public class Player : NetworkBehaviour
{
    // 必须在声明处 inline new，且建议 readonly
    public readonly SyncList<Item> items = new SyncList<Item>();
    public readonly SyncDictionary<int, string> names = new SyncDictionary<int, string>();
    public readonly SyncHashSet<int> ids = new SyncHashSet<int>();
    public readonly SyncSortedSet<int> ranks = new SyncSortedSet<int>();
}
```

- **坑**：`SyncObjectProcessor.cs:46` 会对非 `readonly` 字段发 **Warning**："...should have a 'readonly' keyword in front of the variable because SyncObjects always need to be initialized by the Weaver."
- **坑**：`SyncObjectProcessor.cs:29` — 静态 SyncObject 直接 **Error**："`{name}` cannot be static"。
- **坑**：`MonoBehaviourProcessor.cs:27` — SyncObject 必须在 `NetworkBehaviour` 内，否则 Error。
- **坑**：`SyncObjectProcessor.cs:61` — 每个类 SyncObject 数量有上限（`SyncObjectsLimit`），超了报 Error。
- **坑**：集合回调在客户端反序列化时也会触发（服务端本地修改同样触发），别在回调里做只在单侧成立的事。
- 初始状态在 spawn 时通过 `OnSerializeAll` 全量下发。

---

## 16. `NetworkStartPosition` 与 Weaver

### 16.1 `NetworkStartPosition`（`Core/NetworkStartPosition.cs`，全文 21 行）

| 签名 | 说明 | 位置 |
|---|---|---|
| `[DisallowMultipleComponent] [AddComponentMenu("Network/Network Start Position")] public class NetworkStartPosition : MonoBehaviour` | **坑**：继承 `MonoBehaviour`，不是 `NetworkBehaviour`。 | `Core/NetworkStartPosition.cs:9` |
| `public void Awake()` | 调 `NetworkManager.RegisterStartPosition(transform)`。 | `Core/NetworkStartPosition.cs:11-14` |
| `public void OnDestroy()` | 调 `NetworkManager.UnRegisterStartPosition(transform)`。 | `Core/NetworkStartPosition.cs:16-19` |

**与 NetworkManager 的联动**：
- `NetworkManager.RegisterStartPosition(Transform start)`（`Core/NetworkManager.cs:1106`）：加入列表后按 `GetSiblingIndex()` **重排**（注释说明：假设所有出生点是兄弟节点）。
- `NetworkManager.UnRegisterStartPosition(Transform start)`（`Core/NetworkManager.cs:1121`）。
- `NetworkManager.GetStartPosition()`（`Core/NetworkManager.cs:1128`）：先 `RemoveAll(t => t == null)` 清死引用；**列表为空返回 null**；`Random` 用 `UnityEngine.Random.Range(0, Count)`；`RoundRobin` 用 `startPositionIndex` 取模自增。
- `NetworkManager.OnServerAddPlayer`（`Core/NetworkManager.cs:1358`）默认实现里：`startPos != null ? Instantiate(playerPrefab, startPos.position, startPos.rotation) : Instantiate(playerPrefab)`——**坑**：出生点为空时直接原地实例化，不报错。

### 16.2 Weaver 触发条件

| 环节 | 说明 | 位置 |
|---|---|---|
| `public class ILPostProcessorHook : ILPostProcessor` | Unity 2020.3+ 走 ILPostProcessor（**本项目即走这条**）。 | `Editor/Weaver/EntryPointILPostProcessor/ILPostProcessorHook.cs:21` |
| `public override bool WillProcess(ICompiledAssembly compiledAssembly)` | **触发条件**：程序集名 == `"Mirror"`，**或**该程序集引用了 Mirror.dll；且**未定义** `ILPP_IGNORE` 宏。 | `ILPostProcessorHook.cs:44-58` |
| `public const string IgnoreDefine = "ILPP_IGNORE"` | 用于跳过 weaving 的宏。 | `ILPostProcessorHook.cs:30` |
| `public override ILPostProcessResult Process(ICompiledAssembly compiledAssembly)` | 读 PE/PDB → `new Weaver(Log).Weave(asmDef, asmResolver, out bool modified)` → 有修改才回写。 | `ILPostProcessorHook.cs:60-140` |
| `public bool Weave(AssemblyDefinition assembly, IAssemblyResolver resolver, out bool modified)` | **幂等**：若已含 `GeneratedNetworkCode` 类则直接 return true（防重复 weaving）。 | `Editor/Weaver/Weaver.cs:183-201` |
| `void ToggleWeaverFuse()` | 若被 weave 的是 Mirror.dll 本身，把 `WeaverFuse.Weaved()` 的 IL 从 `return false` 改成 `return true`。 | `Editor/Weaver/Weaver.cs:155-163,252-255` |
| `EntryPoint/CompilationFinishedHook.cs` | 旧版（2020.3 以下）入口，本项目不生效。 | `Editor/Weaver/EntryPoint/CompilationFinishedHook.cs` |
| `EntryPoint/EnterPlayModeHook.cs` | 进入播放前的检查钩子。 | `Editor/Weaver/EntryPoint/EnterPlayModeHook.cs` |
| `public static class WeaverFuse` | 运行时自检。 | `Core/WeaverFuse.cs:11` |
| `public static bool Weaved()` | **坑**：源码实现是 `#if UNITY_2020_3_OR_NEWER → false; #else → true;`（`Core/WeaverFuse.cs:15-20`）——即"编译期常量"，真正翻转靠 Weaver 改 IL。 | `Core/WeaverFuse.cs:15` |

### 16.3 常见 Weaver 报错文案（原文摘录）

| 报错文案（英文原文片段） | 触发原因 | 位置 |
|---|---|---|
| `"{fd.Name} is a SyncObject and must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour"` | SyncList 等放在非 NetworkBehaviour 类里 | `Processors/MonoBehaviourProcessor.cs:27` |
| `"SyncVar {fd.Name} must be inside a NetworkBehaviour.  {td.Name} is not a NetworkBehaviour"` | `[SyncVar]` 放在非 NetworkBehaviour 类里 | `Processors/MonoBehaviourProcessor.cs:21` |
| `"Command {md.Name} must be declared inside a NetworkBehaviour"` | `[Command]` 位置错误 | `Processors/MonoBehaviourProcessor.cs:40` |
| `"ClientRpc {md.Name} must be declared inside a NetworkBehaviour"` | `[ClientRpc]` 位置错误 | `Processors/MonoBehaviourProcessor.cs:45` |
| `"TargetRpc {md.Name} must be declared inside a NetworkBehaviour"` | `[TargetRpc]` 位置错误 | `Processors/MonoBehaviourProcessor.cs:50` |
| `"{fd.Name} cannot be static"` | SyncObject / SyncVar 是 static | `Processors/SyncObjectProcessor.cs:29`、`SyncVarAttributeProcessor.cs:462` |
| `"{fd.Name} should have a 'readonly' keyword in front of the variable because {typeof(SyncObject)}s always need to be initialized by the Weaver."` | SyncList 未加 `readonly`（**Warning**） | `Processors/SyncObjectProcessor.cs:46` |
| `"{td.Name} has > {SyncObjectsLimit} SyncObjects (SyncLists etc). Consider refactoring your class into multiple components"` | SyncObject 超上限 | `Processors/SyncObjectProcessor.cs:61` |
| `"{fd.Name} has generic type. Generic SyncVars are not supported"` | 泛型 SyncVar | `Processors/SyncVarAttributeProcessor.cs:469` |
| `"{fd.Name} has [SyncVar] attribute. SyncLists should not be marked with SyncVar"` | SyncList 上误加 `[SyncVar]`（**Warning**） | `Processors/SyncVarAttributeProcessor.cs:476` |
| `"{td.Name} has > {SyncVarLimit} SyncVars. Consider refactoring your class into multiple components"` | SyncVar 超过 64 上限 | `Processors/SyncVarAttributeProcessor.cs:487` |
| `"Could not find hook for '{syncVar.Name}', hook name '{hookFunctionName}'. "` | `[SyncVar(hook=...)]` 指向的方法不存在 | `Processors/SyncVarAttributeProcessor.cs:134` |
| `"Wrong type for Parameter in hook for '{syncVar.Name}', hook name '{hookFunctionName}'. "` | hook 方法参数类型不对 | `Processors/SyncVarAttributeProcessor.cs:150` |
| `"{syncVar.Name} has unsupported type. Use a supported Mirror type instead"` | SyncVar 类型不支持 | `Processors/NetworkBehaviourProcessor.cs:487,561,661` |
| `"{method.Name} has invalid parameter {param}.  Unsupported type {param.ParameterType},  use a supported Mirror type instead"` | Cmd/Rpc 参数类型不支持 | `Processors/NetworkBehaviourProcessor.cs:794` |
| `"{method.Name} must not be static"` | `[Command]`/`[ClientRpc]`/`[TargetRpc]` 方法为 static | `Processors/NetworkBehaviourProcessor.cs:828` |
| `"{md.Name} cannot be a coroutine"` | 远程调用方法写成协程 | `Processors/NetworkBehaviourProcessor.cs:842` |
| `"{md.Name} cannot return a value.  Make it void instead"` | 远程调用方法有返回值 | `Processors/NetworkBehaviourProcessor.cs:848` |
| `"{md.Name} cannot have generic parameters"` / `"{method.Name} cannot have generic parameters"` | 远程调用方法带泛型参数 | `Processors/NetworkBehaviourProcessor.cs:854,881` |
| `"{method.Name} cannot have out parameters"` | 远程调用方法带 `out` 参数 | `Processors/NetworkBehaviourProcessor.cs:891` |
| `"{method.Name} has invalid parameter {param}, Cannot pass NetworkConnections. Instead use 'NetworkConnectionToClient conn = null' to get the sender's connection on the server"` | Cmd 里传 `NetworkConnection` | `Processors/NetworkBehaviourProcessor.cs:901` |
| `"{method.Name} has invalid parameter {param}. Cannot pass NetworkConnections"` | Rpc 里传 `NetworkConnection` | `Processors/NetworkBehaviourProcessor.cs:905` |
| `"{method.Name} cannot have optional parameters"` | 远程调用方法带可选参数 | `Processors/NetworkBehaviourProcessor.cs:914` |
| `"Abstract ClientRpc are currently not supported, use virtual method instead"` | 抽象 `[ClientRpc]` | `Processors/NetworkBehaviourProcessor.cs:971` |
| `"Abstract TargetRpc are currently not supported, use virtual method instead"` | 抽象 `[TargetRpc]` | `Processors/NetworkBehaviourProcessor.cs:1005` |
| `"Abstract Commands are currently not supported, use virtual method instead"` | 抽象 `[Command]` | `Processors/NetworkBehaviourProcessor.cs:1029` |
| `"{netBehaviourSubclass.Name} has invalid constructor"` / `"has invalid class constructor"` | NetworkBehaviour 构造函数不合法 | `Processors/NetworkBehaviourProcessor.cs:275,334,342` |
| `"Server or Client Attributes can't be added to abstract method. Server and Client Attributes are not inherited so they need to be applied to the override methods instead."` | `[Server]`/`[Client]` 加在抽象方法上 | `Processors/ServerClientAttributeProcessor.cs:35` |
| `"Cannot generate reader for {variableReference.Name}. Use a supported type or provide a custom reader"` | 类型没有 reader | `Readers.cs:145,151,157` |
| `"Cannot generate reader for generic variable {variableReference.Name}. Use a supported type or provide a custom reader"` | 泛型无 reader | `Readers.cs:163` |
| `"Cannot generate reader for interface {variableReference.Name}. Use a supported type or provide a custom reader"` | 接口无 reader | `Readers.cs:169` |
| `"Cannot generate reader for abstract class {variableReference.Name}. Use a supported type or provide a custom reader"` | 抽象类无 reader | `Readers.cs:175` |
| `"{variableReference.Name} is an unsupported type. Multidimensional arrays are not supported"` | 多维数组 | `Readers.cs:87` |
| `"{variable.Name} can't be deserialized because it has no default constructor. Don't use {variable.Name} in [SyncVar]s, Rpcs, Cmds, etc."` | 类型没有无参构造 | `Readers.cs:336` |
| `"Cannot generate writer for {variable}. Use a supported type or provide a custom writer"` | 类型没有 writer | `Writers.cs:302` |
| `"'[SyncVar] {type}.{field}' in '{module}' is modified by '{method}' in '{module}'. Modifying a [SyncVar] from another assembly is not supported. Please add a: 'public void Set{field}(value) { this.{field} = value; }' method in '{type}' and call this function from '{method}' instead."` | 跨程序集改 SyncVar | `Processors/SyncVarAttributeAccessReplacer.cs:102` |
| `"Failed to find Mirror AssemblyNameReference. Can't register Mirror.dll readers/writers."` | 找不到 Mirror 程序集引用 | `Processors/ReaderWriterProcessor.cs:87` |
| `"Failed to resolve {mirrorAssemblyReference}"` | 解析 Mirror 引用失败 | `Processors/ReaderWriterProcessor.cs:85` |
| `"Method not found with name {name} in type {tr.Name}"` / `"Field not found with name {name} in type {tr.Name}"` | 解析 Mirror 内部方法/字段失败 | `Resolvers.cs:24,56` |
| `"Exception :{e}"` | Weaver 内部未捕获异常 | `Editor/Weaver/Weaver.cs:261` |

**运行时（非 Weaver）相关报错**：

| 文案 | 位置 |
|---|---|
| `"No writer found for {typeof(T)}. This happens either if you are missing a NetworkWriter extension for your custom type, or if weaving failed. Try to reimport a script to weave again."` | `Core/NetworkWriter.cs:227` |
| `"Attempted to serialize unspawned GameObject: {value.name}. Prefabs and unspawned GameObjects would always be null on the other side. Please spawn it before using it in [SyncVar]s/Rpcs/Cmds/NetworkMessages etc."` | `Core/NetworkWriterExtensions.cs:267` |
| `"Attempted to serialize unspawned NetworkBehaviour: of type {value.GetType()} on GameObject {value.name}. ..."` | `Core/NetworkWriterExtensions.cs:290` |
| `"'{name}' has another NetworkIdentity component on '{identities[1].name}'. There should only be one NetworkIdentity, and it must be on the root object. Please remove the other one."` | `Core/NetworkIdentity.cs:418` |
| `"Scene {path} needs to be opened and resaved, because the scene object {identity.name} has no valid sceneId yet."` | `Editor/NetworkScenePostProcess.cs:85` |
| `"{name} has already spawned. Don't call Instantiate for NetworkIdentities that were in the scene since the beginning (aka scene objects). ..."` | `Core/NetworkIdentity.cs:378` |

---

## 附录 A：本次核对中"任务里提到但源码不存在"的清单

| 任务中提到的 | 结论 |
|---|---|
| `NetworkIdentity.visible` | 已改名 `visibility`（`Core/NetworkIdentity.cs:229`） |
| `NetworkIdentity.spawned` | 不存在；只有 `internal hasSpawned`（`Core/NetworkIdentity.cs:326`） |
| `NetworkBehaviour.componentIndex` | 不存在；实际为 `ComponentIndex`（`Core/NetworkBehaviour.cs:138`） |
| `NetworkClient.localConnection` | 不存在；只有 `NetworkServer.localConnection` |
| `NetworkConnection.playerController` | 已移除（仅注释残留） |
| `NetworkServer.SendToClient` | 不存在 |
| `NetworkServer.SendToObservers`（public） | 不存在（`static` private，`Core/NetworkServer.cs:714`） |
| `NetworkServer.GetSpawnedObjects` | 不存在；用 `NetworkServer.spawned` |
| `NetworkServer.RegisterMessageHandlers`（public） | 是 `internal`（`Core/NetworkServer.cs:331`） |
| `NetworkClient.OnTransportData`（公开事件） | 是 `internal` 方法（`Core/NetworkClient.cs:326`） |
| `[NetworkMessage]` 特性 | 不存在；`NetworkMessage` 是接口 |
| `[SyncObject]` 特性 | 不存在；`SyncObject` 是抽象基类 |
| `[InterestManagement]` 特性 | 不存在 |
| `[SceneInterestManagement]` 特性 | 不存在；是组件类 |
| `NetworkTransformBase.syncDirection` / `syncInterval` | 不在该类上，继承自 `NetworkBehaviour` |
| `NetworkRigidbody*2D` 的 public 字段 | 无，全部继承 |
| `NetworkAnimator.syncInterval` | 不在该类上，继承自 `NetworkBehaviour`（`Core/NetworkBehaviour.cs:54`） |
| `NetworkAnimator.onlySyncOnChange` | 不存在；该字段只在 `NetworkTransformBase` 上（`Components/NetworkTransform/NetworkTransformBase.cs:58`） |
| `SyncHashSet.cs` / `SyncSortedSet.cs` 独立文件 | 不存在；都在 `Core/SyncSet.cs` |
| Android 平台限制说明（Discovery） | **存在**：`NetworkDiscoveryBase.cs:267-291` 的 Android Multicast fix（`WifiManager.MulticastLock`）+ `Editor/AndroidManifestHelper.cs` |

## 附录 B：本次核对覆盖的文件清单

```
Assets/Mirror/Core/NetworkManager.cs
Assets/Mirror/Core/NetworkManagerHUD.cs
Assets/Mirror/Core/NetworkIdentity.cs
Assets/Mirror/Core/NetworkBehaviour.cs
Assets/Mirror/Core/Attributes.cs
Assets/Mirror/Core/NetworkServer.cs
Assets/Mirror/Core/NetworkClient.cs
Assets/Mirror/Core/NetworkConnection.cs
Assets/Mirror/Core/NetworkConnectionToClient.cs
Assets/Mirror/Core/NetworkConnectionToServer.cs
Assets/Mirror/Core/LocalConnectionToClient.cs
Assets/Mirror/Core/LocalConnectionToServer.cs
Assets/Mirror/Core/NetworkWriter.cs
Assets/Mirror/Core/NetworkWriterExtensions.cs
Assets/Mirror/Core/NetworkWriterPool.cs
Assets/Mirror/Core/NetworkWriterPooled.cs
Assets/Mirror/Core/NetworkReader.cs
Assets/Mirror/Core/NetworkReaderExtensions.cs
Assets/Mirror/Core/NetworkReaderPool.cs
Assets/Mirror/Core/NetworkMessages.cs
Assets/Mirror/Core/NetworkMessage.cs
Assets/Mirror/Core/Messages.cs
Assets/Mirror/Core/Tools/Utils.cs            (Channels)
Assets/Mirror/Core/NetworkStartPosition.cs
Assets/Mirror/Core/WeaverFuse.cs
Assets/Mirror/Core/SyncObject.cs
Assets/Mirror/Core/SyncList.cs
Assets/Mirror/Core/SyncDictionary.cs
Assets/Mirror/Core/SyncSet.cs
Assets/Mirror/Core/InterestManagement.cs
Assets/Mirror/Core/InterestManagementBase.cs
Assets/Mirror/Core/Transport.cs
Assets/Mirror/Components/NetworkTransform/NetworkTransformBase.cs
Assets/Mirror/Components/NetworkTransform/NetworkTransformReliable.cs
Assets/Mirror/Components/NetworkTransform/NetworkTransformUnreliable.cs
Assets/Mirror/Components/NetworkTransform/NetworkTransformHybrid.cs
Assets/Mirror/Components/NetworkAnimator.cs
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyReliable2D.cs
Assets/Mirror/Components/NetworkRigidbody/NetworkRigidbodyUnreliable2D.cs
Assets/Mirror/Components/InterestManagement/**（Distance/Scene/SceneDistance/SpatialHashing/Hex/Match/Team 全部 12 个实现）
Assets/Mirror/Transports/KCP/KcpTransport.cs
Assets/Mirror/Transports/Latency/LatencySimulation.cs
Assets/Mirror/Components/Discovery/NetworkDiscovery.cs
Assets/Mirror/Components/Discovery/NetworkDiscoveryBase.cs
Assets/Mirror/Components/Discovery/NetworkDiscoveryHUD.cs
Assets/Mirror/Editor/Weaver/**（EntryPointILPostProcessor / Processors / Weaver.cs / Readers.cs / Writers.cs / Resolvers.cs）
Assets/Mirror/Editor/NetworkScenePostProcess.cs
```
