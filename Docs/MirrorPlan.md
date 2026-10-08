# Mirror 联机改造计划（Survivor）

> **前置阅读**：`Docs/Mirror/README.md`（索引 + 12 条铁律）、`Docs/Mirror/03-项目落地注意.md`（本项目红线）。
> **详表**：`Docs/Mirror/04-影响面清单.md`（逐文件：现状 → 该在哪端跑 → 需要什么同步手段 → 风险）。
> **查 API**：`Docs/Mirror/02-API速查.md`（签名逐条来自本地源码，官方文档有多处已过时）。
>
> 本文是**唯一进度表**。每完成一项勾选，并写清「怎么验证的 / 结果」。
> 状态图例：☐ 未开始 · ◐ 进行中 · ☑ 完成 · ⚠️ 阻塞

---

## 0. 现状快照（开工前的事实）

### 0.1 已经就位、联机直接受益的部分

| 事实 | 依据 |
|---|---|
| 组合根 `GameBootstrap` 已在 `BeforeSceneLoad` 创建 `[GameBootstrap]`（DDOL）并注册全局服务 | `Assets/Script/Core/GameBootstrap.cs:61-70` |
| 三个 Manager 接口化，注释里已写明联机实现（`IPlayerManager` / `IGameLevelManager` / `IRunSessionService`） | `Core/IInterface/` |
| 玩家是**运行时生成**的（不是场景节点），入口唯一 | `Core/Level/PlayerSpawner.cs` |
| 「按玩家持有」的状态已经在 Player 实例上（经验/武器/升级/升级点/角色/血量） | `Docs/Mirror/04-影响面清单.md` §4.1 |
| `PlayerManager` 已是多人形状（`AllPlayers` + `LocalPlayer` + `LocalPlayerChanged`） | `Manager/PlayerManager.cs:11-18` |
| `StageDirector` / `PortalController` 的胜负判定已按 `AllPlayers` 写 | `Core/Stage/StageDirector.cs:223`、`Core/Level/PortalController.cs:222` |
| 战斗有**两个预留接缝**：`IAttackSpawner`（生成）与 `AttackAuthority`（计时权威） | `Core/Combat/IAttackSpawner.cs:17`、`Core/Combat/AttackAuthority.cs:23` |
| 输入工厂已预留 `network_X` 分支（当前返回 `NullInputHandle`，刻意不回落本地输入） | `InputSystem/InputHandleFactory.cs:58-65` |
| 相机已按 `LocalPlayerChanged` 事件重绑（不是固定引用） | `Core/Level/CinemachinePlayerFollow.cs:39` |
| 业务代码在 `Assembly-CSharp`，`Mirror.asmdef` 是 `autoReferenced: true` → **可直接 `using Mirror`，无需加引用** | `Assets/Mirror/Core/Mirror.asmdef` |

### 0.2 完全没有开始的

- `Assets/Script/` 下 **0 行 Mirror API 调用**（全项目大小写敏感 grep 命中 **7 处注释**提到 Mirror，逐条见 `Docs/Mirror/04-影响面清单.md` §2 导言）。
- 没有 `NetworkManager`、没有任何 `NetworkIdentity`、没有 `spawnPrefabs` 注册。
- **每个客户端都会各自跑完整玩法**：敌人生成、推车推进、胜负判定、结算发金币、传送门出发、大厅取随机种子。

### 0.3 三处「两套系统并存」——改造的核心难点

| # | 冲突 | 现状 | 处理方向 |
|---|---|---|---|
| 1 | **玩家生成权** | `PlayerSpawner`（运行时生成 + 未激活 prefab + 注入配置） vs Mirror 的 `playerPrefab` + `autoCreatePlayer` | **关掉 `autoCreatePlayer`**，生成权仍归 `PlayerSpawner`（改造成只跑服务端），生成后 `NetworkServer.AddPlayerForConnection` |
| 2 | **对象生命周期** | 静态对象池（`EnemyPool`/`ProjectilePool`/`ExpSpritePool`，靠 `SetActive(false)` 复用） vs Mirror 的 `Spawn`/`Destroy` | **联机路径关闭池化**；`IAttackSpawner` 就是为此预留的替换点 |
| 3 | **场景切换** | `SceneFlow.LoadSceneAsync`（每个客户端各自切） vs Mirror 的 `NetworkManager.ServerChangeScene` | `SceneFlow` 改为**按网络角色分派**，客户端不自己切 |

---

## 1. 目标形态与权威约定

**2-4 人在线合作 PvE（无 PvP）**，局域网直连优先，Android + Windows 双端。

### 1.1 权威矩阵（**改代码前先看这张表**）

| 系统 | 权威端 | 同步手段 | 现状文件 |
|---|---|---|---|
| 玩家**移动** | **客户端**（owner） | `NetworkTransform(syncDirection=ClientToServer)` 或 `NetworkRigidbody2D` | `Entity/Player/PlayerController.cs` |
| 玩家**输入 / 瞄准 / 切槽** | 客户端 | 无需同步（本地采样） | `PlayerController.Update` |
| 玩家**开火** | 客户端发起 → **服务端生成** | `[Command]` → `NetworkServer.Spawn` | `Core/Combat/AttackDriver.cs:423` |
| 玩家**伤害结算** | **服务端** | 投射物命中在服务端判定 → `SyncVar` 血量 | `Entity/Common/BaseHealthController.cs:111` |
| 玩家**血量** | 服务端 | `[SyncVar] CurrentHealth` + hook 广播 `HealthChanged` | 同上 |
| 玩家**等级 / 经验 / 升级点** | 服务端 | `[SyncVar]` + `[TargetRpc]` 表现 | `Core/Level/ExperienceLevController.cs` |
| **升级三选一选项** | 服务端抽签 | `[TargetRpc]` 下发 3 个选项；选择走 `[Command]` | `Util/UpgradeSelector.cs`、`Entity/Player/PlayerUpgradeController.cs` |
| 玩家**升级结果**（数值） | 服务端应用 → 广播 | `SyncList<UpgradeRecord>`（**待新建类型**），各端重放同一升级 | 应用逻辑在 `Core/SO/LeavelUp/LevelUpSO.cs` |
| 玩家**角色 / 装备** | 客户端选 → 服务端存 | `[Command]` 上报；生成玩家时服务端下发 | `Core/Services/RunSessionService.cs`、`UI/Lobby/*` |
| **敌人**（生成/AI/追击/接触伤害） | **服务端** | `Spawn` + `NetworkTransform`；客户端禁 AI 与物理 | `Core/Level/EnemySpawner.cs`、`Entity/Enemy/*` |
| **敌人血量 / 死亡** | 服务端 | `[SyncVar]` | `Entity/Enemy/EnemyHealthController.cs` |
| **推车**（位置/耐久/停摆） | **服务端** | `NetworkTransform` + `SyncVar` | `Entity/Cart/CartController.cs` |
| **塔**（放置/升级/血量/开火） | **服务端** | `Spawn` + `SyncVar` + `NetworkTransform` | `Entity/Tower/*` |
| **塔的归属** | 服务端 | 用 `connectionId`（**不能**用 `PlayerController` 引用） | `Entity/Tower/TowerLedger.cs:37` |
| **经验球** | 服务端（或改为「服务端发经验 + 本地特效」） | `Spawn` / `[TargetRpc]` | `Core/Level/ExperienceLevController.cs:239` |
| **关卡阶段 / 胜负 / 计时** | **服务端** | `[SyncVar]` + `[ClientRpc]` | `Core/Stage/StageDirector.cs`、`Manager/GameLevelManager.cs` |
| **击杀统计 / 结算 / 奖励** | **服务端** | `[TargetRpc]` 下发冻结的 `RunResult` | `Core/Stage/RunStatsTracker.cs`、`Core/Stage/RunSettlement.cs` |
| **UI / 音频 / 伤害数字 / 相机 / 暂停** | **纯本地**（每端各自） | 无 | `Core/Services/UIService.cs`、`UI/DamageNumService.cs` |
| **档案**（金币/解锁） | 本机存档保留 | 奖励由服务端下发后本地累加 | `Core/Services/PlayerProfileService.cs` |

