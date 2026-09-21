# Survivor 计划（重构 → 联机）

> 单一计划源。重构目标：在接入联机（Mirror）之前，划清「本地表现层」与「跨端权威状态」的边界，
> 消除重复引导与分散单例，补全 Manager 接口化，让 UI / 音频成为可替换的本地下游消费层。
>
> 状态：**草案已定稿，等待开工确认。**

---

## 0. 决策记录（已确认）

| # | 决策点 | 结论 |
|---|---|---|
| 1 | 重构范围 | **核心**：阶段 1-4、6。目录/命名空间保持现状；按玩家实例化（阶段 5）留待联机前 |
| 2 | UI 重构深度 | **全量**：`IUIService` + `PlayerHudBinder` + 事件驱动，面板不再直连 Manager（12 个面板） |
| 3 | 音频重构深度 | **中等**：`IAudioService` + AudioSource 对象池；保留 `ResourceEnum` 与现有调用点；不暴露 `audioSource` |
| 4 | Addressables 缺失 | **由 AI 写幂等 Editor 批处理脚本**生成 Settings 与分组，按目录/文件名写入地址 |
| 5 | 联机库选型 | **Mirror**（阶段 8 才接入；接口保持 netcode 无关） |
| 6 | UI 数据流 | **强类型 C# 事件 + 接口**；`EventCenter` 仅保留通用本地通知 |
| 7 | 计划文档 | 统一为 **`Docs/Plan.md`**；删除过时的 `Assets/InstanceRefactorChecklist.md` |

---

## 1. 现状快照与已知缺陷

### 已具备的良好基础
- EDM 数据驱动数值系统（`EntityBehaviour` + `EntityStatModel` + `LevelUpSO`）。
- 集中式实体配置（`EntityCatalogSO` + `EntityType` enum，`SOManager` O(1) 查找）。
- `ServiceLocator` 已存在。
- 已接口化：`IPlayerManager`、`IGameLevelManager`、`IExperienceController`、`IInputHandle`、`IAssetService`。
- 平台无关输入工厂 `InputHandleFactory`（含 `GetInput(id)` 多输入源预留）。

### 缺陷清单
1. **引导重复**：`Main.cs`（挂在 `Assets/Game/Prefabs/Start.prefab`，位于 Menu 场景）与
   `GameLevelManager.Start()` 各自创建 `AssetService`、初始化 `UIManager`/`BKMusic`。无统一组合根。
2. **Manager 接口化不完整**：`WeaponManager`/`TowerManager`/`UIManager`/`BKMusic`/`SOManager`/`DamageNumManager`
   仍以 `.Instance` 被直接调用（60+ 处）。
3. **全局单实例假设**：`PlayerManager.LocalPlayer` 唯一、`WeaponManager.weaponSlots` 全局挂场景、
   `ExperienceLevController` 场景单例并回退全局 —— 多玩家会冲突（阶段 5 处理）。
4. **UI 耦合 Manager**：面板直接调 `UIManager.Instance`/`BKMusic.Instance`/`WeaponManager.Instance`/`SOManager.Instance`；
   每个面板各自订阅 `OnEscape`（多面板同时响应）；预加载列表硬编码在 `UIManager.InitializeAsync()`。
5. **音频粗糙**：`BKMusic` 纯 C# 单例，每次播放 `new GameObject` + 1 秒后 `Destroy`，无池；
   直接暴露 `audioSource` 给 UI 调 `mute`/`volume`。
6. **事件用途混杂**：`EventCenter`（enum key）既做表现解耦，又承担 `玩家死亡 → GameOver` 这类权威状态流转。
7. **Addressables 配置缺失（阻塞）**：仓库 `Assets/` 下无 `AddressableAssetsData`，代码按地址加载必失败。
8. **`InputReaderManager` 未挂场景（Windows 阻塞）**：两个场景均无该 Manager，
   `InputHandleFactory.CreateLocalInput()` 在 Windows 下返回 null → 键鼠输入不可用。
   （输入资源资产已存在：`Assets/Prefabs/Common/InputReader.asset`）
9. **`Music/Walk` 地址无对应资源（隐藏 bug）**：`ResourceEnum.Walk` 对应地址 `Music/Walk`，
   实际文件为 `PlayerMove.wav`，`BKMusic.InitializeAsync()` 枚举全部音频时会加载失败。该枚举值当前无引用。

