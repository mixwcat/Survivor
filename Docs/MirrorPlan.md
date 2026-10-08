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
  （`Assets/Mirror/Examples/AdditiveScenes/Scripts/AdditiveNetworkManager.cs:39-51`；⚠️ 该示例目录**不在仓库里**，
  已加进 `.gitignore`，需要时从 Mirror 官方仓库补回）——**等于自己兜一套场景生命周期**。
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
- [ ] **1.2 `NetworkManager.singleton` 的跨会话清理** —— **不需要我们做**：Mirror 自己有
  `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)] ResetStatics()`（`Core/NetworkManager.cs:777-793`），
  而且 `singleton` 是 `{ get; internal set; }`，外部也写不了。
  （更正：本项目 **域重载是开启的**，所以原计划里"关掉 Domain Reload 后静态跨 Play 存活"的前提不成立。）
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

---

## 7. 进度日志（倒序，最新在上）

### 2026-10-08 · P2.5 装备同步（☑ 双进程已验证）

**之前的状态**：联机下**所有人都是空手的**。武器是**装配**上去的，而装配逻辑
（`PlayerSpawner.EquipLoadoutAsync`）只跑在**服务端那份副本**上 ——
客户端的每个玩家副本都不会自己装。后果比"看不到队友的武器"更严重：
**客户端自己的角色也没有武器，所以根本开不了火**。

**做法**

- `NetworkPlayerState` 加 `[SyncVar] string _loadoutCsv`（逗号分隔的武器 id）。
  用字符串而不是 `SyncList<string>`：列表很短、只在生成时写一次，
  而 `SyncList` 要处理初始化时序与增量同步两套语义。
- `PlayerSpawner.EquipLoadoutAsync(player, conn)` 拆成两半：
  解析来源（会话表 / RunSession）与**按列表装配**。
  后者改成 `public`，**两端共用** —— 客户端靠它给每个副本装配。
- `ServerSetLoadout` 在 **spawn 之前**调用（进初始载荷），
  理由与角色 id 相同：客户端的 `Start` 早于"生成后的变更同步"到达，
  武器会在"已经 Start 过"之后才出现。
- hook 里 `if (isServer) return;`（服务端那份已经装过了），客户端按列表装配。

**为什么不会"两端各打一次"**：客户端给远程副本装上的武器**不会 tick** ——
`GunWeapon.Start` 里的 `LocalPlayerGuard` 会挡掉（远程副本 `isLocalPlayer == false`），
服务端那份同理。所以只有**拥有者那一端**的武器真的开火，
配合 P4 的伤害路由（客户端命中 → 上报）正好闭环。

#### 验证（双进程，两端 `SMOKE_OK`）

服务端在**建房前**给自己的 RunSession 配了 `weapon_gun`（测试用的稳定武器 id），
客户端断言**远端副本**上装出了武器：

```
server: 服务端已装备 weapon_gun
client: 满足：远端玩家副本上装出了武器
client: 装备同步已确认：服务端的装备列表同步到客户端，远端副本装配成功
```

### 2026-10-08 · P4.7 + P4.8 经验与等级同步（☑ 双进程已验证）

**之前的状态**：客户端**永远停在 1 级**。经验球（`ExpSpritePool`）是**纯本地实例** ——
服务端生成的球客户端看不到、也捡不到，所以只有主机能升级、能弹升级三选一。

**做法（按 D7 的推荐分支：绕开经验球实体）**

- `EnemyHealthController.Die()`：联机时**直接把经验给击杀者**
  （`ResolveKiller(LastDamage.Attacker)` → 击杀者的 `ExperienceLevController.AddExperience`）；
  单机仍走经验球，**单机行为一字未改**。
- `ExperienceLevController.AddExperience`：联机时客户端调用是**空操作** ——
  客户端也加的话两端会各自演化出一套等级，而"升级三选一"是按本地等级弹的，
  最后变成两边选项数量都不一样。
- `NetworkPlayerState` 加 `[SyncVar] _syncedLevel` / `_syncedExp`，
  服务端**订阅 `OnExpChanged` / `OnLevelUp`**（与血量同一套理由：加经验的路径不止一条，
  漏一条就是"某种来源的经验客户端看不见"）。
- `ExperienceLevController.ApplyNetworkProgress(level, exp)`：客户端应用。
  **必须照常发 `OnExpChanged` / `OnLevelUp`** —— HUD 经验条与升级面板都是事件驱动的订阅者，
  只改字段不发事件的表现是"等级涨了但面板不弹、经验条不动"。