### 1.2 决策点（**开工前必须拍板**，本文默认取"推荐"）

| # | 决策 | 推荐 | 备选与代价 |
|---|---|---|---|
| **D1** | 玩家移动权威 | **客户端权威**（手感优先） | 服务端权威（`[Command]` 发输入，反作弊强但要写预测/和解，工作量大得多） |
| **D2** | 玩家武器伤害判定 | **服务端权威**：开火走 `[Command]`，服务端生成投射物 + 服务端命中判定 | 客户端本地命中 + `[Command]` 上报（手感最好，但**队友看不到你的子弹**、且客户端打的是插值后的敌人位置，命中判定反而更差） |
| **D3** | 场景策略 | **Lobby 就是房间**：`offlineScene = Assets/Scenes/Lobby.unity`，`onlineScene` **留空**。玩家在 Lobby 里建房/加入（此时不切场景），Lobby→Level0 走 `ServerChangeScene`，离开房间由 `StopHost/StopClient` 自动回 offlineScene（= Lobby，语义正好是"回大厅"） | 把房间放在 Menu 并让 `onlineScene = Lobby`（Mirror 默认玩法）：代价是"建房时房间里没有人"，玩家还要再切一次场景才见到自己的角色 |
| **D4** | 玩家生成权 | **`autoCreatePlayer = false`**，`PlayerSpawner` 改造为只在服务端跑，自己 `LoadAssetAsync` → 注入 → 激活 → `AddPlayerForConnection` | 用 `playerPrefab` 自动生成（会打断"未激活 → 注入配置"的顺序，血量/移速会停在 1f 兜底值） |
| **D5** | 联机下的暂停 | **禁用 `Time.timeScale = 0`**，暂停面板改成纯 UI 覆盖层（屏蔽本地输入） | 只在「单人 Host」时允许真暂停 |
| **D6** | 对象池 | 联机路径**关闭池化**（敌人/投射物/经验球走 Mirror 生命周期）；纯本地表现（伤害数字）继续用池 | 自研「Mirror + 池」桥接（Mirror 不支持 unspawn 后复用，复杂度高，先不做） |
| **D7** | 经验球 | **服务端直接发经验 + 本地特效**（省掉一整条网络对象链路） | 经验球走 `Spawn` + 拾取判定（更有"抢经验"的玩法感，但同步面大） |
| **D8** | 敌人数量上限 | 联机下**下调** `EnemySpawner.maxEnemies`（当前 20，含 Boss） | 保持 20（带宽与帧率风险，见 §4） |

### 1.3 场景分工与常驻机制（**开工前定死，但不要重构场景**）

#### 场景分工

| 场景 | 角色 | 联网状态 | 说明 |
|---|---|---|---|
| `Menu` | 纯离线标题页 | 无网络 | 只有 UI；「开始游戏」= 普通 `LoadScene` 进 Lobby |
| `Lobby` | **房间 + 离线大厅** | 既是 `offlineScene`，也是联网后的常驻场景 | 玩家在**离线状态**下就能逛大厅；房间 UI（创建/加入/离开）在这里；玩家对象在这里生成 |
| `Level0` | 战斗关卡 | 由服务端 `ServerChangeScene` 驱动 | 关卡内容两端一致，随机源由服务端下发 |

**关键推论**：`onlineScene` 留空 ⇒ `IsServerOnlineSceneChangeNeeded()` 恒为 false（`Core/NetworkManager.cs:280-283`）
⇒ `StartHost()`/`StartServer()` 直接走 `FinishStartHost()`。
这条路径下：

- ✅ `NetworkServer.SpawnObjects()` **仍会被调用**（`NetworkManager.cs:563`）；
- ❌ **`OnServerSceneChanged` 不会被调用**（它只在真的切了场景的 `FinishLoadScene*` 里）——
  所以"生成玩家"的钩子**不能只挂 `OnServerSceneChanged`**，要用 **`OnServerReady(conn)`**
  （每次切场景 Mirror 会 `SetAllClientsNotReady`，客户端重发 Ready，这个钩子每次都会来一遍），
  幂等判据用 `conn.identity`（伪 null）。

#### 不需要 additive 场景

- Mirror 核心**只做 single-mode 切场景**（`ServerChangeScene` 内部就是 `LoadSceneAsync(name)`）；
  `SceneMessage` 的 `SceneOperation` 在日常流程里恒为 `Normal`。
  官方 additive 示例是自己 `LoadSceneAsync(..., LoadSceneMode.Additive)` 并自己管加载/卸载
  （`Assets/Mirror/Examples/AdditiveScenes/Scripts/AdditiveNetworkManager.cs:39-51`）——**等于自己兜一套场景生命周期**。
- 引入 additive 会额外带来：每个场景各自的 `sceneId` 分配与校验、`SpawnObjects()` 需要按 additive 场景再调一次
  （`Core/NetworkServer.cs:1635-1641` 的注释就是为这个场景写的）、卸载顺序、以及"这个 NetworkIdentity 属于哪个场景"的心智负担。
