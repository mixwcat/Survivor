# Changelog

## 2026-09-21 — 项目重组（阶段 6 + 武器归属）

### 事件与权威边界
- **删除 `EventCenter`** 全局枚举事件总线（连同 `PlayerEnum`/`TowerEnum`）；`ResourceEnum`（音频 ID）独立为 `Assets/Script/Core/ResourceEnum.cs`
- 玩家死亡改为 `PlayerHealthController.Die()` → `IGameLevelManager.NotifyPlayerDied(player)`（权威边界预留；联机实现将转发服务器）
- SO 只读：移除 `WeaponSelectSO.OnSelect` 全局事件；武器激活改由 `PlayerWeaponController.SelectWeapon` 直接处理

### 武器槽按玩家归属
- 新增 `Assets/Script/Entity/Player/PlayerWeaponController.cs`（实现 `IWeaponManager`），挂在 Player 上
  - 槽位由子物体 `BaseWeapon` → `WeaponEntitySO.weaponSelect` **自动建立**（免 Inspector 逐个拖拽）
- `WeaponEntitySO` 新增 `weaponSelect`；`Weapon_Spin/Gun.asset` 绑定 `SpinSelect/GunSelect`
- `PlayerController` 新增 `Weapons` 属性；`BaseWeapon` 经 `Owner.Weapons` 注册
- 消费点改用本地玩家：`SOManager.GetRandomPlayerLevelUpSOs`、`ChooseWeaponPanel`、`GamePanel`
- 删除全局 `WeaponManager`（`Assets/Editor/PlayerWeaponSetup.cs` 批处理迁移场景：Player 加组件、移除旧组件与空物体后删除脚本）

## 2026-09-21 — 项目重组（阶段 5）：按玩家实例化（保守方案）

### 经验按玩家
- `ExperienceLevController` 移除全局 `ServiceLocator` 注册与 `Service` 属性（经验控制器挂在 Player 上，按玩家实例化）
- `PlayerController.Awake` 从同物体 `GetComponent<ExperienceLevController>()`；`ExperienceController` 属性不再回退全局
- 消费点改用本地玩家：`ExpSpriteController`、`TowerPlacementController`、`ChooseTowerPanel`、`LevelUpPanel`、`TowerLevelUpPanel`（均经 `PlayerManager.Service.LocalPlayer.ExperienceController`）

### 武器按父级玩家
- `BaseWeapon.CanOperate()` 改为 `GetComponentInParent<PlayerController>()`，不再依赖全局 `LocalPlayer`

### 一并修复
- `TowerDataSO` 新增 `BulletSpeed` 并在 `FillStatModel` 写入（修复 `Tower_Teto 缺少Type：BulletSpeed`）；`Tower_Teto.asset` 显式设为 8

### 暂缓（保守方案）
- 武器槽下放到玩家（`PlayerWeaponController` + 按 owner 注册表）
- 塔 `OwnerId`/`team` 预留

## 2026-09-21 — 清理回退代码 + Bug 修复

### 移除回退（保持单一访问路径）
- `ManagerSingleton`：删除 `Instance` 的 `FindFirstObjectByType` 懒查找与错误日志、删除 `FindInstance()`；`Instance` 仅返回当前缓存实例
- 各 `Service` 静态属性（`IPlayerManager`/`IGameLevelManager`/`IWeaponManager`/`ITowerManager`/`ISOManager`/`IExperienceController`/`IDamageNumService`）改为纯 `ServiceLocator.TryGet`，无实例时返回 null
- `DamageNumManager`/`ExperienceLevController` 删除 `Instance` 与 `_instance` 字段
- 以 `[DefaultExecutionOrder]` 保证 Manager 先于业务脚本初始化：`InputReaderManager -150`、`SOManager -140`、`PlayerManager -130`、`GameLevelManager`/`ExperienceLevController -120`、`WeaponManager`/`TowerManager -110`、`DamageNumManager -100`

### Bug 修复
- `BaseTower.ForEachValidTarget` 改为**快照遍历**：修复 `Rin` 范围攻击击杀敌人时触发 `OnTriggerExit2D` 修改同一列表导致的 `ArgumentOutOfRangeException`
- 新增 `EPress` 输入动作（`<Keyboard>/e`）到 `InputSystem_Actions.inputactions` 并重新生成包装类，修复「靠近塔按 E 无反应」（此前 Player map 只有 Move/EscapePress，E 交互整条链从未接入）
- 新增 `Assets/Editor/InputActionsSetup.cs`：强制重导入 `.inputactions` 以重生成包装类