⚠️ **这是一次有取舍的设计决定，值得你过一眼**：
联机下**丢掉了"走过去捡经验球"的手感** —— 经验变成击杀即得。
要找回来就得把经验球做成网络对象（服务端 Spawn + 服务端拾取判定），
而峰值 20 次/秒的生成量让那条路的带宽与对象管理代价明显更高。
如果你更看重手感，告诉我，我按"网络化经验球"那条路重做。

#### 验证（双进程，两端 `SMOKE_OK`）

```
client: 对 netId=5 的敌人打一发致命伤害 → 敌人被服务端结算并销毁
client: 满足：击杀经验同步到客户端（说明击杀归属正确）
client: 经验同步已确认：等级=1，经验=1
```

这条断言同时证明了三件事：**击杀归属正确**（经验记在了上报者的玩家身上，不是主机）、
**经验只结算一次**（服务端）、**同步链路通**。

### 2026-10-08 · ⚠️ 修掉上一轮引入的**无限递归** + 玩家血量同步（P2.6 的玩家部分）

#### 先修 bug：`base.TakeDamage` 在 `ApplyDamage` 重写里会栈溢出

上一轮把 `TakeDamage` 拆成"非虚入口 + `ApplyDamage` 虚方法"时，
子类重写里的 `base.TakeDamage(in info)` **没有跟着改名**：

```csharp
protected override void ApplyDamage(in DamageInfo info)
{
    base.TakeDamage(in info);   // ← 入口 → 路由 → ApplyDamage → 这里 → 无限递归 ⇒ 栈溢出
}
```

中招的是 `PlayerHealthController` 与 `TowerHealthController` 两处。
**为什么上一轮的测试没抓到**：那条伤害往返打的是**敌人**，
而 `EnemyHealthController` 是内联实现扣血的，不调 `base.TakeDamage` ——
敌人这条路径恰好绕开了雷。**敌人一碰到玩家或塔，服务端就会崩**。

修完在双进程测试里加了守门断言（服务端给自己扣 1 点），它同时覆盖这两件事。

**教训**：把"入口方法"和"可重写实现"拆开时，**子类里的 `base.Xxx` 必须逐个检查** ——
编译器不会提醒，因为两种写法都是合法的。

#### 玩家血量同步

在此之前客户端副本的血量**永远不动**：敌人接触伤害从 P3 起就只在服务端结算，
而客户端没有任何通道知道自己的血掉了 —— **HUD 血条一直满、角色永远不会死**，完全静默。

- `NetworkPlayerState` 加 `[SyncVar] float _syncedHealth`（`-1` 是"还没写过"的哨兵）。
  服务端**订阅 `HealthChanged`** 而不是在每个扣血点写 SyncVar ——
  扣血路径有好几条（接触伤害、投射物、将来的毒圈），漏一条就是"某种伤害客户端看不见"。
- `BaseHealthController.ApplyNetworkHealth`：客户端应用，只改数据 + 发事件，
  **不走 `TakeDamage`/`Heal`**（那些是结算，客户端跑就等于两端各结算一次）。
- `BaseHealthController.IsHealthSynced`（虚，默认 false，玩家 override 为 true）+
  `IsClientHealthReplica`：**`Start` 不再无条件写 `CurrentHealth = MaxHealth`**。
  这是个时序陷阱 —— SyncVar 的 hook 在 `Awake` 之后、`Start` **之前**跑，
  `Start` 再写一次就把刚同步下来的值抹掉了（而且完全静默）。
  只有声明了 `IsHealthSynced` 的实体受影响，敌人/塔/推车的行为不变。

#### 验证（双进程，两端 `SMOKE_OK`）

```
server: 服务端给自己扣 1 点：100.0 → 99.0（玩家路径无递归）
client: 满足：队友的血量同步到客户端（服务端扣了 1 点）
client: 玩家血量同步已确认：服务端扣血 → SyncVar → 客户端副本血量下降
```

### 2026-10-08 · P4（第一段）伤害路由：客户端命中 → 服务端结算（☑ 双进程已验证）

**之前的状态**：客户端能开火、子弹能飞、能"打中"，但**打不掉血** ——
`EnemyHealthController` 从 P3 起就有权威守卫，而客户端产生的命中没有任何上报通道。

**做法：把路由挂在伤害的"唯一入口"上，六个调用点一行都不用改。**