- 本项目 **3 个场景 / 2-4 人**，收益为零。**结论：不加。**

#### 不需要新建持久场景（继续用 DDOL 组合根）

- 项目已有 `GameBootstrap`（`RuntimeInitializeOnLoadMethod(BeforeSceneLoad)` + `DontDestroyOnLoad`），
  且**刻意不依赖任何场景/Inspector 接线**（`Core/GameBootstrap.cs:9-10` 的类注释）。
  再建一个持久场景 = 项目里出现**两套常驻机制**，规则要写两遍；Android 冷启动还多一次场景加载。
- 现有静态清理（`ResetStatics`）、服务注册/注销、失败可重试都已围绕 DDOL 组合根设计好，没有理由推倒。

#### ⚠️ 但 DDOL 上**不能**放 `NetworkBehaviour`（本次查源码发现的硬约束）

Mirror 的场景对象判据是 `sceneId != 0`（`Core/Tools/Utils.cs:90-103`）；
而 prefab 资产的 `sceneId` 被**强制置 0**、只分配 `assetId`（`Core/NetworkIdentity.cs:604-617`）。
所以由 prefab 实例化出来的 DDOL 对象 `sceneId == 0` ⇒ 被当成**动态生成对象**，
`NetworkServer.SpawnObjects()` 只筛场景对象，根本不会碰它（`Core/NetworkServer.cs:1642,1657`）⇒ 它永远不会被 spawn，
`netId` 恒为 0：

- `[Command]` / `[ClientRpc]` / `[SyncVar]` **全部不可用**；
- `isServer` / `isClient` 恒为 false（`Awake` 与之后都是）——**而且不报错**。

（除非你显式 `NetworkServer.Spawn(那个对象)` —— 但那等于把它当动态对象管，还不如一开始就放对地方。）

所以：

| 想做的事 | 正确形态 |
|---|---|
| 跨场景的会话表（谁选了什么角色/装备） | **纯 C# / MonoBehaviour 的服务端内存表**（不是 `NetworkBehaviour`） |
| 对外的网络接口（客户端上报选择） | 挂在**玩家对象**上的 `NetworkPlayerState : NetworkBehaviour`（动态 Spawn 的对象，一切正常） |
| 服务端主动发消息 | 静态的 `NetworkServer.SendToAll(msg)` / `conn.Send(msg)`（不需要 `NetworkIdentity`） |
| `NetworkManager` 本身 | **可以**放 DDOL —— 它不靠 spawn 工作，自己 `InitializeSingleton()` + `DontDestroyOnLoad`（`NetworkManager.cs:699-718`） |

配置怎么落：做一个 **`NetworkManager.prefab` 资产**（含 `KcpTransport`），
`offlineScene` / `spawnPrefabs` / `maxConnections` 都配在资产上；组合根在 `Boot()` 时 `Instantiate` 它并设 `dontDestroyOnLoad = true`。
**配置可视化 + 生命周期归组合根 + 不新增场景**，三者兼得。

---

## 2. 阶段总览

| 阶段 | 内容 | 依赖 | 规模 | 状态 |
|---|---|---|---|---|
| **P0** | 前置与基线（关编辑器、编译、Weaver、**技术 spike**） | — | 小 | ☐ |
| **P1** | 网络骨架：连得上、进得去场景、会话层 | P0 | 中 | ☐ |
| **P2** | 玩家网络化：生成/输入/移动/相机/血量 | P1 | 大 | ☐ |
| **P3** | 关卡与敌人权威：时间/波次/推车/敌人/胜负 | P2 | 大 | ☐ |
| **P4** | 战斗与建造：投射物/塔/经验/升级 | P3 | 大 | ☐ |
| **P5** | UI / 大厅 / 结算 / 档案 | P4 | 中 | ☐ |
| **P6** | 打磨：断线、Android、带宽、重开 | P5 | 中 | ☐ |

> **每个阶段独立可验证**：P1 之后是"能连上并看到空关卡"，P2 之后是"两个角色各自能动"，P3 之后是"怪两边一致"，P4 之后是"打得动、建得起"，P5 之后是"一局能完整跑完"。

---

## 3. 分阶段任务清单

### P0 — 前置与基线

**目标**：在写第一行网络代码之前，把环境和未知项钉死。

- [ ] **0.1 关闭 Unity 编辑器，跑一次批处理编译，记录基线**
  `Unity.exe -batchmode -nographics -quit -projectPath <proj> -logFile <log>`
  确认无 `error CS`、日志有 `Tundra build success`。
  ⚠️ 编辑器开着时批处理会因工程锁崩溃（`another Unity instance is running`）——这是环境问题不是代码问题。
- [ ] **0.2 确认 Weaver 生效**：Console 无 `Mirror Weaver` / `WeaverFuse` 报错；`Assets/Mirror/version.txt` 仍是 `96.11.3`。
- [ ] **0.3 贴 vendored Mirror 的本地补丁**：`NetworkConnection()` 构造函数里的 `Time.time` 在 Unity 6 domain reload 序列化期间会抛
  `UnityException: get_time is not allowed to be called during serialization` → `try/catch` 回退 0。
  **在文件里写明这是本地补丁**（升级 Mirror 后要重贴）。依据：`Docs/Mirror/03-项目落地注意.md` §15。
- [ ] **0.4 技术 spike（最高风险项，先做）**：写一个**一次性**的最小验证场景，确认下面这条链路能跑通
  （这是整个计划里唯一没有把握的部分，必须先用实验回答，不要靠推断）：
  1. `[GameBootstrap]` 上 `AddComponent<KcpTransport>()` → `AddComponent<NetworkManager>()`，`dontDestroyOnLoad = true`
     （**顺序不能反**：`NetworkManager.Awake` 会 `TryGetComponent<Transport>()` 自动接线）。
  2. **两条路径都要验**：
     - **A（D3 的主路径）**：当前场景已是 `offlineScene`（Lobby）⇒ `onlineScene` 留空 ⇒ `StartHost()` **不切场景**，
       直接走 `FinishStartHost()`。确认 `SpawnObjects()` 被调到、`OnServerSceneChanged` **没有**被调到、`OnServerReady` 被调到。
     - **B**：`ServerChangeScene("Level0")` 再切回来，确认切场景前后各回调的次序。
  3. 在 **`OnServerReady(conn)`** 里为每个连接生成一个带 `NetworkIdentity` 的对象并 `AddPlayerForConnection`
     （生成顺序：`Instantiate`（未激活）→ 注入 → `SetActive(true)` → `AddPlayerForConnection`）。
  4. **验证场景切换后 `conn.identity` 的状态**：Mirror 在卸载场景时销毁玩家对象，但**不会**把 `conn.identity` 置空
     （只有 `DestroyPlayerForConnection` / `RemovePlayerForConnection` 会，见 `Assets/Mirror/Core/NetworkServer.cs:1083,1405`）。
     据此决定切场景前是调 `DestroyPlayerForConnection(conn)` 还是直接重新 `AddPlayerForConnection`（销毁后的对象是伪 null，
     判空能过，但 `conn.owned` 里会留残项）。
  5. 用**纯远程客户端**（Editor 里第二个实例 + ParrelSync，或一个打包的 exe）各验一遍，不要只验 Host。
  **产出**：一段可复用的「跨场景重建玩家」代码 + 结论写回 `03-项目落地注意.md`。
