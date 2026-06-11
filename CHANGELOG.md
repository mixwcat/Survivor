# Changelog

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