`BaseHealthController.TakeDamage` 拆成两段：

```csharp
public void TakeDamage(in DamageInfo info)          // 非虚 —— 唯一入口，负责路由
{
    if (DamageRouter.ShouldForwardToServer(gameObject)) { DamageRouter.ForwardToServer(gameObject, info); return; }
    ApplyDamage(info);
}
protected virtual void ApplyDamage(in DamageInfo info)   // 子类 override 这个
```

- **为什么非虚**：子类（敌人/玩家/塔）原本 override 的是 `TakeDamage`，
  把路由放在那里就等于每加一种实体都要记得加一次上报 —— 而漏掉的表现是
  "这种怪客户端的子弹打不掉血"，静默且难查。
- **为什么"客户端就上报"是安全判据**：服务端**永远不会**在客户端的副本上调用 `TakeDamage`
  （它只在自己的那份上结算），所以客户端上出现的每一次 `TakeDamage` 都必然是本地产生的。
- 调用点有六处（子弹 ×2、环绕物、范围伤害、光束、敌人接触伤害），全部**未改动**。

**服务端侧**（`NetworkPlayerState.CmdApplyDamage`）做的是**形状校验，不是命中校验**：
目标存在、不能是自己、不能是玩家（合作模式不互伤）、数值有限且不超上限。
另外 `NetworkPlayerState.LocalSender` 在 `OnStartLocalPlayer` 里登记 ——
`[Command]` 只能由自己拥有的对象发出，客户端要上报任何东西都得先找到自己的玩家。

⚠️ **这是一次有意识的取舍**：`MirrorPlan` 的 4.4 首选是"服务端生成投射物 + 服务端判定命中"，
那是完整做法。这里先用**上报**方案，因为它**不动投射物与武器那一整套**，
能先把"客户端打得动怪"打通。代价是**服务端校验不了"这一枪是否真的打中了"**。
取舍写在 `DamageRouter` 的类注释里，将来要收紧只需替换 `ForwardToServer`。

#### 验证：双进程测试新增了一条跨进程的伤害往返断言

服务端与客户端各自用**确定性规则**（netId 最小的那只怪）选出同一个目标，所以不需要为测试加任何跨进程通信：

```
client: 对 netId=5 的敌人打一发致命伤害（本地 TakeDamage，应被路由到服务端）
client: 满足：敌人被服务端结算并销毁，客户端收到销毁
server: 满足：客户端上报的伤害被服务端结算，敌人已销毁
```

这条链路横跨本项目最容易出错的三样东西：`[Command]` 的发送方身份（`LocalSender`）、
netId 在两端的对应关系、以及"客户端不本地结算"这个约定。
**它在 Host 测试里完全测不到** —— Host 下 `TakeDamage` 直接本地结算，走不到路由。

**P4 剩余**：4.1/4.3/4.4 的完整做法（服务端生成投射物）、4.5 塔网络化、4.6 升级三选一、
4.7 经验球、4.8 经验等级同步、4.9 句柄与销毁顺序；以及 P2.5 的装备同步（远程玩家目前空手）。

### 2026-10-08 · ⭐ 双进程联机测试（补上"客户端那一半"的验证手段）

#### 为什么非做不可

上一轮那个 `NetworkAuthority` bug 说明了一件事：**Host 单进程测试对客户端侧路径给的是虚假信心**。
Host 里服务端与客户端是**同一个对象**，所有 `ApplyNetwork*` 都会因权威守卫提前返回 ——
"客户端真的按广播走"这条路径**在 Host 下原理上就测不到**。

#### 怎么绕开"两个 Unity 不能开同一个工程"

镜像工程 + 两个实例：

| 角色 | 工程 | 入口 |
|---|---|---|
| 服务端 | 本仓库 | `NetworkSmokeTest.RunServerFromCommandLine` |
| 客户端 | 镜像副本（默认 `D:\unity\proj\SurvivorClient`） | `NetworkSmokeTest.RunClientFromCommandLine` |

命令：`& Tools\run-network-2p.ps1`。脚本每次先用 `robocopy /MIR` 同步
`Assets`/`Packages`/`ProjectSettings`（**不动副本的 `Library`**，所以只有改动的资源需要重新导入 ——
这是它便宜到能当回归工具用的原因）。

镜像的首次创建是一次性的 8.8 GB 拷贝，命令写在 `Tools/README.md` 里。

#### ⚠️ 第一版测试是**没有鉴别力的**，这一点值得记下来