- [ ] **0.5 决策 D1–D8 拍板**（§1.2），把结论写进本文件。
- [ ] **0.6 场景路径口径确认**：`offlineScene` 填**资产路径**（本项目为 `Assets/Scenes/Lobby.unity`），不要只填场景名 ——
  Mirror 内部混用两种口径：`SceneManager.LoadSceneAsync(name)`（路径与场景名都收）与
  `SceneManager.GetActiveScene().path != offlineScene`（**只认路径**，见 `Assets/Mirror/Core/NetworkManager.cs:608,1297`）。
  （`Utils.IsSceneActive`（`Core/Tools/Utils.cs:215-220`）是 path 或 name 都收的，不要拿它当"只认路径"的依据。）

**验证**：批处理编译通过；spike 的 5 步在 Host 与纯远程客户端上都能跑通。

---

### P1 — 网络骨架（连得上、进得去场景）

**目标**：Host / Join 能连上，服务端能驱动场景切换，会话数据能跨场景存活。**不含任何玩法同步**。

- [ ] **1.1 `NetworkManager` 挂到组合根**
  - 在 `GameBootstrap.Boot()` 之后创建（或新增 `NetworkBootstrap`），挂在 `[GameBootstrap]` 上；
    **不要**在每个场景各摆一个（重复实例会互相销毁，且服务切换时会断）。
  - 形态：**`NetworkManager.prefab` 资产**（含 `KcpTransport`），组合根 `Instantiate` 它 —— 配置可视化 + 生命周期归组合根（见 §1.3）。
  - `dontDestroyOnLoad = true`（`NetworkManager.cs:699-718`，会在 `Awake` 里 `SetParent(null)` + `DontDestroyOnLoad`；同物体上的 `KcpTransport` 靠 `TryGetComponent` 自动接线，见 `:729-734`）。
  - `maxConnections = 4`；`transport = KcpTransport`（端口可配，默认 7777）。
  - `autoCreatePlayer = false`（D4）；`playerPrefab = null`（玩家 prefab 走 Addressables，不走这个字段）。
  - `offlineScene = Assets/Scenes/Lobby.unity`；`onlineScene` **留空**（D3，见 §1.3）。
  - 不用 Mirror 自带的 `NetworkManagerHUD`（临时调试可以，别进正式流程）。
- [ ] **1.2 `NetworkManager.singleton` 加进 `GameBootstrap.ResetStatics()`**
  （`Core/GameBootstrap.cs:46-59`）—— 关掉 Domain Reload 后它是静态的，会跨 Play 存活。
- [ ] **1.3 场景策略落地（D3，见 §1.3）**
  - `offlineScene = Assets/Scenes/Lobby.unity`，`onlineScene` 留空（见 0.6 的路径口径）。
  - **`Menu` 保持纯离线**：`MenuPanel` 的「开始游戏」仍是普通的 `SceneFlow.LoadLobby()`（不联网、不建房）。
  - `Core/SceneFlow.cs` 改为**按网络角色分派**：
    - 服务端 → `NetworkManager.singleton.ServerChangeScene(name)`；
    - 客户端 → **什么都不做**（等服务端的 `SceneMessage`）；
    - 单机/离线（`mode == Offline`）→ 保持现在的 `SceneManager.LoadSceneAsync`。
  - **保留 `Time.timeScale = 1f` 的复位**（`SceneFlow.cs:39`，跨场景全局状态）。
- [ ] **1.4 房间 UI 放进 Lobby**（不是 Menu）
  - 新建 `UI/Lobby/NetworkRoomPanel.cs`：`创建房间（Host）` / `加入（输入 IP:Port）` / `离开房间` / `退出到标题`；
    局域网 IP 输入框 + 记住上次地址（`PlayerPrefs`）。
  - 由 `LobbyDirector` 在进入 Lobby 时显示（房间未开始时是常驻入口，不是模态遮挡）。
  - ⚠️ **建房时玩家已经在 Lobby 里了** —— 所以 `StartHost` 走的是"不切场景"的路径，
    玩家的生成由 `OnServerReady`/`OnStartServer` 触发（见 1.3 的推论与 P2.2），**不要**指望 `OnServerSceneChanged`。
  - 离开房间 = `StopHost()`/`StopClient()` → Mirror 自动回 `offlineScene`（Lobby），玩家对象被销毁、大厅回到离线状态。
- [ ] **1.5 会话层 `NetworkSessionService`（新建，跨场景的「每玩家选择」）**
  - **为什么必须新建**：玩家对象在切场景时会被销毁，它的 `SyncVar` 一起没了；
    而"谁选了什么角色/带什么武器"必须活过场景切换。`RunSessionService` 是**进程内单份**，Host 下服务端与客户端会共用一个实例，语义错误。
  - ⚠️ **形态：纯 C# / `MonoBehaviour` 的服务端内存表，不要做成 `NetworkBehaviour`** ——
    DDOL 上的 `NetworkBehaviour` 永远拿不到 `netId`，`[Command]`/`SyncVar` 全部静默失效（见 §1.3）。
    表内容：`connectionId → { characterId, loadoutIds[] }`，对外 `TryGet(connId, out data)`。
  - 选角/装备的写入路径：客户端在**玩家对象的** `NetworkPlayerState` 上 `[Command] CmdSetCharacter/CmdSetLoadout`
    → 服务端写进会话表 → 服务端在**生成玩家时**读表注入。
  - （可选，P5）让大厅显示队友选择：用玩家对象上的 `SyncVar` 暴露，而不是给会话表本身加同步。
- [ ] **1.6 断线处理**：`OnServerDisconnect` 清会话表 + 销毁该玩家占用的塔/召唤物；
  `OnClientDisconnect` 回到离线大厅（`offlineScene`）+ 给提示（不要留在一个"世界不动"的场景里）。
