# Mirror 联机接入计划（阶段 8）

> 前置：`Docs/Plan.md` 的阶段 1-6 已完成（组合根 + 接口化 + UI/音频本地化 + 按玩家实例化 + 事件/权威边界）。
> 本文档是**唯一联机进度表**，每完成一项勾选并注明验证结果。
>
> **目标形态**：2-4 人**在线合作**（PvE，敌人/塔共享，无 PvP）。
> **权威模型**：**服务端权威**（敌人 AI/生成/伤害/波次/关卡时间/掉落），**玩家移动客户端权威**（低延迟、后用反作弊按需收紧）。

---

## 0. 前置决策与手动步骤

| # | 事项 | 谁做 | 状态 |
|---|---|---|---|
| 0.1 | 确定 Mirror 导入方式（UPM git / Asset Store / vendored `Assets/Mirror`） | 用户 | ☐ |
| 0.2 | 导入 Mirror 并确认编译通过（含 Weaver） | 用户 + 我 | ☐ |
| 0.3 | 确认联机库版本与 Mirror Weaver 生效（Console 无 Weaver 报错） | 我 | ☐ |
| 0.4 | 确认场景架构：是否新增 `Bootstrap`/`PersistentSystems` 场景（当前 Menu/Level0；建议先不拆） | 用户 + 我 | ☐ |
| 0.5 | 明确玩家上限与出生点（Level0 是单玩家场景，多玩家需运行时生成） | 用户 | ☐ |

**已知坑（来自项目 skill `unity-batch-autoconfig`）**：
- 编程式改预制体不触发 Mirror `assetId` 分配 → 需读取 `NetworkIdentity.assetId` getter 触发分配再保存。
- `AddComponent<NetworkTransform/NetworkRigidbody>()` 会触发 `Reset()` NRE（噪声，组件仍生效，随后显式设 `target`/`syncDirection`/`updateMethod`）。
- Unity 6 domain reload 序列化带 `NetworkIdentity` 的预制体会触发 `NetworkConnection()` 里 `Time.time` 异常 → vendored Mirror 打本地补丁。
- Mirror `SpawnMessage` 不含父子关系：客户端需在 `OnStartClient` 自行重建父子，或服务端算好世界坐标用 `NetworkTransform`。
- 删除场景 Player 会置空 Cinemachine 相机 `TrackingTarget` → 改由运行时脚本指向本地玩家。
- 每个 `NetworkServer.Spawn` 的预制体必须注册 `spawnPrefabs`（Player 单独放 `playerPrefab`）。
- 静态单例/`ServiceLocator`/C# 事件只服务本进程；跨端状态必须 `SyncVar`/`Command`/`ClientRpc`。

---

## 1. 进度总览

| 阶段 | 内容 | 依赖 | 预估 | 状态 |
|---|---|---|---|---|
| **M1** | 网络基础设施（NetworkManager / Player 预制体 / spawn 注册 / NetworkIdentity） | 0.x | 中 | ☐ |
| **M2** | 玩家网络化（NetworkPlayerManager / 输入 / 移动同步 / 相机） | M1 | 大 | ☐ |
| **M3** | 玩法权威（敌人 AI/生成/伤害/波次/关卡时间 服务端） | M2 | 大 | ☐ |
| **M4** | 投射物 / 塔 / 掉落同步（Teto 子弹、Spin/Gun 投射物、经验球、塔放置与升级） | M3 | 大 | ☐ |
| **M5** | UI / 音频本地隔离复核 + 端到端回归 | M4 | 中 | ☐ |
| **M6** | 打磨（断线重连、延迟、重开、异常处理） | M5 | 中 | ☐ |

---

## 2. 分阶段任务清单

