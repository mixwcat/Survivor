# Instance 单例重构推广清单

> 目标：将所有全局 `XXXManager.Instance` 逐步迁移到「接口抽象 + ServiceLocator 注册」模式，为联机功能预留扩展点。
> 
> 原则：**渐进式重构**，每次只改一个 Manager，保证编译通过、功能无损。

---

## 一、优先级总览

| 优先级 | Manager | 联机影响 | 重构方式 | 预估工作量 |
|:---:|:---|:---|:---|:---:|
| 🔴 P0 | PlayerManager | **必须改**（单玩家→多玩家） | 提取 `IPlayerManager` + 多玩家字典 | 中 |
| 🔴 P0 | InputReaderManager | **必须改**（需支持网络输入） | 已有 `IInputHandle` 接口，扩展工厂即可 | 小 |
| 🔴 P0 | GameLevelManager | **必须改**（需区分权威/预测状态） | 提取 `IGameLevelManager` + 状态机拆分 | 大 |
| 🟡 P1 | ExperienceLevController | 每个玩家独立经验 | 从全局单例改为按玩家实例 | 中 |
| 🟡 P1 | WeaponManager | 武器槽属于玩家 | 提取 `IWeaponManager`，支持按 owner 查询 | 中 |
| 🟡 P1 | TowerManager | 塔是共享还是阵营？ | 提取 `ITowerManager`，预留 `TeamId` 字段 | 小 |
| 🟡 P1 | TowerPlacementController | 输入来源需可替换 | 注入 `IInputHandle` 而非内部创建 | 小 |
| 🟢 P2 | UIManager | 本地显示层，大概率保留 | 提取 `IUIManager` 接口（便于测试） | 小 |
| 🟢 P2 | DamageNumManager | 纯本地视觉效果 | 提取接口，联机模式下可空实现 | 小 |
| 🟢 P2 | SOManager | 只读配置，所有客户端共享 | 提取 `ISOManager`（便于Mock测试） | 小 |
| 🟢 P2 | BKMusic | 本地音频 | 保持单例，仅提取接口 | 极小 |
| 🟢 P2 | JsonMgr | 本地存档，联机改用服务器 | 保持单例，联机时注册 `NetworkSaveManager` | 极小 |
| — | ExpSpritePool | 嵌套单例，依赖 ExperienceLevController | 随 ExperienceLevController 一起重构 | 中 |

---

## 二、详细重构方案

### 🔴 P0 - PlayerManager

**现状问题**：
```csharp
// 全局只缓存一个玩家
public static PlayerManager Instance;
public PlayerController Player { get; private set; }
```

**联机冲突**：联机模式下有 N 个玩家，无法只存一个 `Player`。

**目标接口**：
```csharp
public interface IPlayerManager {
    /// <summary>本地玩家（用于本地输入、摄像机跟随）</summary>
    PlayerController LocalPlayer { get; }
    
    /// <summary>所有玩家（包括本地和远程）</summary>
    IReadOnlyList<PlayerController> AllPlayers { get; }
    
    /// <summary>按网络ID查找玩家</summary>
    PlayerController GetPlayer(int playerId);
    
    /// <summary>注册/注销</summary>
    void Register(PlayerController player);
    void Unregister(PlayerController player);
}
```

**实现策略**：
- 单机模式：`LocalPlayer` 就是唯一玩家，`AllPlayers` 只包含一个元素
- 联机模式：`LocalPlayer` 是本地，`AllPlayers` 包含所有客户端同步过来的玩家实例

**待修改引用点**：
- [ ] `BaseTower`（查找攻击目标时默认打 PlayerManager.Instance.Player）
- [ ] `PlayerInteraction`（交互系统，需明确是本地玩家还是所有玩家）
- [ ] `DetectPlayer`（塔的 DetectPlayer 触发器）
- [ ] `EnemyController`（敌人追踪目标）
- [ ] `GameLevelManager`（游戏结束判断）

---

### 🔴 P0 - InputReaderManager / InputHandleFactory

**现状问题**：
```csharp
// InputHandleFactory.cs
public static IInputHandle GetLocalInput() { ... }
```

**联机冲突**：只支持 `"Local"`，联机需要 `"Network_Player2"` 这样的输入源。