第一版客户端断言的是"推车在动、时钟在走"。但**在旧 bug 下这两条也会通过** ——
客户端自己推进推车、自己累加时钟，看起来一样在动。**测试全绿而 bug 仍在**。

改成两条**只有广播才能产生**的判据：

1. **哨兵波次**：服务端在关卡里把 `CurrentWave` 设成 `42`，
   客户端断言自己看到了 42 —— 客户端**不可能自己算出**这个值
   （波次在正常流程里根本没人改过，"客户端是 0"和"广播没生效"长得一模一样）。
2. **`CartController.AppliedNetworkStateCount > 0`**：一个只在"广播真的被应用"时才增长的诊断计数。
   权威判据写错时它是 0 —— 而那个症状本身是**完全静默**的（不报错、不掉帧、画面正常）。

#### 实测结果（`Logs/2p-server.log` / `Logs/2p-client.log`，两端都 `SMOKE_OK`）

客户端（**真正的第二个进程**）验证到的：

- 连上服务端，房间里看到**两个**玩家（`connId=1883311051 address=127.0.0.1`）；
- **恰好一个**角色的 `isLocalPlayer` 为真 —— 认错人的后果是相机跟错、输入给错；
- **跟着服务端切到关卡**（客户端自己不切场景）；
- 关卡内**敌人副本 `netId != 0`** ⇒ **assetId 与 `spawnPrefabs` 对真正的远程客户端是通的**
  （这一条以前从没被验证过；漏配的症状是客户端报 "Failed to spawn server object"，而 Host 端一切正常）；
- **哨兵波次 42 到达客户端** ⇒ `StageStateMessage` 的注册、广播、处理器、`ApplyNetworkClock` 的守卫全部正确；
- **已应用 28 条推车状态广播** ⇒ `CartController.ApplyNetworkState` 真的在执行。

### 2026-10-08 · ⚠️ 修正 P3.6 的一个**失效的权威判据** + P3.7 阶段/胜负同步（☑ 服务端侧已验证）

#### 先说 bug：上一轮的权威判据对**场景对象**完全无效

`NetworkAuthority` 最初的实现是：

```csharp
public bool IsAuthority => _identity == null || _identity.isServer;   // ← 错
```

对**网络对象**（敌人 prefab 实例）它是对的。但对**没有 `NetworkIdentity` 的场景对象**
（推车、`StageDirector`、`EnemyBoundary`）`_identity` 恒为 `null` ⇒ **恒为 `true`** ⇒ 等于没判。
后果是**客户端仍然自己推进推车、自己推进阶段、自己判胜负**，而 P3.6 花了一整轮加的那些守卫
一个都没生效。**Host 单进程的自动化测试完全看不出来**（Host 本来就是服务端）。

改成按对象形态分两种判据：

```csharp
if (_identity != null) return _identity.isServer;              // 网络对象：看副本归属
return !NetworkBootstrap.IsActive || NetworkServer.active;      // 场景对象：看本进程是不是服务端
```

（场景对象两端各有一份**互相独立的本地实例**，只能问"本进程是不是服务端"；
单机没有会话 ⇒ 恒为权威，单机行为不变。）

**教训**：`IsAuthority` 这种东西必须按"对象是怎么存在的"分情况，
而不是一句 `identity == null || isServer` 就完事 —— 后者在单机下永远为真，
于是**联机时才失效，而单机测试全绿**。同一个坑还差点让 `GameLevelManager` 的时钟守卫重演一遍
（它不是网络对象，用 `NetworkServer.active` 才对）。

#### P3.7 阶段 / 胜负 / 结算同步

| 文件 | 作用 |
|---|---|
| `Messages/StageStateMessage.cs` | `StageStateMessage`（Phase / LevelTime / CurrentWave，2Hz）+ `RunResultMessage`（结算，一次性）。字段**摊平成基元类型**，不塞 `RunResult`，避免依赖 Weaver 对自定义类型的自动读写器 |
| `Core/Network/StageNetworkSync.cs` | 服务端 2Hz 广播；客户端处理器转发。由 `StageDirector.Start` **运行时 `AddComponent`** —— 不走场景接线（场景对象引用在批处理脚本下最容易出问题） |
| `StageDirector.ApplyNetworkPhase` | 只改状态 + 发事件。推进条件（节点清完 / 全员阵亡 / 抵达终点）**全部留在服务端** |
| `StageDirector.ApplyNetworkResult` | 客户端走一遍**本机**的 `RunFinished` ⇒ 场景里的 `RunSettlement` 照常写**本机**档案 + 弹面板。奖励用服务端下发的数值，客户端**不重算**（各人的击杀统计本来就不一样） |
| `GameLevelManager.ApplyNetworkClock` | 客户端应用时钟与波次。**不做本地预测**（2Hz 对只显示到秒的 HUD 足够；本地累加会与权威值漂移，变成"各端时间不一样"却都看着正常） |
| `GameLevelManager.Update` | 联机时客户端不再本地累加 `_levelTime` |
| `IGameLevelManager` | 接口加了 `ApplyNetworkClock`（`GameLevelManager` 是唯一实现者，已确认） |