## 2026-09-21 — 项目重组（阶段 3）：UI 全量重构

### IUIService / UIManager
- `IUIService` 新增 `OnEscapeUnhandled` 事件与 `HandleEscape()`
- `UIManager` 维护面板显示栈；`ShowPanel` 置顶（`SetAsLastSibling`）；`HidePanel` 立即出栈
- ESC 统一由 `UIManager` 分发：从栈顶向下找第一个 `CanHandleEscape` 的面板执行 `EscLogic`，无人处理则触发 `OnEscapeUnhandled`
- 预加载改为**反射收集全部 `BasePanel` 子类**，新增面板零改动

### BasePanel
- 移除隐式 `InputHandleFactory.GetInput("local")` 与逐面板 `OnEscape` 订阅
- 新增 `CanHandleEscape`（HUD 为 false，弹窗为 true）
- 淡出回调触发一次后置空，避免重复执行

### HUD 事件驱动
- 新增 `Assets/Script/UI/PlayerHudBinder.cs`：订阅 `LocalPlayer` 的 `IExperienceController` 事件刷新 `GamePanel`
- `ExperienceLevController` 删除 `SyncUI()`，核心只发事件，UI 由绑定器更新

### ESC 行为
- `GameLevelManager` 不再直接订阅输入，改订阅 `IUIService.OnEscapeUnhandled` 打开暂停面板
- `GameSettingPanel`/`MusicSettingPanel` 新增 `EscLogic`（关闭并恢复）
- `ChooseTowerPanel`/`LevelUpPanel`/`TowerLevelUpPanel`/`PausePanel` 标记 `CanHandleEscape`
- `InputHandleFactory` Android 缺摇杆由 `LogError` 降为 `LogWarning`（菜单场景正常情况）

### 关键文件
```
Assets/Script/Core/IInterface/IUIService.cs   ← 扩展 ESC API
Assets/Script/UI/UIManager.cs                 ← 面板栈 + ESC + 反射预加载
Assets/Script/UI/BasePanel.cs                 ← 去输入依赖 + CanHandleEscape
Assets/Script/UI/PlayerHudBinder.cs           ← 新增
Assets/Script/UI/GamePanel/*.cs               ← 迁移到 UIManager.Service + ESC
Assets/Script/Core/Level/ExperienceLevController.cs ← 删除 SyncUI
Assets/Script/Manager/GameLevelManager.cs     ← ESC 改订阅 OnEscapeUnhandled
```

### 运行期修复（阶段 3 验证中发现）
- **`ManagerSingleton.FindInstance()`**：`Service` 回退改为「缓存实例 → 静默查找场景实例」。此前的 `HasInstance ? Instance : null` 在场景加载时若实体 `OnEnable` 早于 Manager `Awake`，会返回 null 导致注册被跳过——进而 `PlayerManager.LocalPlayer` 恒为空，连锁导致怪物不生成、武器不工作、HUD 不更新。
- **`GameBootstrap`**：改为每个服务独立 `SafeInit`，任一服务初始化失败不再阻塞其余服务。
- **`AudioService`**：逐条音频加载单独 try/catch，单条失败不影响其余。

## 2026-09-21 — 项目重组（阶段 4）：音频接口化

> 为减少面板二次改动，音频阶段提前到 UI 重写之前完成。

### 新增
- `Assets/Script/Core/Services/AudioService.cs`：实现 `IAudioService`。BGM 使用独立循环 `AudioSource`；SFX 使用 8 路 `AudioSource` 对象池 + `PlayOneShot`，取代原先「每个音效 `new GameObject` 1 秒后 `Destroy`」。支持 `BgmMuted`/`SfxEnabled`/`BgmVolume`/`SfxVolume`，并提供 `AudioService.Service` 访问入口。
- `GameBootstrap` 创建并注册 `IAudioService`。

### 迁移与删除
- 全部 38 处 `BKMusic` 调用点迁移到 `IAudioService`（HealthController、Teto/Rin/Luo、Spin/Gun 武器、经验球、各 UI 面板）
- `GameSettingPanel`/`MusicSettingPanel` 不再直接操作 `AudioSource`
- 删除 `Assets/Script/Manager/BKMusic.cs`

### 关键文件
```
Assets/Script/Core/Services/AudioService.cs   ← 新增
Assets/Script/Core/GameBootstrap.cs           ← 注册 IAudioService
Assets/Script/Manager/BKMusic.cs              ← 删除
```