### M1 — 网络基础设施
- [ ] 1.1 新建 `NetworkManager`（场景或 Prefab），配置 transport、playerPrefab、spawnPrefabs。
- [ ] 1.2 创建/改造 **Player 预制体**：`NetworkIdentity` + `PlayerController`/`ExperienceLevController`/`PlayerWeaponController`/血量/交互 + Cinemachine 目标。
- [ ] 1.3 给需要用 `NetworkServer.Spawn` 的预制体加 `NetworkIdentity`（敌人 3 种、Teto 子弹、Spin/Bullet 投射物、塔 3 种、经验球）。
- [ ] 1.4 所有 spawn 预制体登记 `NetworkManager.spawnPrefabs`；Player 放 `playerPrefab`。
- [ ] 1.5 处理 `assetId` 分配（见坑 1）与 domain reload 序列化异常（见坑 3）。
- [ ] 1.6 确定场景加载：`NetworkManager` 的网络场景 vs 现有 `Menu`/`Level0` 流程。

**验证**：Host 端启动 → 场景加载 → 1 个 Player 生成且不报 Weaver/Spawn 错误。

### M2 — 玩家网络化
- [ ] 2.1 新增 `NetworkPlayerManager : NetworkBehaviour, IPlayerManager`：`Register/Unregister` 由 `OnStartServer/OnStartClient` 驱动；`LocalPlayer` = `isLocalPlayer` 的实例。
- [ ] 2.2 `PlayerController` 增加 `NetworkIdentity` 生命周期守卫：非本地玩家不读输入；本地玩家接 `IInputHandle`。
- [ ] 2.3 `IPlayerManager` 在 `GameBootstrap`/网络启动时替换注册（本地实现 ↔ 网络实现）。
- [ ] 2.4 玩家移动：客户端权威（`NetworkTransform` client authority）或服务端权威（`[Command]` 输入 → 服务端 Rigidbody）——本计划默认**客户端权威**。
- [ ] 2.5 相机：运行时指向本地玩家（替换场景固定引用，见坑 5）。
- [ ] 2.6 经验/武器归属随 Player 实例自动隔离（阶段 5 已具备）。
- [ ] 2.7 `NetworkInputHandle`：工厂 `network_` 分支接远程输入（如需服务端权威时使用）。

**验证**：Host + Client 各自控制自己的角色；双方看到彼此移动；相机各跟各的。

### M3 — 玩法权威
- [ ] 3.1 `EnemySpawner` 改为 `[Server]` 权威生成；波次/`CurrentWave` 服务端维护（`NetworkGameLevelManager`）。
- [ ] 3.2 `NetworkGameLevelManager : NetworkBehaviour, IGameLevelManager`：`LevelTime`/波次 `SyncVar`；`NotifyPlayerDied` 走服务器（`[Command]` → 服务端判定）。
- [ ] 3.3 敌人 AI/寻敌/移动在服务端执行；客户端仅表现（`NetworkTransform`）。
- [ ] 3.4 伤害结算服务端权威：`TakeDamage`/`Heal` 在服务端，广播表现（伤害数字/音效本地）。
- [ ] 3.5 敌人对玩家的目标选择在服务端进行（`EnemyTargetFinder` 服务端）。
- [ ] 3.6 游戏结束：全部玩家死亡（服务端判定）→ `ClientRpc` 打开 `DeadPanel`。

**验证**：Client 攻击敌人 → 敌人血量/死亡在服务端结算且两端一致；全灭后两端都进结算。

### M4 — 投射物 / 塔 / 掉落同步
- [ ] 4.1 Teto 子弹：`NetworkIdentity` + 服务端 Spawn + `NetworkTransform`；命中判定服务端。
- [ ] 4.2 Spin/Gun 投射物：同上；武器攻击节奏由各玩家本地驱动，服务端做命中校验（按需）。
- [ ] 4.3 塔放置：`ChooseTowerPanel`/`TowerPlacementController` 放置经 `[Command]` 到服务端 Spawn；`TowerManager` 注册在服务端。
- [ ] 4.4 塔升级：`LevelUpSO.ApplyTo` 在服务端应用并同步；客户端仅表现。
- [ ] 4.5 经验球：`ExpSpritePool` 改为服务端权威 Spawn + 拾取判定；或改为「服务端直接发经验 + 本地特效」（推荐，简化）。
- [ ] 4.6 玩家 `StatModel`/升级结果同步：玩家自身 `SyncVar` 或在服务端应用后广播。

