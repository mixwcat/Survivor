# CLAUDE.md

本文件只写**项目约定与红线**（读代码推不出来的部分）。
「某个类现在怎么实现」请直接读代码；这里不重复描述，避免文档与实现分叉。

## 项目事实

- Unity **6000.0.44f1**，C# 9（.NET Standard 2.1），2D 俯视 survivor-like
- 目标平台 **Android**（触屏 + Joystick Pack），同时兼容 Windows（键鼠）
- 场景：`Assets/Scenes/Menu.unity`（主菜单）、`Assets/Scenes/Lobby.unity`（大厅：选角/武器台/传送门）、`Assets/Scenes/Level0.unity`（战斗）
  —— 三个都在 Build Settings 里；场景名常量集中在 `Core/SceneFlow.cs`
- **无自动化测试**；`*.sln`/`*.csproj` 由 Unity 生成，不要手改
- ⚠️ **Domain Reload 实际是开启的**（更正于 2026-10）：`ProjectSettings/EditorSettings.asset` 里是
  `m_EnterPlayModeOptionsEnabled: 1` + `m_EnterPlayModeOptions: 0` —— 选项虽启用但**没有任何 flag**，
  即域重载与场景重载都照常发生（日志里能看到 `Reloading assemblies for play mode` 与 `domain reloads=1`）。
  本文件此前写的"关闭了 Domain Reload、静态状态跨 Play 存活"与事实不符。
  影响：不要依赖"静态状态能跨 Play 会话存活"来省事；`GameBootstrap.ResetStatics()` 之类的防御
  每次进 Play 都会跑（`RuntimeInitializeOnLoadMethod` 与域重载无关），保留即可。
  **如果有脚本想在进入 Play 后继续持有状态（例如编辑器侧的状态机），它会被域重载清掉** ——
  这类状态要放 `SessionState`（跨域重载存活）或放进运行时程序集里的 `MonoBehaviour`。

## 联机（Mirror）

**动联机代码前先读 `Docs/Mirror/README.md`**（索引 + 12 条铁律），进度表在 `Docs/MirrorPlan.md`。

- Mirror **96.11.3**，vendored 在 `Assets/Mirror/`（不是 UPM 包，可直接读/改源码）；`Mirror.asmdef` 是 `autoReferenced`，
  业务代码在 `Assembly-CSharp`，可直接 `using Mirror`。
- **API 签名一律查 `Docs/Mirror/02-API速查.md`** —— 官方文档有多处与 96.11.3 源码不符
  （`NetworkSceneManager` / `[SyncObject]` / `NetworkServer.SendToClient` / `Channels.UnreliableSequenced` /
  `NetworkServer.GetSpawnedObjects` 等**都不存在**）。该文件的「源码中不存在的 API 清单」是权威。
- 本项目专属红线（未激活 prefab 与 `Spawn` 的强制激活、Addressables 与 `spawnPrefabs`、对象池冲突、
  Host 下静态状态共享、暂停与 `timeScale`……）见 `Docs/Mirror/03-项目落地注意.md`。
- ⚠️ **vendored 本地补丁（已打，升级 Mirror 后要重贴）**：`Assets/Mirror/Core/NetworkConnection.cs` 里
  `Time.time` 的 `try/catch` 与 `IsAlive` 的"时间戳为 0 视为存活"守卫。
  台账见 `Docs/Mirror/local-patches.md`；`grep -rn "本地补丁" Assets/Mirror/` 可一次查全。
  **除这条之外不要改 `Assets/Mirror/` 下的任何文件。**

## 开发工作流

- 改完代码必须验证：批处理编译
  `Unity.exe -batchmode -nographics -quit -projectPath <proj> -logFile <log>`
  然后检查日志里的 `error CS` 与 `Tundra build success`。
  项目里已有封装脚本：`& Tools\compile-check.ps1 -LogName compile.log`（见 `Tools/README.md`）。
- 联机改动的**端到端冒烟**（两个层次，改完联机代码至少跑第一个）：
  - `& Tools\run-network-smoke.ps1` —— Host 单进程，几十秒。覆盖**服务端**那一半。
  - `& Tools\run-network-2p.ps1` —— **双进程**（本仓库当服务端 + 镜像副本当客户端），几分钟。
    ⭐ **只有它能验证客户端侧路径**：Host 里服务端与客户端是同一个对象，
    所有 `ApplyNetwork*` 都会因权威守卫提前返回 —— 已经因此漏过一个真 bug
    （`NetworkAuthority` 对场景对象恒为 true，整批客户端守卫失效而 Host 测试全绿）。
  两者都**不能**替代人 Play（输入、相机、画面仍需人工确认）。