结算的**双份风险**已经在两处挡住：`StageDirector.FinishRun` 只发一次 `RunFinished`，
Host 下广播回来的 `RunResultMessage` 又会被 `ApplyNetworkResult` 的权威守卫挡掉 ——
否则本机会发两次金币。

#### 证据

`Tools/run-network-smoke.ps1` → `SMOKE_OK`，新增断言：
`关卡时钟 = 2.04 秒，阶段 = Travelling` ——
验证的是两处新守卫（`CartController` / `GameLevelManager` / `StageDirector`）
**没有把服务端自己挡住**（判据写反的症状是"车不动、时钟停在 0"，且没有任何报错）。

⚠️ **仍然是 Host 单进程的测试**：`ApplyNetworkPhase` / `ApplyNetworkResult` / `ApplyNetworkClock` /
`ApplyNetworkState` 在 Host 下都会因权威守卫提前返回，也就是说
「**客户端真的按广播走**」这条路径只有**真正的第二个进程**能验证。
这一条现在是最重要的待人工确认项 —— 我已经把它列进下面的 Play 清单。

### 2026-10-08 · P3.6 推车状态同步（☑ 服务端侧已由冒烟测试覆盖）

**方案：推车刻意不做成网络对象。**

给它挂 `NetworkIdentity` 会让它变成 Mirror 的**场景对象**，而
`NetworkScenePostProcess` 会在进 Play 时**强制 `SetActive(false)`**
（`Assets/Mirror/Editor/NetworkScenePostProcess.cs:103`），
只有 `NetworkServer.SpawnObjects()` 才会把它激活 ——
**本项目单机模式仍然要能玩，单机没有服务端 ⇒ 推车永远不会被激活 ⇒ 整个关卡瘫掉。**

所以两端各留一份本地实例（场景里那份，**已有的接线一行没动** ——
`StageDirector.Cart`、`CartRepairInteractable.Cart` 都还是场景引用），
只把"权威进度"从服务端广播过来：

| 文件 | 作用 |
|---|---|
| `Core/Network/Messages/CartStateMessage.cs` | 结构体：`Distance` / `HealthNormalized` / `IsMoving` / `IsDisabled`（< 20 字节） |
| `Entity/Cart/CartNetworkSync.cs` | 服务端 15Hz `NetworkServer.SendToAll`；客户端处理器转发给本地推车 |
| `CartController.ApplyNetworkState` | 客户端应用：设 `_distance` → 摆位 → 触发 `DisabledChanged`。**刻意不触发 `ReachedNode`**（到点该不该继续走是阶段决策，归服务端） |
| `CartHealthController.ApplyNetworkHealth` | 只改数据 + 发事件，**不走 `TakeDamage`/`Heal`**（那些是结算，客户端跑就等于两端各结算一次）；带 0.01 容差，否则 15Hz 广播会让 `HealthChanged` 每帧都发（订阅方里有 TMP 文本） |
| `StageDirector` | `Start`/`Update`/`FinishRun` 加权威守卫 —— 客户端不再自己推进阶段、不再自己判"抵达终点＝胜利" |

**消息处理器的注册时机是个坑**：`CartNetworkSync.RegisterClientHandler()` 必须由
`SurvivorNetworkManager.OnStartClient` 调用，**不能放在本组件的 `Start` 里** ——
服务端在关卡加载完就开始广播，而客户端的场景对象要到场景加载完才出现，
晚注册会漏掉开头几条（表现是"进关卡后推车停着不动，过一会儿才追上"）。

**证据**：`Tools/run-network-smoke.ps1` → `SMOKE_OK`，新增断言
`推车已行驶 0.61 弧长` —— 它验证的是**权威守卫没有把服务端自己挡住**
（判据写反或 `NetworkIdentity` 缺失的表现是"车永远不动"，且没有任何报错）。