- [ ] **1.7 体检脚本**：`Assets/Editor/SceneWiringAudit.cs` / `EntitySOValidator.cs` 增加一条检查 ——
  「凡是会被 `NetworkServer.Spawn` 的 prefab：根节点有 `NetworkIdentity`、`assetId != 0`、且已在 `spawnPrefabs` 里」。

**验证**：在 Lobby 里 `创建房间` → **不切场景**、玩家立刻出现、Console 无红错；
一个纯远程客户端在 Lobby 里 `加入` 后能看到两个玩家；进出 Level0 一次；`离开房间` 能回到离线大厅。

**风险**：`onlineScene` 留空 ⇒ `OnServerSceneChanged` 不触发（已在 §1.3 写清替代钩子）；
跨场景重建玩家的时序仍由 0.4 spike 覆盖。

---

### P2 — 玩家网络化

**目标**：每个端只控制自己的角色，两端能看到彼此、相机各跟各的。

- [ ] **2.1 Player prefab 加 `NetworkIdentity`**
  - `Assets/Game/Prefabs/Common/Player.prefab` 的**根节点**（资产里 `m_IsActive: 0`）加 `NetworkIdentity`。
  - ⚠️ **不要**因为加网络而把 prefab 改成激活 —— 那会让 `Awake` 先跑，`SetEntityConfig` 之后被拒（`Core/EDM/EntityBehaviour.cs:61-67`），血量/移速停在 1f。
  - 批处理后**读一次 `ni.assetId` getter** 触发分配（`Docs/Mirror/03-项目落地注意.md` §4）。
- [ ] **2.2 `PlayerSpawner` 改造成网络生成器**
  - 只服务端跑（`if (!NetworkServer.active) return;`）。
  - 生成顺序**不可颠倒**：`Instantiate`（未激活）→ 注入 `playerConfig` / `PlayerRoleController` / 武器 → **自己** `SetActive(true)` → `NetworkServer.AddPlayerForConnection(conn, go)`。
    理由：`NetworkServer.Spawn` 内部会无条件 `SetActive(true)`（`Assets/Mirror/Core/NetworkServer.cs:1766`），依赖它就等于注入晚了。
  - 触发时机：**`OnServerReady(conn)`**（每次进入/切换场景后 Mirror 都会让客户端重发 Ready）
    + `OnStartServer`（兜住 Host 自己的本地连接）。
    ⚠️ **不要用 `OnServerSceneChanged`**：D3 下 `onlineScene` 留空，建房时不会切场景，这个回调根本不触发（见 §1.3）。
    也不要依赖场景里的 `Start` —— 它与 Mirror 的 `SpawnObjects` 谁先跑没有保证。
  - 幂等：判据用"该 `conn.identity` 是否已存在"（销毁后的对象是伪 null，判空能过），而不是场景里 `PlayerSpawner` 的 `HasPlayerAlready()`。
- [ ] **2.3 `PlayerController` 加本地守卫**
  - `Update` 的输入采样、`FixedUpdate` 的 `ApplyMove` 只在 `isLocalPlayer`（或 `isOwned`）时执行；
    **远程玩家的实例必须跳过物理写入**，否则本地物理会覆盖同步过来的位置。
  - `_inputHandleId` 是**序列化字段**、默认 `"local"` —— 必须在运行时按实例改写
    （如 `local` / `remote_<netId>`），否则 Host 下两个玩家共享同一个输入句柄（远程角色跟着本地输入动，且不报错）。
  - `OnEnable/OnDisable` 里的 `PlayerManager.Register/Unregister` 与 `EnemyTargetRegistry.Register/Unregister` 要区分端：
    客户端不该把远程玩家注册进"本地目标表"，服务端的 `EnemyTargetRegistry` 才是敌人 AI 的目标来源。
  - ⚠️ 注意 `isLocalPlayer` 在客户端是**晚于 `Awake`** 才为 true 的，不要在 `Awake` 里判。
- [ ] **2.4 `PlayerManager` 网络化**（`Manager/PlayerManager.cs`）
  - `LocalPlayer` 不能再是"第一个注册者"（`:39-40`）—— 改为按 `NetworkIdentity.isLocalPlayer` 判定。
  - `AllPlayers` 在客户端只含本地已知实例 → `PortalController.AllPlayersInside` 与 `StageDirector.CheckDefeat` 在客户端会失真。
    **这两个判定必须整体搬到服务端**（见 P3），客户端只消费结果。
  - 建议做成 `NetworkPlayerManager`（实现 `IPlayerManager`），由网络启动流程替换 `ServiceLocator` 里的注册
    （`ServiceLocator` 只清一次，换实现要**运行期**重新注册；`Clear()` 会连跨场景服务一起清，不要用它换）。
- [ ] **2.5 玩家的网络状态组件（新建 `NetworkPlayerState : NetworkBehaviour`）**
  挂在 Player 根节点上，承载"需要跨端一致且要活过这一局"的字段：
  `SyncVar<string> characterId`；`SyncList<string> loadoutIds`；
  `SyncVar<int> level` / `SyncVar<int> exp` / `SyncVar<int> upgradePoints`；
  `SyncList<UpgradeRecord> upgrades`（升级结果，各端重放；**`UpgradeRecord` 是待新建的类型，项目里目前没有**，
  需要能被 Mirror 自动序列化 —— 见 `Docs/Mirror/02-API速查.md` §8 的自定义类型规则）；
  `SyncVar<Vector2> aimDirection`（远程玩家的朝向表现）。
  ⚠️ **不要**为了同步把 `EntityStatModel` 整个序列化 —— 用"广播决策、各端重放同一升级"的方式（见 4.6）。
- [ ] **2.6 血量网络化**：`BaseHealthController` 改为 `NetworkBehaviour`
  - ⚠️ 它一改，**所有子类所在的 prefab 根节点都必须有 `NetworkIdentity`**：
    Player、6 份敌人、3 座塔、Cart（`Docs/Mirror/03-项目落地注意.md` §15.4）。
  - `CurrentHealth` 改成 `[SyncVar]` 支撑的属性 + hook 里调 `RaiseHealthChanged()`，让本地 UI 照旧工作。
  - `TakeDamage` / `Heal` 加 `if (!isServer) return;`（客户端只表现）。
  - ⚠️ `Heal()` 里直接调了 `DamageNumService.SpawnDamageNum`（`Entity/Common/BaseHealthController.cs:152`）——
    服务端进程里没有画布，这条会静默空跑。伤害数字改走"服务端结算 + `[ClientRpc]` 表现"或干脆本地自行生成。