- ⚠️ **写"客户端侧"的断言时，要挑客户端不可能自己产生的判据**：
  "推车在动 / 时钟在走"在权威判据写错时**照样会通过**（客户端自己推进）。
  双进程测试用的是哨兵值 + 只在"广播真的被应用"时才增长的计数器，理由见 `Docs/MirrorPlan.md`。
- 📚 **「怎么让 Unity 无头地跑起来做验证」的完整方法（进 Play、双进程、域重载陷阱、
  PowerShell 启动方式）在 skill `.claude/skills/unity-batch-autoconfig/SKILL.md` 的
  「无头 Play 模式验证」一节** —— 动批处理/测试编排之前先读它。
- ⚠️ **编辑器开着时批处理会因工程锁直接崩溃**（报 "another Unity instance is running"）——这是环境问题不是代码问题。跑之前先检查 Unity 进程。
- 批处理**能进 Play 跑运行时断言**（见上面的冒烟脚本），所以"服务端逻辑对不对"通常不用等人。
  但**表现层**（输入手感、相机、画面、UI 观感）与**长流程**（跑完一整局到胜负结算）
  仍必须由人 Play 确认 —— 不要声称"功能已验证"。
- **脚本化接线场景对象时，用 public 字段直接赋值，不要走 `SerializedObject`**：实测在**场景里的组件**上，
  `AssetReference` 的内联字符串（`m_AssetGUID`）能写进去，但**对象引用**（单个引用与数组元素）写不进去 ——
  `arraySize` 落盘了、元素仍是 `null`，加 `EditorUtility.SetDirty` 也无效。
  改成 public 字段 + 直接赋值后一次通过。（prefab 资产上用 `PrefabUtility.LoadPrefabContents` + `SerializedObject` 是可靠的。）
- DOTween 若提示未 Setup：跑一次 `Tools ▸ Demigiant ▸ DOTween Utility Panel ▸ Setup DOTween`。

## 命名与文件

- 私有字段 `_camelCase`；公开/序列化字段与方法 `PascalCase`
- 游戏代码**不使用 namespace**（第三方 `LitJson` 除外）
- ScriptableObject 资产类名以 `SO` 结尾
- 用 `FindFirstObjectByType<T>()`，不用废弃的 `FindObjectOfType<T>()`
- **`.meta` 随文件一起移动**（重命名必须保留 GUID，否则场景/prefab 引用断裂）
- **实体 id 清单是生成物**：`Assets/EntityIdCatalog.csv` —— 所有带稳定 `id` 的资产（玩家/敌人/塔/武器/推车/角色）一行一个：
  `Class,Id,DisplayName,AssetName,AssetPath,DataRef,PrefabGuid,UpgradeCount`。
  **不要手改**（会与资产分叉），用 `Tools ▸ Export Entity Id Catalog` 重新生成
  （批处理 `-executeMethod EntityIdCatalogExporter.ExportFromCommandLine`；扫描口径与 `EntitySOValidator` 一致，都是 `t:BaseEntitySO`）。
- 接口放 `Assets/Script/Core/IInterface/`，服务实现放 `Assets/Script/Core/Services/`

## 服务与 Manager

### 全局服务（`IAssetService` / `IAudioService` / `IUIService`）

| 约定 | 说明 |
|---|---|
| 命名 | 接口 `IXxxService` ↔ 实现 `XxxService` |
| 形态 | `MonoBehaviour`，由组合根 `GameBootstrap` 创建并挂在 `[GameBootstrap]` 上 |
| 访问 | `XxxService.Service?.…`（纯 `ServiceLocator.TryGet`，未注册返回 null） |
| 禁止 | **不暴露 `Instance`**、不自建单例、不做场景查找回退 |
| 初始化 | `InitializeAsync()` 幂等 + 并发安全（`_initTask ??= …`），失败可重试 |
| 生命周期 | 组合根独占「创建 + 注册 + 初始化」；服务自身在 `OnDestroy` 注销并释放句柄 |

- 场景级服务（如 `DamageNumService`）不由组合根创建，留在各自领域目录，但同样遵循上表。
- **禁止** `ServiceLocator.Get<T>()`（未注册会抛异常，且绕过了统一判空约定）。
- 需要「服务就绪」的场景脚本：`await GameBootstrap.Ready`。

### 场景内 Manager