⚠️ **冒烟测试覆盖不到的部分**：Host 模式下服务端与客户端是**同一个对象**，
`ApplyNetworkState` 会因为 `IsAuthority` 为真而提前返回 —— 也就是说
"客户端真的按广播摆位"这条路径只有**真正的第二个进程**能验证。这是人工 Play 的必查项。

**P3 剩余**

- **P3.7 阶段/胜负的网络化**：服务端现在是唯一权威了，但 `Phase` 还没同步给客户端，
  结算面板也还没走 `[TargetRpc]` —— 客户端在胜负发生时**什么都看不到**。
- **P3.5 `GameLevelManager`**：`LevelTime` / `CurrentWave` 仍是各端各算
  （只影响 HUD 显示，因为生成权已经在服务端）。
- **P3.8 `RunStatsTracker`**：击杀统计只在服务端累计，还没送给客户端。
- **P4.7 经验球**：客户端看不到也捡不到经验 —— 当前最明显的已知缺口。

### 2026-10-08 · P3.1–P3.4 敌人服务端权威（☑ 已由冒烟测试覆盖）

**做了什么**

- 新增 `NetworkAuthority`（`Core/Network/NetworkAuthority.cs`）：与 `LocalPlayerGuard` 配对的另一半判据 ——
  前者回答"是不是**我**的角色"（输入相关），后者回答"该不该由**这一端**结算"（权威相关）。
  非网络对象（单机）恒为 `true`，所以单机流程一行分支都不用加。
- **`EnemyController`**：`FixedUpdate`（寻敌/移动/击退）与 `HitImpact` 加权威守卫；
  `OnEnable/OnDisable` 的注册只在服务端做（客户端注册会让本地计数与服务端分叉，
  而 `EnemySpawner` 的上限判定正是读这个计数）。`EnhanceWithWave` 改名 `ApplyWaveEnhancement` 并公开 ——
  联机路径不走池，也就不会经过 `OnGetFromPool`。
- **`EnemyHealthController`**：`TakeDamage` / `HurtColliders` / 接触伤害的即时结算全部加权威守卫；
  `Die()` 在联机下改走 `NetworkServer.Destroy`。
  **敌人血量刻意不做 SyncVar** —— 死亡本身就是同步信号（`Destroy` 会广播），
  血量只有"血条"这一个消费者，而敌人没有血条。
- **`EnemyTargetFinder`**：`Start`/`Update` 加守卫（客户端不跑 AI，每 0.5s 一次的寻敌纯属白烧 CPU）。
- **`EnemyBoundary`**：越界回收只在服务端，且联机下用 `NetworkServer.Destroy`
  （客户端自己 `Destroy` 会绕过 Mirror：服务端那只还活着，而客户端已经看不见它了）。
- **`EnemySpawner`**：`Update` 在"联机且非服务端"时直接返回（顺带跳过客户端的启动期体检，
  避免同一份配置错误在每端各报一遍）；`Spawn` 分流到新增的 `SpawnNetworked`
  （`Instantiate` → 注入 EntitySO → 波次/哨站难度 → `NetworkServer.Spawn`，**不走对象池**）。
  单机路径原样保留。
- **`NetworkSetup.cs`** 改为**扫目录**（`Assets/Game/Prefabs/EnemyPrefab`）而不是硬编码文件名：
  给每个 prefab 补 `NetworkIdentity` + `NetworkRigidbodyUnreliable2D`（`ServerToClient`，
  `syncInterval = 0.05` 即 20Hz），并把它们登记进 `spawnPrefabs`。
  硬编码清单最容易出的问题是**漏一个**，而漏掉的症状是"只有纯客户端会报
  Failed to spawn server object，Host 端一切正常"——最难查的那种。

**证据**：`Tools/run-network-smoke.ps1` → `SMOKE_OK`，其中新增的断言通过：
`关卡内敌人数量 = 1（均已 spawn，netId != 0）`，`NetworkBootstrap` 日志里 `spawnPrefabs=7`。

断言刻意**不**用 `GameLevelManager.GetEnemyCount()` —— 那个计数由 `OnEnable` 维护，
而 `OnEnable` 在 `Instantiate` 时就跑了，即使 `NetworkServer.Spawn` 失败计数照样是正的。
只有查 `netId != 0` 才能证明"这只怪真的被 Mirror 接管了"。

**仍未做（P3 剩余）**