## 2026-09-21 — 项目重组（阶段 2）：Manager 接口化

### 新增接口 + 实现
- `ISOManager` ← `SOManager`（注册进 `ServiceLocator`，`Service` 静态回退）
- `IWeaponManager` ← `WeaponManager`（新增 `WeaponSlots`/`Weapons` 只读属性）
- `ITowerManager` ← `TowerManager`（新增 `Towers` 只读属性）
- `IDamageNumService` ← `DamageNumManager`（注册提前到 `Awake`）
- `IUIService` ← `UIManager`（组合根注册）

### 调用点迁移（`.Instance` → 接口 `Service`）
- 玩法侧：`EntityBehaviour`、`BaseTower`、`BaseWeapon`、`EnemyTargetFinder`、各 `*HealthController`、`DamageNumText`
- UI/关卡侧：`GameLevelManager`、`DetectPlayer`、`TowerPlacementController`、`ExperienceLevController`、`Main`、`LevelUpPanel`、`TowerLevelUpPanel`、`ChooseWeaponPanel`、`GamePanel`、`SOManager`

### 延后项（已记录于 Docs/Plan.md）
- `IAudioService`/`AudioService` 实现与音频调用点 → 阶段 4（与面板音频一起）
- 面板内 `UIManager.Instance` → 阶段 3 UI 重写
- `IPlayerManager.GetPlayer(int)` → 阶段 5（需先有玩家网络 id）

### 运行期修复（阶段 2 验证中发现）
- 返回菜单时 `[InputReaderManager] 重复实例` 会销毁整个 `Start` 物体（连带 `Main`）导致菜单不显示 → `Assets/Editor/PrefabCleanup.cs` 从 `Start.prefab` 移除 `InputReaderManager`，统一由 `InputHandleFactory` 按需自建
- `TowerHealthPanel.UpdateHealthUI` 初始化时序 NRE → 缓存移到 `Awake` 并加空守卫
- `TowerPlacementController.Update` 在 `Init` 之前运行导致 `_inputHandle` 为空的 NRE → `Update` 顶部空守卫
- 塔预制体漏配 `EntityType`（默认 `Player=0`），导致塔读取玩家配置、报 `缺少Type：TowerAttackRange` / `operate interval <= 0` → 新增 `Assets/Editor/EntityPrefabSetup.cs` 幂等写入 `TowerTeto=4/TowerRin=5/TowerLuo=6`

### 关键文件
```
Assets/Script/Core/IInterface/ISOManager.cs        ← 新增
Assets/Script/Core/IInterface/IWeaponManager.cs    ← 新增
Assets/Script/Core/IInterface/ITowerManager.cs     ← 新增
Assets/Script/Core/IInterface/IDamageNumService.cs ← 新增
Assets/Script/Core/IInterface/IUIService.cs        ← 新增
Assets/Script/Core/IInterface/IAudioService.cs     ← 新增（待阶段 4 实现）
Assets/Script/Manager/{SOManager,WeaponManager,TowerManager}.cs  ← 实现接口 + 注册
Assets/Script/UI/{UIManager,DamageNumManager}.cs   ← 实现接口
```

## 2026-09-21 — 项目重组（阶段 1）：组合根 + Addressables + 地址常量

### 修复既有编译错误（工作树此前无法编译）
- 6 处 `(await handle.Task).Result` 误用（`Task<T>` await 后已是结果）→ `await handle.Task`
- `AssetService.InitializeAsync` 的 `Addressables.ResourceLocators.Count` 缺 `using System.Linq` → `.Count()`
- `UIManager` 预加载 `TowerHealthPanel`（非 `BasePanel`）导致泛型约束失败 → 移除该预加载