**目标接口**（已有 `IInputHandle`，只需扩展工厂）：
```csharp
public static class InputHandleFactory {
    public static IInputHandle GetLocalInput() => ...;
    
    // 新增：按玩家ID获取输入（联机用）
    public static IInputHandle GetInput(string inputId) {
        if (inputId == "local") return GetLocalInput();
        return new NetworkInputHandle(inputId); // 从网络同步读取
    }
}
```

**待修改引用点**：
- [ ] `PlayerController.Awake()`（当前调用 `GetLocalInput()`，改为可配置 `_inputHandleId`）
- [ ] `TowerPlacementController`（放置塔时的输入源）

---

### 🔴 P0 - GameLevelManager

**现状问题**：管理全局游戏状态（时间、暂停、敌人生成、游戏结束）。

**联机冲突**：
- 谁有权调用 `GameOver()`？应该是服务器。
- `Time.timeScale = 0` 暂停会影响所有玩家，联机下通常不支持全局暂停。

**目标接口**：
```csharp
public interface IGameLevelManager {
    float GameTime { get; }
    bool IsPaused { get; }
    bool IsGameOver { get; }
    
    event System.Action OnGameOver;
    event System.Action<float> OnGameTimeUpdate;
    
    void Pause();   // 单机生效，联机可能空实现
    void Resume();
    void GameOver();
}
```

**联机实现差异**：
- 单机：`GameOver()` 直接执行
- 联机：`GameOver()` 发送 RPC 到服务器，由服务器权威决定

**待修改引用点**：
- [ ] `PlayerController`（死亡时调用 GameOver）
- [ ] `EnemyController`（敌人生成、波次）
- [ ] `ExperienceLevController`（升级时机）
- [ ] `BaseTower`（塔的攻击计时）
- [ ] `BaseWeapon`（武器的攻击计时）
- [ ] `UIManager` / `GamePanel`（暂停按钮）

---

### 🟡 P1 - ExperienceLevController

**现状问题**：全局单例，管理一套经验/等级/技能点。

**联机冲突**：每个玩家独立升级。

**重构策略**：
1. 提取 `IExperienceController` 接口
2. 将 `ExperienceLevController` 从单例改为 **每个玩家挂载一个实例**
3. `PlayerController` 持有自己的 `IExperienceController`

```csharp
public interface IExperienceController {
    int Level { get; }
    int Experience { get; }
    int AvailablePoints { get; }
    
    void AddExperience(int amount);
    event System.Action<int> OnLevelUp; // 参数：新等级
}
```

**待修改引用点**：
- [ ] `PlayerController`（改为自身组件引用）
- [ ] `LevelUpPanel`（显示升级选项时传入目标玩家）
- [ ] `ExpSprite`（经验球拾取时给哪个玩家加经验？需要知道是谁捡的）

---

### 🟡 P1 - WeaponManager

**现状问题**：
```csharp
public class WeaponSlot {
    public string weaponId;
    public GameObject weaponRoot;      // 场景中的挂载点
    public WeaponSelectSO weaponSelectSO;
}
public List<WeaponSlot> weaponSlots;
```

**联机冲突**：武器槽是全局的，但每个玩家应该有独立的武器。

**重构策略**：
1. 提取 `IWeaponManager` 接口
2. 将 `weaponSlots` 和激活逻辑下放到 **PlayerController** 或独立的 `PlayerWeaponController`
3. `WeaponManager` 变为轻量级注册表，或按 `ownerId` 索引

```csharp
public interface IWeaponManager {
    IReadOnlyList<BaseWeapon> GetWeapons(int ownerId);
    void ActivateWeapon(int ownerId, string weaponId);
    void RegisterWeapon(int ownerId, BaseWeapon weapon);
}
```

**待修改引用点**：
- [ ] `ChooseWeaponPanel`（当前直接读 `WeaponManager.Instance.weaponSlots`）
- [ ] `WeaponSelectSO.OnSelect`（事件需知道是哪个玩家选择的）
- [ ] `BaseWeapon.OnEnable()`（注册时需上报 owner）

---

### 🟡 P1 - TowerManager

**现状问题**：全局塔注册表。

**联机评估**：如果是合作模式，塔是共享的，冲突较小。如果是对战模式，塔属于阵营。