- **P3.6 推车 + P3.7 关卡阶段/胜负**：这两个必须一起做 —— 客户端目前仍会自己推进
  `CartController._distance` 并自己判定胜负（点终点、全灭）。它们现在**碰巧**大致同步
  （同一条路径、同一速度、同一时刻起步），但一旦发生停摆/修理就会分叉。
  做法：推车加 `NetworkTransform`（服务端权威），`StageDirector` 只在服务端推进阶段。
- **P3.5 `GameLevelManager`**：`LevelTime` / `CurrentWave` 仍是各端各算；
  关卡种子（`RunSession.seed`）也还是本机的 `Environment.TickCount`，各端敌人生成序列不同 ——
  但因为生成权已经在服务端，客户端那份种子已经不影响了。
- **P3.8 `RunStatsTracker`**：击杀统计目前只在服务端累计（`EnemyHealthController.TakeDamage`
  已被权威守卫挡住），但结算面板还没接网络。
- **P4.7 经验球**：目前只在服务端生成本地实例，**客户端看不到也捡不到经验** ——
  这是当前最明显的已知缺口。

### 2026-10-08 · P0.4 技术未知项**已用无头冒烟测试回答**（☑）

**结论：跨场景重建玩家这条链路是通的。** 证据：`Logs/r1-smoke3.log` 里的 `SMOKE_OK` + 退出码 0。

跑法（编辑器必须关着，**不要加 `-quit`**）：

```powershell
& Tools\compile-check.ps1 -LogName compile.log          # 编译校验
& Tools\run-network-smoke.ps1 -LogName smoke.log        # 端到端冒烟（会真的进 Play）
```

三个脚本的用途与踩坑见 `Tools/README.md`。

冒烟测试（`Assets/Editor/NetworkSmokeTest.cs` 入口 + `Assets/Script/Core/Network/NetworkSmokeDriver.cs` 运行时驱动）
逐步验证并全部通过：

1. 组合根装配 `NetworkManager`（Addressables 的 `Net/NetworkManager` → `transport=KcpTransport`、`spawnPrefabs=1`）；
2. 大厅生成**离线**玩家；
3. `StartHost()` —— **不切场景**（断言 `GetActiveScene().path == Lobby`）；
4. 服务端生成**恰好一个**玩家（离线那个已让位）—— 即 `OnServerReady` 路径成立；
5. `ServerChangeScene` → Level0 → **玩家在新场景重建，数量仍为 1** ⟵ **这就是 P0.4 要回答的问题**；
6. `ServerChangeScene` → Lobby → 再次重建，数量仍为 1（不累积）；
7. 回到大厅后 `Time.timeScale == 1`。

**它验证不了的**（仍然必须由人 Play）：真正的远程客户端、输入、相机、UI、画面表现。
**下一步的人工 Play 因此可以缩到**：两个实例互连、各控各的角色、互相看得见 ——
连接/生成/切场景/重建已经由这台机器上的自动化覆盖了。

**顺带记两个坑**

- **写 SyncVar 不能早于激活**：`NetworkPlayerState.ServerSetCharacter` 一开始放在 `SetActive(true)` 之前，
  直接抛 `NullReferenceException` —— Weaver 生成的是**属性** `Network_characterId`，
  它要用 `NetworkBehaviour.netIdentity`，而未激活对象还没跑 `Awake`、`netIdentity` 是 null。
  正确顺序：注入数值（激活前）→ `SetActive(true)` → 写 SyncVar（spawn 前）→ `AddPlayerForConnection`。
  **这个 bug 是冒烟测试抓到的**，靠读代码不容易发现。
- **域重载其实是开启的**（见 `CLAUDE.md` 项目事实的更正）。这决定了冒烟测试的驱动**必须**放在
  运行时程序集里：挂在 `EditorApplication.update` 上的 Editor 状态机会在进入 Play 的瞬间被清掉，
  表现是"进了 Play 之后再无任何输出"（第一次跑就是这么挂住的）。

### 2026-10-08 · P0 完成 + P1/P2 第一段垂直切片（待 Play 验证）

**已完成并验证（批处理编译，无 `error CS`、Weaver 正常）**

- **P0.1/P0.2 基线**：改动前先跑了一次批处理编译，确认工作区本身是干净的
  （`Tundra build success`、无 `error CS`、`Mirror | mirror-networking.com` banner 出现 → Weaver 生效）。