- 统一继承 `ManagerSingleton<T>`，初始化写 `OnSingletonAwake()`（不要重写 `Awake`）。
- **`ManagerSingleton<T>` 不暴露 `Instance`**——业务代码一律走各自的 `Service` 静态属性，避免出现第二条访问路径。需要跨场景保留时重写 `PersistAcrossScenes => true`。
- 核心 Manager 用 `[DefaultExecutionOrder(负值)]` 保证先于业务脚本初始化。
- 需要「按玩家隔离」的状态（升级选项、武器槽、经验/等级）**挂在 Player 上**，不要放全局单份——否则联机下玩家之间会互相覆盖。

## 资源与句柄

- 资源一律走 Addressables，**禁止硬编码地址字符串**，统一引用 `AssetKeys`。
- 但「走 Addressables」不等于「走地址字符串」：投射物与武器 prefab 用 `AssetReferenceGameObject`（GUID）引用。⚠️ AssetReference **同样要求目标在 Addressables 组里**，否则运行时加载失败 —— `AssetKeys` 里因此没有 Bullet/Spin 常量，但那几条组内条目**不能删**。
- **获取与归还必须成对**，失败分支同样要归还：
  - `LoadAssetAsync` 的句柄由**调用方** `Release`
  - `InstantiateAsync` 的实例必须用 `IAssetService.ReleaseInstance` 归还——**直接 `Destroy` 不会递减引用计数**
  - `ReleaseInstance` 返回 `false` 表示该对象未被跟踪、不会被销毁，此时才需要自己 `Destroy` 兜底
- 随场景销毁的对象（如放置幽灵）要在 `OnDestroy` 补一次归还，覆盖「切场景」这条外部销毁路径。
- 句柄泄漏只在真机包暴露，编辑器下（Asset Database 模式）看不出来——不要因为"编辑器没问题"就放过。

## 性能红线（Android 逐帧路径）

- **`EntityStatModel.GetStat` 是逐帧热路径**：内部禁止 LINQ / 装箱 / 任何堆分配。`EntityBehaviour.GetStat` 的告警必须**只提示一次**，否则配置缺失会退化成每帧一次 `LogWarning` + 字符串拼接。
- **材质一律用 `sharedMaterial`**：访问 `Renderer.material` 会为每个渲染器克隆一份材质实例（泄漏 + 合批失效）。`SpriteRenderer.color` 是顶点色，不涉及材质实例。
- **不要每帧给 TMP 文本赋值**（触发整套字形网格重建）：时钟/帧率要节流；伤害数字的淡出走 `CanvasGroup.alpha` 而不是 `TMP.alpha`。
- **避免逐帧写 `Transform`**：朝向/缩放没变就别写；距离比较用 `sqrMagnitude`。
- 逐帧路径上的告警、日志、字符串插值一律要加「只提示一次」标记。
- 别每帧 `GetComponent` / `Camera.main` / `FindXxx`；循环里不要用 `foreach` 遍历接口类型的集合（`IReadOnlyList<T>` 会装箱枚举器，用 `for` + 索引器）。

## 架构原则（按优先级）

1. **先架构，后编码**：涉及多文件/模块边界的设计变更，先把方案讲清楚再动手。
2. **接口化优先**：新增 Manager/Controller 先定义接口再实现，接口放 `Core/IInterface/`。
3. **联机兼容作为默认假设**：默认多玩家；按玩家持有的状态挂 Player；玩家列表遍历 `AllPlayers` 而不是只取 `LocalPlayer`。
4. **职责拆分**：核心状态变更只改数据 + 发强类型事件；UI/音效由订阅者处理。**不用字符串/枚举 key 的全局事件总线**。
5. **YAGNI**：没有第二个使用者的抽象/工厂/管理器/预加载不要引入；死代码直接删（git 记得住）。
6. **初始化时序防御**：不依赖 `Awake`/`Start` 顺序，用负 `DefaultExecutionOrder` + 懒加载兜底。
7. **按"局内选择"变化的配置由装配方注入，不要预接在 prefab 上**：职业数值就是这类（一个 Player prefab 服务多个职业）。
   因此 Player prefab 的**根节点在资产里是未激活的**、`entityConfig` 留空 —— `PlayerSpawner` 在 `Instantiate` 之后、
   **激活之前**注入角色的 `playerConfig`。顺序不能反：先激活的话 `Awake` 已经按"没有配置"失败过一次，
   注入会被 `EntityBehaviour.SetEntityConfig` 拒绝（血量上限/移速会停在 1f 兜底值）。
   往场景里手动拖 Player prefab 时要记得它是未激活的。