### 资源地址现状（代码中硬编码）
```
UI/Canvas            UI/<PanelName>       Common/ExpSprite
Common/TetoBullet    Common/SpriteToHandle
Weapon/Bullet        Weapon/Spin          Music/<ResourceEnum>
```

---

## 2. 重构原则（含设计模式标注）

- **组合根（Composition Root）+ 服务定位器（Service Locator）/ 依赖倒置（DIP）**：
  服务在唯一起点按依赖顺序注册，消费方只依赖接口。
- **关注点分离（SoC）**：核心状态变更只改数据并发强类型事件；UI/音效由订阅者处理。
- **单一职责（SRP）/ 开闭原则（OCP）**：新增武器/塔/面板只加子类，不改现有分支。
- **单一事实源（SSOT）/ 数据只读**：SO 仅作配置，运行时状态放服务字段，禁止写回 SO。
- **联机边界**：`ServiceLocator`、C# event 只服务本进程；跨端状态必须走 Mirror（阶段 8）。

---

## 3. 阶段与检查清单

### 阶段 1 — 基础设施与引导  ✅ 已完成（2026-09-21）
- [x] **0.0 恢复基线**：修复 8 个既有编译错误（`.Task).Result` ×6、`ResourceLocators.Count` 缺 LINQ、`PreloadPanelAsync<TowerHealthPanel>` 约束不满足）。开工前工作树已无法编译（`Assembly-CSharp.dll` 早于源码修改时间）。
- [x] **1.1 Addressables 自动配置脚本**：`Assets/Editor/AddressablesSetup.cs`（幂等；菜单 `Tools ▸ Setup Addressables` / 批处理 `SetupFromCommandLine`；完成后打印 `CLI_OK`）。已实际运行生成 `Assets/AddressableAssetsData`（UI/Common/Weapon/Music 四组，含 `Music/PlayerMove`）。
- [x] **1.2 新建 `GameBootstrap`（组合根）** `Assets/Script/Core/GameBootstrap.cs`：`[RuntimeInitializeOnLoadMethod]` 自建，注册 `IAssetService`、初始化 `UIManager`/音频、PC 下自建 `InputReaderManager`；暴露 `GameBootstrap.Ready`。
- [x] **1.3 删除重复引导**：`Main.cs` 改为 `await Ready` 后展示 `MenuPanel`；`GameLevelManager.Start()` 移除自建 `AssetService`/`UIManager`/`BKMusic`，改为 `await Ready`。
- [x] **1.4 修复 `InputReaderManager` 未挂载**：`InputReaderManager` 运行时自建 `InputReader`；PC 平台场景缺失时由 `InputHandleFactory` 按需创建（不覆盖场景内已有实例，避免 `Start.prefab` 被重复销毁）。
- [x] **1.5 `ServiceLocator` 增强**：新增 `IsRegistered<T>()`、`Clear()`。
- [x] **1.6 资产地址常量**：新增 `Assets/Script/Core/AssetKeys.cs`，替换全部硬编码地址。
- [x] **1.7 修复 `Music/Walk`**：`ResourceEnum.Walk` → `PlayerMove`（与实际资源 `PlayerMove.wav` 匹配）。
- [x] **1.8 删除过时清单**：移除 `Assets/InstanceRefactorChecklist.md`。

**验证**：离线 Roslyn 编译 Assembly-CSharp / Editor 脚本均 0 error；Unity 批处理导入编译 0 error 并生成 Addressables 配置。**待用户 Play 确认运行时。**


### 阶段 2 — 接口补全（Manager → Service）  ✅ 已完成（2026-09-21）
- [x] `ISOManager` + `SOManager`（自注册 + `Service` 回退）→ 调用点已迁移（`EntityBehaviour`/`BaseTower`/`LevelUpPanel`/`TowerLevelUpPanel`）
- [x] `IWeaponManager` + `WeaponManager`（`WeaponSlots`/`Weapons`/`GetWeapon<T>`）→ 调用点已迁移
- [x] `ITowerManager` + `TowerManager`（`Towers`）→ 调用点已迁移（`EnemyTargetFinder`/`BaseTower`）
- [x] `IDamageNumService` + `DamageNumManager`（`SpawnDamageNum`/`ReturnToPool`）→ 调用点已迁移（各 HealthController/`DamageNumText`）
- [x] `IUIService` + `UIManager`：接口实现并在组合根注册；`GameLevelManager`/`DetectPlayer`/`TowerPlacementController`/`ExperienceLevController`/`Main` 已迁移
- [x] `IGameLevelManager` 语义：接口文档已注明「权威状态 vs 本地暂停表现」
- [ ] `IAudioService`/`AudioService`：**接口已定义，实现与调用点迁移并入阶段 4**（与面板音频一起，避免重复改动）
- [ ] 面板内 `UIManager.Instance` 调用：**并入阶段 3 的 UI 重写**（面板会被事件驱动改造，避免二次改动）
- [ ] `IPlayerManager.GetPlayer(int id)`：**并入阶段 5**（当前 PlayerController 尚无网络 id，避免投机代码）