- **P0.3 本地补丁**：`NetworkConnection()` 的 `Time.time` 加了 `try/catch`，并连带把 `IsAlive`
  改成"`lastMessageTime <= 0` 视为存活"（只加 try/catch 会让**新连接被立刻踢掉**）。
  台账见 `Docs/Mirror/local-patches.md`，可用 `grep "本地补丁" Assets/Mirror/` 查全。
- **P0.6 场景口径**：`offlineScene = Assets/Scenes/Lobby.unity`（路径口径），`onlineScene` 留空。
- **P1.1/P1.2 联机组合根**：`NetworkBootstrap`（组合根持有，可降级）+ `SurvivorNetworkManager`；
  `Net/NetworkManager` prefab 由 `Assets/Editor/NetworkSetup.cs` 生成，进了 Addressables 的 `Net` 组。
- **P1.3 场景分派**：`SceneFlow` 现在按网络角色分派（服务端 `ServerChangeScene` / 客户端不动 / 离线照旧），
  并统一了 `MenuPath` / `LobbyPath` / `StagePath` 常量。
- **P1.5 会话表**：`INetworkSessionService` + `NetworkSessionService`（**纯 MonoBehaviour，不是 NetworkBehaviour**
  —— DDOL 上的 `NetworkBehaviour` 永远拿不到 `netId`）。
- **P1.4（临时）**：`DevNetworkPanel`（IMGUI，`UNITY_EDITOR || DEVELOPMENT_BUILD` 包住）承担建房/加入/切场景/选角。
  **正式的房间面板落地后整个文件删掉**，自举也在文件内，不需要动 `GameBootstrap`。
- **P2.1/P2.2 玩家网络化**：Player prefab 加了 `NetworkIdentity` + `NetworkRigidbodyReliable2D(ClientToServer)`
  + `NetworkPlayerState`；`PlayerSpawner.SpawnForConnectionAsync` 走
  「`Instantiate`（未激活）→ 注入 → 写角色 id → **自己** `SetActive` → `AddPlayerForConnection`」。
- **P2.3/P2.4 本地守卫**：`PlayerController` 懒获取输入、只给本地玩家；`PlayerManager.LocalPlayer`
  改按 `NetworkIdentity.isLocalPlayer` 判（不再是"第一个注册的"）。
  新增 `LocalPlayerGuard` 统一"是不是本机玩家"的判据，`PlayerAnimationController` / `PlayerInteraction` /
  `GunWeapon` 都改用它 —— **远程副本读本地输入**是这一轮最容易漏的一类 bug（不报错，只是所有人一起动）。

**未完成 / 已知缺口**

- P1.4 正式房间面板（`NetworkRoomPanel` + prefab + Addressables）未做。
- P1.6 断线处理只有日志，没有 UI 反馈。
- P2.5 只同步了 `characterId`；**装备（loadout）/ 等级 / 经验 / 升级点都还没同步** ——
  所以联机时远程玩家目前是**空手**的。
- P2.6 血量未同步（各自一份，敌我伤害还没联网）。
- P2.8 相机绑定未在联机下实测。

### Play 验证清单（人工部分）

> **大部分已经被自动化覆盖了**：
> - `Tools\run-network-smoke.ps1`（Host 单进程）：连接、`OnServerReady` 生成玩家、建房不切场景、
>   跨场景重建、玩家数量稳定、`timeScale`、敌人生成、推车推进、关卡时钟。
> - `Tools\run-network-2p.ps1`（**双进程**，⭐ 覆盖面大得多）：上面这些 + **客户端侧的一切** ——
>   看到两端玩家、认出自己的角色、跟着切场景、敌人在客户端也有 netId、哨兵波次到达、
>   推车广播被应用。
>
> 下面只列**两台自动化都验证不了**的部分：输入、相机、画面表现、人机交互。

1. **各控各的**：客户端按住方向键，只有自己的角色动（对方静止）；按 E 只有自己交互。
2. **相机**：每个实例的相机只跟自己的角色（同时存在本地与远程玩家，取错就跟错人）。
3. **画面表现**：走动时对方角色的动画方向对不对（远程副本的行走动画是按位移反推的）。
4. **客户端侧的胜负结算**：打完一局，客户端应**也弹出结算面板**，数值与 Host 一致
   （`StageDirector.ApplyNetworkResult`）。自动化只覆盖到进入关卡后的十几秒，跑不完一整局。
5. **离开房间** → 回到离线大厅，`Time.timeScale == 1`，能再次建房（重开一局的状态清理）。