- [ ] **2.7 移动同步**：给 Player prefab 加 `NetworkTransform`（`syncDirection = ClientToServer`）或 `NetworkRigidbody2D`
  - 2D 项目注意：`NetworkTransform` 同步的是 `Transform` 而不是 `Rigidbody2D`；
    要物理一致就用 `NetworkRigidbodyReliable2D`（它会在无权威方把刚体设成 Kinematic）。
  - ⚠️ `AddComponent<NetworkTransform>()` 会触发 `Reset()` NRE（噪声，组件仍会加上），随后显式设 `target` / `syncDirection` / `syncInterval`。
- [ ] **2.8 相机绑本地玩家**：确认 `CinemachinePlayerFollow` 绑的是 `isLocalPlayer` 的那个实例
  （Host 下同时存在本地与远程玩家，取"第一个"必然出错）。
- [ ] **2.9 移除场景里的旧玩家节点**：`Level0` 已无 Player 节点（已核实），`Lobby` 同样由 `PlayerSpawner` 生成 —— 确认没有第二条生成路径。

**验证**：Host + 一个纯远程客户端，各自控制自己的角色；双方看到彼此移动；相机各跟各的；
Console 无红错；远程玩家的移动不会被本地物理覆盖。

---

### P3 — 关卡与敌人权威

**目标**：敌人/推车/时间/胜负只在服务端跑，客户端只表现。

- [ ] **3.1 `EnemySpawner` 只服务端跑**
  - `Update` 首行加 `if (!NetworkServer.active) return;`。
  - 随机源已经由 `StageDirector` 从 `RunSession.seed` 注入（`Core/Level/EnemySpawner.cs:97-98`）—— 种子改为**服务端下发**（见 3.5）。
- [ ] **3.2 敌人 prefab 加 `NetworkIdentity` + 注册 `spawnPrefabs`**
  - 6 份：`Enemy_Cloud / Enemy_Slime / Enemy_Soil / Snake / Spoil / Wolf`（`Assets/Game/Prefabs/EnemyPrefab/`，**含在 `spawnPrefabs` 里**）。
  - `spawnPrefabs` 是**强引用**，会让它们进构建并常驻 —— 这是 Mirror 的硬性要求（assetId 表两端必须一致），不要改成 Addressables 运行时注册。
- [ ] **3.3 敌人生成改走 `NetworkServer.Spawn`，**关闭池化**
  - `EnemyPool.Spawn` 的两条路径（`Core/Level/EnemySpawner.cs:353` 池化 / `:370` Boss 非池化）在联机下统一为
    `Instantiate` → 注入 `EntitySO` → `NetworkServer.Spawn`。
  - ⚠️ Boss 的注入必须早于 `Spawn`（`Spawn` 会 `SetActive(true)`）；且 `EnemyController.SetEntityConfig` 在 StatModel 建好后拒绝注入。
  - `GameBootstrap.ResetStatics()` 里的 `EnemyPool.Reset()` **保留**（单机路径与编辑器仍要）。
- [ ] **3.4 客户端关闭敌人的 AI 与物理**
  - `EnemyController.FixedUpdate`（寻敌/移动）、`EnemyTargetFinder`、`EnemyHealthController` 的接触周期伤害
    都加 `if (!isServer) return;`；客户端的位置完全由 `NetworkTransform` 驱动。
  - `EnemyBoundary`（边界回收）也要只在服务端跑（否则客户端会把"服务端还在的怪"回收掉）。
- [ ] **3.5 `GameLevelManager` 网络化**（`Manager/GameLevelManager.cs`）
  - `_levelTime` / `_currentWave` 改 `[SyncVar]`，只在服务端累加；客户端由 hook 刷 UI。
  - `enemies` 列表（`RegisterEnemy`/`GetEnemyCount`）只在服务端有意义 → 客户端不注册。
  - ⚠️ **暂停**：`ApplyPauseState` 直接写 `Time.timeScale`（`:173`）。按 D5 禁用真暂停。
  - `RunSession.seed` 改由服务端 `BeginNew` 时生成并下发（现在是 `Environment.TickCount`，各端不同 → 敌人分布不同）。
- [ ] **3.6 `CartController` 服务端模拟**
  - `Update` 里的弧长推进只在服务端；`transform` 由 `NetworkTransform` 同步；
    `IsMoving` / `IsDisabled` / `TravelledDistance` / 耐久改 `[SyncVar]`。
  - `ReachedNode` / `DisabledChanged` 这两个事件只在服务端有意义（订阅者 `StageDirector` 也在服务端）。
- [ ] **3.7 `StageDirector` 服务端权威**（`Core/Stage/StageDirector.cs`）
  - `Phase` / `_finished` 服务端；`PhaseChanged` / `RunFinished` 通过 `[ClientRpc]` 广播。
  - `CheckDefeat` 用服务端的 `AllPlayers`（全端一致），客户端不判胜负。
  - `PortalController.Depart`（`Core/Level/PortalController.cs:251`）现在每个客户端都会各自 `SceneFlow.LoadStage`
    → 改为 `[Command]` 上报"我准备好了"，服务端判定全员后 `ServerChangeScene`。
- [ ] **3.8 `RunStatsTracker` 服务端**（`Core/Stage/RunStatsTracker.cs`）
  - 击杀数只在服务端累计；注意它注册的是**具体类型**（`RunStatsTracker.cs:23,32`），不是接口 —— 替换实现时这条访问路径也要改。
- [ ] **3.9 `EnemyTargetRegistry` 服务端**（`Core/Level/EnemyTargetRegistry.cs`）
  - 敌人的目标表（玩家/塔/推车）只在服务端有意义；客户端注册只会污染本地表。
- [ ] **3.10 客户端"晚生成"问题逐个确认**
  - `ExpSpriteController.OnGetFromPool` 缓存 `LocalPlayer`（`Core/Level/ExpSpriteController.cs:43-49`）；
    `PlayerHudBinder.BindRoutine` 的 `while (local == null)` 一旦被错误玩家占位就不再重试（`UI/PlayerHudBinder.cs:60-65`）。
  - 统一改成"订阅 `LocalPlayerChanged` + 判 `isLocalPlayer`"，不要靠"第一个注册的"。

**验证**：Host 与纯远程客户端看到的敌人数量/位置/死亡一致；推车位置与停摆两端一致；
两端计时一致；全灭后两端都进结算；中途加入的客户端能看到正确的敌我状态。

---

### P4 — 战斗与建造同步

**目标**：打得动、建得起，伤害只在权威端结算一次。