**验证**：离线 Roslyn + Unity 批处理编译均 0 error；新接口 `.meta` 已生成。**待用户 Play 确认运行时。**


### 阶段 3 — UI 系统重构（全量）  ✅ 已完成（2026-09-21）
- [x] `IUIService` 统一面板生命周期：显示栈 + `SetAsLastSibling` 层级 + **ESC 栈顶优先分发**（`HandleEscape` / `OnEscapeUnhandled`）
- [x] `BasePanel` 改造：移除隐式 `InputHandleFactory.GetInput("local")` 与逐面板 ESC 订阅；新增 `CanHandleEscape`；淡出回调置空防重入
- [x] **`PlayerHudBinder`**：订阅 `LocalPlayer` 的 `IExperienceController` 强类型事件刷新 `GamePanel`；`ExperienceLevController` 删除 `SyncUI()` 对 UI 的直接写入
- [x] 面板依赖解耦：全部 `UIManager.Instance` → `UIManager.Service`；`GameLevelManager` 不再直接订阅输入，改订阅 `OnEscapeUnhandled`
- [x] 预加载自动化：反射收集所有具体 `BasePanel` 子类并预加载（新增面板零改动）
- [x] ESC 行为补齐：设置面板新增 `EscLogic`（关闭并恢复游戏）
- [x] `LevelUpPanel` 已作用于 `LocalPlayer`（经 `PlayerManager.Service.LocalPlayer`）

**部分延后**：面板的「暂停/升级选择」仍直接调用 `IGameLevelManager`/`ISOManager`（属玩法相邻逻辑，联机前随阶段 5 一并改为按玩家 + 事件）；`ChooseWeaponPanel`/`ChooseTowerPanel` 的按玩家武器/塔归属属阶段 5。
**额外修复（验证中发现）**：移除全部 `Service` 回退（改由 `[DefaultExecutionOrder]` 保证初始化次序）；`BaseTower.ForEachValidTarget` 改快照遍历修复 Rin 越界；补 `EPress` 输入动作（`<Keyboard>/e`）接通 E 交互。
**验证**：离线 Roslyn + Unity 批处理编译 0 error。**待用户 Play 确认。**


### 阶段 4 — 音频系统重构（中等）  ✅ 已完成（2026-09-21，提前到阶段 3 之前以避免面板二次改动）
- [x] `IAudioService`：`PlayBgm`、`PlaySfx(ResourceEnum)`、`BgmMuted`/`SfxEnabled`、`BgmVolume`/`SfxVolume`
- [x] `AudioService`（`Core/Services/`）：BGM 独立源 + SFX `PlayOneShot` 对象池（8），替代每次 `new GameObject`
- [x] `GameSettingPanel`/`MusicSettingPanel` 改走 `IAudioService`，不再触碰 `audioSource`
- [x] 全部 38 处 `BKMusic` 调用点迁移（HealthController / 塔 / 武器 / 经验球 / 面板）
- [x] 删除 `BKMusic`


### 阶段 5 — 按玩家实例化（保守方案）  ✅ 已完成（2026-09-21）
- [x] `PlayerController` 持有自身 `ExperienceLevController`（`GetComponent`，去掉全局回退）
- [x] `ExperienceLevController` 不再注册为全局服务；所有消费点改用 `PlayerManager.LocalPlayer.ExperienceController`
- [x] `BaseWeapon.CanOperate()` 改为按父级玩家（`GetComponentInParent<PlayerController>()`），不再假设全局 `LocalPlayer`
- [x] ~~塔 owner/team~~ **不需要**：塔是合作模式下的共享资源，不区分为玩家归属（YAGNI）；升级/退款取「当前本地玩家」即可
- [x] **武器槽下放玩家**：新增 `PlayerWeaponController`（实现 `IWeaponManager`）挂在 Player 上，槽位由子武器经 `WeaponEntitySO.weaponSelect` 自动建立；删除全局 `WeaponManager`（场景组件已批处理迁移）