**重构策略**（轻度）：
1. 提取 `ITowerManager` 接口
2. 预留 `TeamId` 或 `OwnerId` 字段（暂时不用，但接口支持）

```csharp
public interface ITowerManager {
    IReadOnlyList<BaseTower> AllTowers { get; }
    void Register(BaseTower tower);
    void Unregister(BaseTower tower);
    
    // 预留：按阵营查询
    // IReadOnlyList<BaseTower> GetTowersByTeam(int teamId);
}
```

**待修改引用点**：
- [ ] `BaseTower.OnEnable/OnDisable`（注册/注销）
- [ ] `TowerPlacementController`（放置后注册）
- [ ] `ChooseTowerPanel`（UI 显示塔列表，通常只读，影响小）

---

### 🟡 P1 - TowerPlacementController

**现状问题**：内部直接调用 `InputHandleFactory.GetLocalInput()`，硬编码了单机输入。

**联机冲突**：联机模式下，玩家可能收到远程的"放置塔"指令。

**重构策略**：
1. `TowerPlacementController` 通过构造函数/Init 注入 `IInputHandle`
2. 联机模式下，服务器下发 "PlaceTower" RPC，客户端用虚拟输入模拟

```csharp
public class TowerPlacementController : MonoBehaviour {
    private IInputHandle _input;
    
    public void Init(IInputHandle input, TowerDataSO data) {
        _input = input;
        // ...
    }
}
```

**待修改引用点**：
- [ ] `ChooseTowerPanel`（创建时传入输入源）
- [ ] `GamePanel`（确认/取消按钮的回调）

---

### 🟢 P2 - UIManager

**现状问题**：纯C#单例，管理面板生命周期。

**联机评估**：UI 是本地显示层，通常不需要同步。

**重构策略**（最小改动）：
1. 仅提取 `IUIManager` 接口，方便测试时 Mock
2. 保持单例注册到 ServiceLocator

```csharp
public interface IUIManager {
    T ShowPanel<T>() where T : BasePanel;
    void HidePanel(string panelName);
    T GetPanel<T>() where T : BasePanel;
}
```

---

### 🟢 P2 - DamageNumManager

**现状问题**：MonoBehaviour单例，管理伤害数字对象池。

**联机评估**：伤害数字是纯视觉效果，不同步也无所谓。

**重构策略**：
1. 提取 `IDamageNumManager` 接口
2. 联机模式下可注册 `NullDamageNumManager`（如果服务器不需要）

---

### 🟢 P2 - SOManager

**现状问题**：MonoBehaviour单例，持有所有配置数据引用。

**联机评估**：配置数据是只读的，所有客户端共享同一份。

**重构策略**：
1. 提取 `ISOManager` 接口
2. 这是最安全、最简单的提取，没有任何副作用

```csharp
public interface ISOManager {
    EntityDataRegistry EntityDataRegistry { get; }
    Material TowerHighlightMaterial { get; }
    IReadOnlyList<LevelUpSO> GetUpgradePool(string tag);
}
```

---

### 🟢 P2 - BKMusic / JsonMgr

**联机评估**：
- `BKMusic`：音频是本地播放，联机下不需要同步
- `JsonMgr`：本地存档，联机下改用服务器存储

**重构策略**：
- 仅提取接口，保持现有单例实现
- 联机时注册新实现（如 `ServerSaveManager` 实现 `IJsonManager`）

---

## 三、基础设施：先建什么？

### Step 1: ServiceLocator（必须在所有 Manager 之前完成）

```csharp
// Assets/Script/Core/ServiceLocator.cs
public static class ServiceLocator {
    private static readonly Dictionary<Type, object> _services = new();
    
    public static void Register<T>(T service) {
        _services[typeof(T)] = service;
    }
    
    public static T Get<T>() {
        if (_services.TryGetValue(typeof(T), out var svc)) return (T)svc;
        throw new InvalidOperationException($"Service {typeof(T).Name} not registered");
    }
    
    public static bool TryGet<T>(out T service) {
        if (_services.TryGetValue(typeof(T), out var svc)) {
            service = (T)svc;
            return true;
        }
        service = default;
        return false;
    }
}
```

### Step 2: MonoSingleton 泛型基类（统一现有单例）