**验证**：两端看到的子弹/塔/掉落位置与结算一致；拾取经验两端等级一致。

### M5 — UI / 音频隔离复核 + 端到端回归
- [ ] 5.1 复核：`UIManager`/`AudioService`/`PlayerHudBinder`/`DamageNumManager` 只在本地运行，不被网络调用。
- [ ] 5.2 `PlayerHudBinder` 绑定本地玩家（`LocalPlayer`），联机下每端只显示自身 HUD。
- [ ] 5.3 面板（选择武器/升级/选塔/暂停）只作用于 `LocalPlayer`。
- [ ] 5.4 端到端回归：Host + Client 完整跑一局（菜单 → 选武器 → 战斗 → 塔 → 升级 → 死亡 → 返回菜单）。
- [ ] 5.5 「纯远程客户端」单独测试（不只测 Host）。

### M6 — 打磨
- [ ] 6.1 断线/重连处理（`OnServerDisconnect`/`OnClientDisconnect`）。
- [ ] 6.2 延迟与插值（`NetworkTransform` 插值、`sendInterval`）。
- [ ] 6.3 重开局/场景重载的网络状态清理（`ServiceLocator.Clear()` 时机）。
- [ ] 6.4 异常与边界（0 玩家、玩家中途加入）。

---

## 3. 接口替换映射（联机实现）

| 接口 | 本地实现 | 联机实现 | 说明 |
|---|---|---|---|
| `IPlayerManager` | `PlayerManager` | `NetworkPlayerManager` | `LocalPlayer` = 本地 `NetworkIdentity`；`AllPlayers` 同步 |
| `IGameLevelManager` | `GameLevelManager` | `NetworkGameLevelManager` | 时间/波次 `SyncVar`，结束服务端判定 |
| `IInputHandle` | `PCInputHandle`/`MobileInputHandle` | `NetworkInputHandle` | 仅在服务端权威移动时需要 |
| `IWeaponManager` | `PlayerWeaponController` | 同（随 Player 实例） | 每玩家独立，天然隔离 |
| `ITowerManager` | `TowerManager` | 服务端权威注册表 | 塔共享，无需 owner |
| `IAudioService`/`IUIService`/`IDamageNumService`/`ISOManager` | 本地实现 | 不变 | 纯本地/只读 |

注册时机：由网络启动流程在 `ServiceLocator` 中替换（`GameBootstrap` 之后、进入关卡之前）。

---

## 4. 风险与回退

| 风险 | 影响 | 缓解 |
|---|---|---|
| Mirror Weaver / `assetId` 未分配 | 客户端无法生成对象 | 批处理脚本 + 读取 `assetId` 触发分配 |
| 序列化 `Time.time` 异常（domain reload） | 编辑器报错 | vendored Mirror 打补丁 |
| 客户端权威移动被滥用 | 作弊 | 后续按需改服务端权威 |
| 静态服务跨端污染 | 状态错乱 | 跨端只走 `SyncVar`/Rpc；`ServiceLocator` 仅进程内 |
| 场景/预制体大改导致断引用 | 无法运行 | 全部用批处理脚本 + GUID 保持 |

---

## 5. 验证矩阵（每阶段收敛后必跑）

| 场景 | Host | 纯 Client |
|---|---|---|
| 启动 / 进入关卡 | ☐ | ☐ |
| 移动 / 相机 | ☐ | ☐ |
| 敌人生成 / 追击 / 伤害 / 死亡 | ☐ | ☐ |
| 武器开火 / 子弹命中 | ☐ | ☐ |
| 塔放置 / 升级 / 治疗 | ☐ | ☐ |
| 经验拾取 / 升级 | ☐ | ☐ |
| 死亡 → 结算 → 返回菜单 | ☐ | ☐ |
| Console 无红错 | ☐ | ☐ |