### 组合根（Composition Root）
- 新增 `Assets/Script/Core/GameBootstrap.cs`：`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 自建，统一初始化并注册全局服务；暴露 `GameBootstrap.Ready`
- `Main.cs` / `GameLevelManager.Start()` 删除重复的 `AssetService`/`UIManager`/`BKMusic` 引导，改为 `await GameBootstrap.Ready`
- `InputReaderManager` 运行时自建 `InputReader`（`ScriptableObject.CreateInstance`），PC 平台在缺失时由 `InputHandleFactory` 按需创建，解决 Windows 输入不可用且不覆盖场景内已有实例
- `ServiceLocator` 新增 `IsRegistered<T>()`、`Clear()`

### Addressables
- 新增 `Assets/Editor/AddressablesSetup.cs`：幂等生成 `AddressableAssetsData` 与 UI/Common/Weapon/Music 分组和地址（配合 `AssetKeys`）
- 新增 `Assets/Script/Core/AssetKeys.cs`：集中所有资源地址，替换硬编码字符串
- 修复 `ResourceEnum.Walk` → `PlayerMove`（原先无对应资源，音频初始化会失败）

### 关键文件
```
Assets/Script/Core/GameBootstrap.cs          ← 新增
Assets/Script/Core/AssetKeys.cs              ← 新增
Assets/Editor/AddressablesSetup.cs           ← 新增
Assets/Script/Core/ServiceLocator.cs         ← IsRegistered / Clear
Assets/Script/Core/Main.cs                   ← 委托 Bootstrap
Assets/Script/Manager/GameLevelManager.cs    ← 去重复引导
Assets/Script/Manager/InputReaderManager.cs  ← 自建 InputReader
Assets/Script/Util/Event/EventEnum.cs        ← Walk → PlayerMove
Docs/Plan.md                                 ← 新增（计划源）
```

## 2026-06-10 — ServiceLocator + Manager 接口化

### ServiceLocator 基础设施
- 新建 `ServiceLocator.cs` — 全局服务注册表，`Register<T>` / `Get<T>` / `TryGet<T>` / `Unregister<T>`
- 为所有 Manager 提供联机替换扩展点：单机注册本地实现，联机注册网络实现

### PlayerManager 接口化
- 新建 `IPlayerManager` 接口：`LocalPlayer`、`AllPlayers`、`Register`/`Unregister`
- `PlayerManager` 实现 `IPlayerManager`，新增 `AllPlayers` 列表支持多玩家
- 新增 `Service` 静态属性（ServiceLocator 优先，Instance 回退）
- `EnemyController.FindTarget()` 从单玩家改为遍历 `AllPlayers` 找最近目标（联机兼容）

### 输入系统扩展
- `InputHandleFactory.GetInput(string inputId)` 替代 `GetLocalInput()`，支持按 ID 缓存和释放
- 预留 `network_` / `ai_` 前缀扩展点
- `PlayerController` / `GunWeapon` / `PlayerAnimationController` / `PlayerInteraction` 新增 `_inputHandleId` 字段
- `TowerPlacementController.Init()` 支持外部注入 `IInputHandle`

### GameLevelManager 接口化
- 新建 `IGameLevelManager` 接口：关卡时间、波次、暂停、敌人注册、GameOver 事件
- `GameLevelManager` 实现接口，新增 `Service` 静态属性
- 新增 `OnGameOver`、`OnGameTimeUpdate` 事件，便于联机状态同步
- 所有调用方 `GameLevelManager.Instance` → `GameLevelManager.Service`（12 个文件）

### 玩家经验模块接口化 + 职责拆分
- 新建 `IExperienceController` 接口：`CurrentLevel`、`CurrentExp`、`AvailablePoints`、`ExpToNextLevel`、事件
- `ExperienceLevController` 实现接口，新增 `Service` 静态属性
- **`ProcessLevelUps()`**：`if` 改为 `while`，支持一次大量经验连续升级
- **`CanUseLevelPoint`** → 职责拆分：核心方法只处理状态和触发事件，UI/音效通过 `SubscribeDefaultPresentation` 事件订阅处理
- 提取 `SyncUI()` 统一刷新 `GamePanel`，消除分散在多个方法中的重复 UI 调用
- `PlayerController` 新增 `_experienceController` 字段 + `ExperienceController` 属性（优先自身组件，回退全局 Service）
- `ExpSpriteController` 碰撞时优先给碰撞到的玩家自身加经验（联机兼容）

### 关键文件
```
Assets/Script/Core/ServiceLocator.cs                          ← 新增
Assets/Script/Core/IPlayerManager.cs                          ← 新增
Assets/Script/Core/IGameLevelManager.cs                       ← 新增
Assets/Script/Core/IInterface/IExperienceController.cs        ← 新增
Assets/Script/Manager/PlayerManager.cs                        ← 实现 IPlayerManager + Service
Assets/Script/Manager/GameLevelManager.cs                     ← 实现 IGameLevelManager + Service
Assets/Script/Core/Level/ExperienceLevController.cs           ← 实现 IExperienceController + 职责拆分
Assets/Script/Entity/Player/PlayerController.cs               ← _inputHandleId + ExperienceController
Assets/Script/InputSystem/InputHandleFactory.cs               ← GetInput(string) + 缓存
Assets/Script/Entity/Enemy/EnemyController.cs                 ← FindTarget 遍历 AllPlayers
Assets/Script/Core/Level/ExpSpriteController.cs               ← 联机兼容加经验
```

---

## 2026-06-09 — 交互系统重构 + 武器/塔系统重构

### 交互系统重构
- `PlayerController` 只负责移动；**新增 `PlayerInteraction`** 组件独立管理交互
- `IInteractable` 扩展 `OnSelected()` / `OnDeselected()` 回调，解决多塔同时显示提示的问题
- `DetectPlayer` 不再直接操作玩家字段，改为调用 `PlayerInteraction.Register/Unregister`

### 防御塔高亮 (Shader)
- `BaseTower.SetHighlight(bool)` 切换高亮材质，支持多 SpriteRenderer 复合结构
- `SOManager.towerHighlightMaterial` 提供统一材质配置，也可在单个塔 Prefab 上覆盖
- **新增 Shader**: `sg_HighLight2D.shadergraph`（Renderer2D 下的 Sprite Outline 发光效果）
- **新增材质**: `mat_HightLight.mat`

### 武器系统重构（DataSO 拆分 + 泛化 Manager + 职责解耦）

**核心改动**:
- `WeaponDataSO` 改为 abstract，专属字段拆分到子类：`SpinWeaponDataSO` / `GunWeaponDataSO`
- 新增 `WeaponSelectSO` — 武器选择专用 SO，与 `LevelUpSO`（数值升级）彻底解耦
- `WeaponManager` 泛化 — `List<WeaponSlot>` 替代硬编码字段，新增武器零代码修改
- `ChooseWeaponPanel` 从 `WeaponManager.weaponSlots` 动态读取未激活武器
- `SOManager` 升级池标签化 — `LevelUpSO.targetTags` + `BaseWeapon.weaponTags` 按标签过滤，消除 `is` 类型判断
- `BaseHealthController` 增加 `BaseMaxHealth` 变化时的 CurrentHealth 补偿逻辑
- `FireBallController` 改名为 `SpinWeaponController`，伤害通过 `Init()` 传入（不再硬编码）

**DataSO 拆分**:
```
WeaponDataSO (abstract) — AttackInterval, projectilePrefab
├── SpinWeaponDataSO — RotationSpeed, Size, LifeTime, HitPushForce
└── GunWeaponDataSO — BulletSpeed, BulletHitForce
```

**EntityDataRegistry（统一配置表）**:
- 新建 `EntityDataRegistry` SO — 集中存放所有 `entityId → DataSO` 映射
- `EntityBehaviour` 支持 `_registryId` 自动从 Registry 查找 DataSO
- 解决 DataSO 分散在多个 Prefab/Scene Inspector 中难以管理的问题

### 关键文件
```
Assets/Script/Entity/Player/PlayerInteraction.cs            ← 新增
Assets/Script/Entity/Player/PlayerController.cs             ← 简化（删除交互逻辑）
Assets/Script/Core/IInteractable.cs                         ← 扩展接口
Assets/Script/Entity/Tower/BaseTower.cs                     ← 新增 SetHighlight
Assets/Script/Entity/Tower/DetectPlayer.cs                  ← 调用 SetHighlight