```csharp
// Assets/Script/Core/MonoSingleton.cs
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T> {
    public static T Instance { get; private set; }
    
    protected virtual void Awake() {
        if (Instance != null) {
            Destroy(gameObject);
            return;
        }
        Instance = (T)this;
        OnAwake();
    }
    
    protected virtual void OnDestroy() {
        if (Instance == this) Instance = null;
    }
    
    protected virtual void OnAwake() { }
}
```

### Step 3: 统一注册点（场景启动时一次性注册）

```csharp
// Assets/Script/Core/Bootstrap.cs
public class Bootstrap : MonoBehaviour {
    void Awake() {
        // 按依赖顺序注册
        ServiceLocator.Register<IPlayerManager>(PlayerManager.Instance);
        ServiceLocator.Register<IGameLevelManager>(GameLevelManager.Instance);
        ServiceLocator.Register<IWeaponManager>(WeaponManager.Instance);
        ServiceLocator.Register<ITowerManager>(TowerManager.Instance);
        ServiceLocator.Register<ISOManager>(SOManager.Instance);
        ServiceLocator.Register<IUIManager>(UIManager.Instance);
        // ...
    }
}
```

---

## 四、重构顺序建议

```
第1轮：基础设施
  └─ ServiceLocator.cs（新建）
  └─ MonoSingleton<T>（新建）
  └─ Bootstrap.cs（新建，场景挂载）

第2轮：只读/低风险 Manager（提取接口，不影响业务逻辑）
  └─ ISOManager → SOManager
  └─ IUIManager → UIManager
  └─ IDamageNumManager → DamageNumManager
  └─ IJsonManager → JsonMgr

第3轮：输入系统（已有良好基础，改动小）
  └─ IInputHandle 已存在，扩展 InputHandleFactory
  └─ TowerPlacementController 注入 IInputHandle

第4轮：核心游戏逻辑（高风险，需测试）
  └─ IPlayerManager → PlayerManager
  └─ IExperienceController → ExperienceLevController
  └─ IWeaponManager → WeaponManager

第5轮：关卡/状态管理（最高风险）
  └─ IGameLevelManager → GameLevelManager
  └─ 考虑 GameContext 模式

第6轮：联机适配
  └─ NetworkPlayerManager 实现 IPlayerManager
  └─ NetworkGameLevelManager 实现 IGameLevelManager
  └─ NetworkInputHandle 实现 IInputHandle
```

---

## 五、检查清单（每轮完成后勾选）

### 基础设施
- [ ] ServiceLocator 实现完成
- [ ] MonoSingleton<T> 实现完成
- [ ] Bootstrap 场景挂载并测试

### 低风险接口提取
- [ ] ISOManager + SOManager 修改完成
- [ ] IUIManager + UIManager 修改完成
- [ ] IDamageNumManager + DamageNumManager 修改完成
- [ ] IJsonManager + JsonMgr 修改完成

### 输入系统扩展
- [ ] InputHandleFactory.Get(string id) 实现
- [ ] TowerPlacementController 注入 IInputHandle
- [ ] PlayerController 支持配置 _inputHandleId

### 核心逻辑重构
- [ ] IPlayerManager 接口
- [ ] PlayerManager 实现多玩家字典
- [ ] IExperienceController 接口
- [ ] ExperienceLevController 改为非单例
- [ ] IWeaponManager 接口
- [ ] WeaponManager 支持 ownerId

### 关卡状态重构
- [ ] IGameLevelManager 接口
- [ ] GameLevelManager 状态机拆分
- [ ] 暂停逻辑联机兼容

### 联机实现
- [ ] NetworkPlayerManager
- [ ] NetworkGameLevelManager
- [ ] NetworkInputHandle
- [ ] 帧同步/状态同步选型（Mirror/FishNet/Netcode）

---

## 六、注意事项

1. **不要同时改多个Manager**：每次只重构一个，保证编译通过、运行正常
2. **先提接口，再改实现**：先让所有调用方通过 ServiceLocator.Get<T>() 访问，再修改具体类
3. **保留 `.Instance` 作为兼容层**：重构期间，Manager 仍然提供 `.Instance`，但内部实现走 ServiceLocator
4. **测试优先级 P0 的功能**：PlayerManager 和 GameLevelManager 是游戏核心，改完必须完整跑一局
5. **meta 文件**：每次新增 .cs 文件后，切回 Unity 让编辑器生成 .meta