- [ ] **4.1 网络生成实现 `NetworkAttackSpawner`（实现 `IAttackSpawner`）**
  - `IAttackSpawner`（`Core/Combat/IAttackSpawner.cs:17`）是**全项目最干净的接缝**：`AttackMethodSO` 与资产完全不用改。
  - ⚠️ 唯一障碍：接口入参是 `GameObject prefab`（跨端不是稳定标识）。
    实现里要按 **prefab → `NetworkIdentity.assetId`** 解析（服务端与客户端各自的 prefab 引用不同）。
  - `Spawn` → `NetworkServer.Spawn`；`Despawn` → `NetworkServer.Destroy`（取代 `ProjectilePool.Return`）。
- [ ] **4.2 `AttackDriver` 写入权威**（`Core/Combat/AttackDriver.cs`）
  - 按 `AttackAuthority` 的注释约定：**网络层在实体 spawn 之后读一次 `Authority`，据此设置 `HasAuthority`**（唯一写入点）。
  - 塔武器（`AttackAuthority.Server`）→ 客户端 `HasAuthority = false`；玩家武器（`Local`）→ 按 D2 决策处理
    （若选服务端权威，则玩家武器的 tick 也要在服务端跑，客户端的 `HasAuthority = false`）。
  - ⚠️ 写入时机必须早于第一个 `Update`，否则会有一帧双端各打一次。
  - ⚠️ `AttackDriver.SetAttack` 在 `_started` 之后拒绝注入（`:152-161`）—— 网络生成的对象要保证注入早于 `Start`。
- [ ] **4.3 投射物 prefab 加 `NetworkIdentity` + 注册**
  `Weapon/Bullet.prefab`、`Weapon/Bullet_Shell.prefab`、`Common/TetoBullet.prefab`、`Common/TetoShell.prefab`
  （+ 环绕物 `Weapon/Spin.prefab`，若服务端生成）。
  - ⚠️ `AssetKeys` 里没有 Bullet/Spin 常量，但**这几个 Addressables 条目不能删**（`AttackDriver._prefab` 用 GUID 引用，同样要求目标在组里）。
- [ ] **4.4 玩家开火走 `[Command]`**（D2）
  - `PlayerController`/武器输入 → `[Command] CmdFire(origin, direction, weaponSlot)` → 服务端生成投射物 + 命中判定 + `SyncVar` 扣血。
  - 客户端本地只做**表现**：枪口火焰、音效、后坐力（`AttackDriver.OnPerformed` 的订阅者留在开火端）。
  - 若实测手感不可接受，再退回"本地命中 + `[Command]` 上报"，并在本文件标注代价。
- [ ] **4.5 塔网络化**
  - 3 份塔 prefab 加 `NetworkIdentity` + 注册 `spawnPrefabs`；`TowerPlacementController.ConfirmPlacement`（`:409`）改为 `[Command]` 到服务端生成。
  - 放置幽灵（`Common/SpriteToHandle.prefab`）**保持纯本地**，不要网络化。
  - `TowerLedger.Owner` 现在是 `PlayerController` 本地引用（`Entity/Tower/TowerLedger.cs:37`），
    而 `TowerLevelUpPanel` 用它做归属校验（`UI/GamePanel/TowerLevelUpPanel.cs:258`）→ **必须改成 `connectionId`**。
  - 塔升级 / 治疗（`HealAlliesAttackSO` 的 `ctx.Host` 比较是本地实例比较）都要在服务端结算。
- [ ] **4.6 升级三选一**
  - 服务端抽签（`Util/UpgradeSelector.cs` 的静态 `_rng` 是**所有玩家共用**的；注释已给出正解："权威端抽完广播"或"各端同一个种子"）。
  - 服务端 → `[TargetRpc]` 下发 3 个选项 → 客户端选 → `[Command] CmdPickUpgrade(index)` → 服务端应用到自己的 StatModel
    → 通过 `SyncList<UpgradeRecord>`（新建类型）广播 → 各端（含客户端自己的那份）**重放同一个升级**。
  - ⚠️ `PlayerUpgradeController` 的选项缓存已经按玩家持有（`Entity/Player/PlayerUpgradeController.cs:32`），不要挪回全局。
- [ ] **4.7 经验球（D7）**
  - 推荐：服务端算击杀归属 → `[TargetRpc]` 给击杀者加经验 + `[ClientRpc]` 播本地特效（**完全绕开** `ExpSpritePool` 与网络生命周期的双重管理）。
  - 若保留经验球实体：加 `NetworkIdentity` + 服务端 `Spawn` + 服务端拾取判定，并关闭 `ExpSpritePool`。
- [ ] **4.8 玩家经验/等级同步**：`ExperienceLevController` 的 `currentLevel/currentExp` 由服务端驱动，
  客户端本地照旧订阅 `OnExpChanged` / `OnLevelUp` 刷 UI 与播特效。
- [ ] **4.9 Addressables 句柄与 Mirror 销毁的顺序**
  - `NetworkServer.Destroy` **不会**递减 Addressables 引用计数。
  - 武器走的是"手动 `Instantiate` + 每持有者一份句柄"（刻意避开 `InstantiateAsync`，见 `Entity/Player/PlayerWeaponController.cs:195-200`）—— 联机后这套约定仍成立，但要明确 `NetworkServer.Destroy` 与 `ReleaseHandle` 的先后。
  - `ProjectilePool.RetainsInstancesOf` / `DropPool` 那段（`Core/Combat/AttackDriver.cs:338-339`）在联机路径下不再需要。

**验证**：Client 攻击敌人 → 敌人血量两端一致、死亡只结算一次；塔放置/升级两端一致；
升级三选一两端选项一致、数值同步；经验只给击杀者且两端等级一致。

---

### P5 — UI / 大厅 / 结算 / 档案

**目标**：一局能完整跑完（菜单 → 联机 → 选角 → 关卡的 → 塔 → 升级 → 死亡/胜利 → 结算 → 回菜单）。

- [ ] **5.1 选角与装备走网络**（`UI/Lobby/CharacterSelectPanel.cs`、`TowerBenchPanel.cs`、`WeaponBenchPanel.cs`）
  - 客户端本地写入 `RunSessionService`（用于本端 UI）**同时** `[Command]` 上报到 `NetworkSessionService`。
  - ⚠️ 角色**数值**仍按现有约定"等下次生成玩家才生效"（`Core/Level/PlayerSpawner.cs:317-321`）—— 大厅切角色只改能力位。