// 武器系统重构
Assets/Script/Core/EDM/Data/WeaponDataSO.cs                 ← 改为 abstract
Assets/Script/Core/EDM/Data/SpinWeaponDataSO.cs             ← 新增
Assets/Script/Core/EDM/Data/GunWeaponDataSO.cs              ← 新增
Assets/Script/SO/WeaponSelectSO.cs                          ← 新增
Assets/Script/Manager/WeaponManager.cs                      ← List<WeaponSlot> 泛化
Assets/Script/Manager/SOManager.cs                          ← 标签过滤 + EntityDataRegistry
Assets/Script/SO/EntityDataRegistry.cs                      ← 新增
Assets/Script/Entity/Player/Weapons/BaseWeapon.cs           ← weaponTags + GetAttackInterval/GetBaseDamage
Assets/Script/Entity/Player/Weapons/FireBall/SpinWeaponController.cs   ← 改名 + Init 传入 damage

// 塔 DataSO 拆分
Assets/Script/Core/EDM/Data/TowerDataSO.cs                  ← 删除 Luo 专属字段
Assets/Script/Core/EDM/Data/LuoTowerDataSO.cs               ← 新增

// EDM 核心
Assets/Script/Core/EDM/EntityBehaviour.cs                   ← _registryId + Registry 自动查找
```