**验证**：离线 Roslyn + Unity 批处理编译 0 error。**待用户 Play 确认。**


### 阶段 6 — 事件与数据边界  ✅ 已完成（2026-09-21）
- [x] `EventCenter` 全局枚举事件总线**已删除**（无使用方）；状态流转改强类型/接口调用
- [x] `玩家死亡 → GameOver` 改为经 `IGameLevelManager.NotifyPlayerDied(player)`（权威边界预留，联机时由网络实现转发服务器）
- [x] SO 只读审计：唯一运行时改 SO 的 `WeaponSelectSO.OnSelect` 全局事件已移除；武器激活改由 `PlayerWeaponController.SelectWeapon` 直接处理
- [x] `EventEnum` 拆分：删除 `PlayerEnum`/`TowerEnum`；`ResourceEnum`（音频 ID）独立为 `Core/ResourceEnum.cs`


### 阶段 7 — 目录/命名空间对齐（**本次不做**）
- [ ] 用 `AssetDatabase.MoveAsset` 按功能域重组 + namespace（保留 GUID）

### 阶段 8 — 联机接入（Mirror）  ⏭ 计划见 `Docs/MirrorPlan.md`
- [ ] 前置：**手动导入 Mirror**（UPM / Asset Store / vendored）
- [ ] M1 网络基础设施 → M2 玩家网络化 → M3 玩法权威 → M4 投射物/塔/掉落同步 → M5 UI/音频复核 → M6 打磨
- 详细任务表、接口替换映射、验证矩阵见 `Docs/MirrorPlan.md`

---

## 4. 架构设计要点

### 组合根与服务注册
```
GameBootstrap (Start.prefab / 场景对象)
  └─ 按序 new/Find + ServiceLocator.Register<I*>()
        AssetService, InputReaderManager, AudioService, UIService,
        SOManager, DamageNumManager, PlayerManager, GameLevelManager, WeaponManager, TowerManager
```
- `ManagerSingleton<T>` 保留（供 Inspector 拖拽与场景生命周期绑定）；业务代码一律走 `ServiceLocator` 接口。
- 目标：把现有各 Manager 的「自注册于 `OnSingletonAwake`」逐步收敛到组合根统一注册，避免注册时序分散。

### UI（全量）
- `IUIService` 管面板注册、栈、层级、ESC 分发；面板不再互相抢订阅。
- `PlayerHudBinder` 订阅 `LocalPlayer` 的强类型事件；联机时每客户端只显示本地玩家 HUD，天然隔离。
- 新增面板零改动 `UIManager`（反射或目录 SO）。
- 面板只依赖 `IUIService` + 本地玩家只读数据，不依赖 `WeaponManager`/`SOManager`/`IAudioService`（音效经接口）。
- 面板内部禁止 `Find`/`GetInput`；生命周期由 `IUIService` 驱动。

### 音频（中等）
- `IAudioService` 统一入口 + 对象池；UI 经接口调音量/静音，不再触碰 `audioSource`。
- 保留 `ResourceEnum` 作为 clip id（不引入事件驱动 Presenter，按决策）。

### 联机边界（为阶段 8 预留）
- **权威状态**：关卡时间、波次、敌人生成、伤害结算、掉落。
- **本地表现**：UI、音频、伤害数字、相机。
- **接口替换点**：`IPlayerManager`/`IGameLevelManager`/`IInputHandle` 已有；补 `IWeaponManager`/`ITowerManager` 后即可替换为 Mirror 实现。

---

## 5. 需人工 / 批处理操作
1. **Addressables**：由 AI 编写并运行 Editor 批处理脚本（阶段 1.1）；你只需在脚本运行后抽查分组与地址。
2. **运行时验证**：批处理无法进入 Play，重构后每阶段由你 Play 确认（见第六节）。
3. **音频资源**：如后续引入 AudioMixer，分组参数需你拍板（本决策为中等范围，暂不引入）。

---

## 6. 验证方式
- 无自动化测试。每阶段：关闭 Unity → 批处理 `-executeMethod` 配置/编译 + Roslyn 离线编译校验 → 用户 Play 验证。
- 批处理只能验证编译与资源接线；**运行时表现必须由用户 Play 确认**。
- 每阶段收敛后再进入下一阶段，避免大规模并行改动导致无法定位回归。