- [ ] **5.2 HUD 只显示本地玩家**：`UI/PlayerHudBinder.cs` 绑定本地玩家（注释里已经这么设计，联机下要确认 `isLocalPlayer` 判定到位）。
- [ ] **5.3 面板只作用于 `LocalPlayer`**：升级面板、选塔面板、武器升级面板、暂停面板。
- [ ] **5.4 暂停面板按 D5 改造**：不再写 `Time.timeScale`。
- [ ] **5.5 结算与奖励**：`Core/Stage/RunSettlement.cs` 只在服务端跑；
  `RunResult` 通过 `[TargetRpc]` 下发到每个客户端 → 各自弹 `RunResultPanel` + 写入本机档案。
  ⚠️ `PlayerProfileService` 是**每台机器一份**（`Core/Services/PlayerProfileService.cs:39`），
  结算奖励由服务端下发数值、客户端本地累加（不要试图同步整个档案）。
  ⚠️ `OutpostDifficulty` 读本机档案决定敌人强度（`Core/Stage/OutpostDifficulty.cs:42`）→ 联机下必须由**服务端**决定并随生成表下发。
- [ ] **5.6 伤害数字纯本地化**：确认没有任何 `DamageNumService` 调用会落在服务端路径上（见 2.6）。
- [ ] **5.7 `InteractionPromptView` / `MobileInputHandle.TouchHeld`**：`TouchHeld` 是**全局单份**且不在重置清单里
  （`InputSystem/MobileInputHandle.cs:97`）→ 多玩家/多交互物下"谁按着"无法区分；Android 上要么一人一设备，要么改成按玩家分配摇杆。

**验证**：§5 的完整流程在 Host + 纯远程客户端上各跑一遍，Console 无红错。

---

### P6 — 打磨

- [ ] **6.1 断线 / 中途加入**：`OnServerDisconnect` / `OnClientDisconnect` / `OnServerConnect`（中途加入要能拿到完整状态）。
- [ ] **6.2 延迟与插值**：`NetworkTransform` 的 `syncInterval`、`LatencySimulation` 模拟 100-200ms 验证手感；
  需要时上 `DistanceInterestManagement` / `SpatialHashingInterestManagement`（2D 项目注意 `SpatialHashingInterestManagement` 的 `CheckMethod` 默认是 3D 的 `XZ`，**必须改成 2D 的平面**）。
- [ ] **6.3 带宽**：用 `NetworkStatistics` 看敌人 20 只 + 子弹时的实际占用；按 D8 决定要不要下调 `maxEnemies`。
- [ ] **6.4 重开局清理**：回菜单 → 再开一局时，`ServiceLocator`、`ManagerSingleton._instance`、静态池、`NetworkManager.singleton` 的状态必须干净
  （`GameBootstrap.ResetStatics` **不会**在重开时触发，它只在进 Play 前跑）。
- [ ] **6.5 Android 联机**：局域网 IP 输入、`NetworkDiscovery`（可选）、移动端按键布局与摇杆分配。
- [ ] **6.6 反作弊（按需）**：D1 客户端权威移动 / D2 服务端权威伤害之后，剩下的作弊面主要是"客户端改本地数值"。
  按需加：射速下限校验、伤害上限校验、位置突变校验。**不要**在没有实际需求时提前做。

---

## 4. 风险与回退

| 风险 | 影响 | 缓解 / 回退 |
|---|---|---|
| 跨场景重建玩家的 API 组合与预期不符 | P1 卡住 | **P0.4 spike 先做**；回退方案：把 Lobby 与 Level0 合并成一个场景（少一次 `ServerChangeScene`） |
| `NetworkServer.Spawn` 强制激活打断"注入后激活" | 玩家/敌人血量与移速停在 1f | 严格按 2.2 的顺序；用体检脚本拦"prefab 被改成激活" |
| 关闭对象池后敌人/子弹数量带来的性能与 GC 压力 | 帧率下降、卡顿 | 先按 D8 下调上限；必要时再考虑自研「Mirror + 池」桥接 |
| `BaseHealthController` 改成 `NetworkBehaviour` 牵动 11 份 prefab | 漏一个就 `OnValidate` 报错 | 一次性批量处理（Player 1 + 敌人 6 + 塔 3 + Cart 1），用批处理脚本 + 体检 |
| Host 下静态状态共享导致"服务端逻辑"与"客户端逻辑"互相干扰 | 难排查的双重结算 | 所有跨端逻辑显式判 `isServer` / `isClient`；不要靠"客户端不会跑这段" |
| Addressables 与 `spawnPrefabs` 的强引用冲突 | 与"资源全走 Addressables"的约定有张力 | 接受（Mirror 的硬性要求）；只对**玩家 prefab** 保留 Addressables 动态加载路径 |
| 联机下的暂停语义 | 玩家体验错乱 | 按 D5 禁用真暂停，最省事 |
| 编辑器 Domain Reload 关闭 + Mirror 静态单例 | 跨 Play 状态残留 | `ResetStatics` 里一并清 `NetworkManager.singleton` |

---

## 5. 验证矩阵（每阶段收敛后必跑）

> **必须包含一个纯远程客户端**（Editor + 打包 exe，或 ParrelSync），只测 Host 会漏掉大量问题。

| 场景 | Host | 纯 Client |
|---|---|---|
| 启动 / 连接 / 进 Lobby | ☐ | ☐ |
| 进 Level0 / 场景同步 | ☐ | ☐ |
| 各自控制角色 / 互相可见 / 相机 | ☐ | ☐ |
| 敌人生成 / 追击 / 死亡两端一致 | ☐ | ☐ |
| 开火 / 子弹可见 / 命中只结算一次 | ☐ | ☐ |
| 塔放置 / 升级 / 治疗 | ☐ | ☐ |
| 经验归属 / 升级三选一 | ☐ | ☐ |
| 推车耐久 / 停摆 / 恢复 | ☐ | ☐ |
| 死亡（单人死 / 全灭）→ 结算 → 回菜单 | ☐ | ☐ |
| 中途加入 | — | ☐ |
| 拔线 / 断线提示 / 回到菜单 | ☐ | ☐ |
| Console 无红错、无 Weaver 报错 | ☐ | ☐ |
| 重开一局（回菜单再出发）状态干净 | ☐ | ☐ |

---

## 6. 手动步骤（需要人做的）

| # | 事项 | 谁 |
|---|---|---|
| M-1 | 关掉 Unity 编辑器再跑批处理（CLI 会因工程锁崩溃） | 用户 |
| M-2 | 第二个客户端实例：ParrelSync 或打包一个 exe | 用户 |
| M-3 | 每阶段的 Play 验证（批处理不能进 Play） | 用户 |
| M-4 | Android 真机联机验证 | 用户 |
