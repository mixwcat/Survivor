# EDM — Entity Data Model（实体数据模型）

EDM 是本项目的核心数值架构，采用**数据驱动 + 运行时修饰**的设计：
- **策划**通过 ScriptableObject 资产配置基础数值，无需写代码
- **运行时**所有数值聚合为最终值，支持升级/装备/Buff 等动态修改
- **业务代码**统一通过 `GetStat(StatType)` 读取，不关心数值来源

---

## 架构分层

```
┌─────────────────────────────────────────────────────────────┐
│                    调用层（业务代码）                          │
│  PlayerController  BaseTower  BaseWeapon  EnemyController   │
│  BaseHealthController  Teto/Rin/Luo  Spin/GunWeapon         │
├─────────────────────────────────────────────────────────────┤
│                    升级层（数据驱动修改）                       │
│  LevelUpSO.ApplyTo(entity) → StatModel.AddModifier()        │
├─────────────────────────────────────────────────────────────┤
│                    运行时层（Model）                           │
│  EntityStatModel — 基础值字典 + 修饰符字典 → 实时聚合最终值     │
│  StatModifier      — 单条修饰符（类型/数值/来源）              │
├─────────────────────────────────────────────────────────────┤
│                    配置层（Data SO）                           │
│  PlayerDataSO / TowerDataSO / WeaponDataSO / EnemyDataSO    │
│  BaseEntityDataSO（抽象基类，定义 FillStatModel 流程）        │
├─────────────────────────────────────────────────────────────┤
│                    元数据层                                    │
│  StatType enum     — 全局数值类型标识                          │
│  EModifierType enum — Add / Multiply / Override              │
└─────────────────────────────────────────────────────────────┘
```

---

## 核心类速查

| 文件 | 职责 | 关键 API |
|---|---|---|
| `StatType.cs` | 全局数值枚举 | 新增数值时在此扩展 |
| `EModifierType.cs` | 修饰符类型 | `Add`, `Multiply`, `Override` |
| `EntityBehaviour.cs` | 实体 MonoBehaviour 基类 | `GetStat(type)`, `SetEntityData(data)` |
| `EntityStatModel.cs` | 运行时数值容器 | `GetStat()`, `AddModifier()`, `OnStatChanged` |
| `StatModifier.cs` | 单条修饰符 | `TargetStat`, `Value`, `ModifierType`, `Source` |
| `BaseEntityDataSO.cs` | 数据配置基类 | `FillStatModel(model)` |
| `*DataSO.cs` | 各实体专属配置 | 继承基类，override `FillStatModel` |
| `StatDefinition.cs` | 单条基础值定义 | `Type` + `BaseValue` |
| `StatModifierData.cs` | 升级配置 struct | `TargetStat` + `Value` + `ModifierType` |

---

## 数值聚合公式

```
最终值 = (基础值 + ΣAdd) × (1 + ΣMultiply)

若存在 Override 修饰符 → 直接返回 Override 值（最高优先级）
```

---

## 完整数据流

### 1. 配置阶段（编辑时）
策划在 Unity Inspector 中创建/编辑 DataSO 资产：
- `PlayerData` → 设置 MaxHealth, MoveSpeed, PickRange, UnbeatableTime...
- `TowerData` → 设置 AttackRange, AttackInterval, HealAmount...
- `WeaponData` → 设置 RotationSpeed, BulletSpeed...
- `EnemyData` → 设置 MaxHealth, MoveSpeed, ExpReward...

### 2. 初始化阶段（运行时 Awake）
```csharp
// EntityBehaviour.Awake()
StatModel = new EntityStatModel();
_entityData?.FillStatModel(StatModel);   // 将 SO 中的基础值填充到 Model
```

### 3. 游戏过程中（升级/Buff）
```csharp
// LevelUpSO.ApplyTo(entity)
foreach (var modData in statModifiers)
{
    var modifier = new StatModifier(modData.TargetStat, modData.Value, modData.ModifierType, this);
    entity.StatModel.AddModifier(modifier);   // 立即生效
}
```

### 4. 读取阶段（业务代码轮询）
```csharp
// PlayerController.FixedUpdate()
float speed = GetStat(StatType.BaseMoveSpeed);
rb.linearVelocity = inputVector.normalized * speed;
```

### 5. 变化响应（事件驱动）
```csharp
// BaseTower.Start()
StatModel.OnStatChanged += OnAnyStatChanged;

// 当 TowerAttackRange 被升级修改时 → 自动更新碰撞体半径和圆环绘制
protected virtual void OnAnyStatChanged(StatType type)
{
    if (type == StatType.TowerAttackRange)
    {
        detectionCollider.radius = GetStat(StatType.TowerAttackRange);
        DrawCircle();
    }
}
```

---

## 模块调用地图

### 实体层（继承 EntityBehaviour）

| 实体 | 继承链 | 读取的 StatType | 订阅 OnStatChanged |
|---|---|---|---|
| `PlayerController` | EntityBehaviour | BaseMoveSpeed | ❌ |
| `BaseTower` | EntityBehaviour | TowerAttackRange, TowerAttackInterval... | ✅（攻击范围） |
| `BaseWeapon` | EntityBehaviour | TowerAttackInterval, FireBallRotationSpeed... | — |
| `EnemyController` | EntityBehaviour | BaseMoveSpeed, BaseMaxHealth, BaseDamage | ❌ |

### 血量层（依赖 EntityBehaviour）

| 类 | 读取的 StatType | 变化响应 |
|---|---|---|
| `BaseHealthController` | BaseMaxHealth, BaseDamage | MaxHealth 增加时 clamp CurrentHealth |
| `PlayerHealthController` | 继承基类 | — |
| `TetoHealthController` | 继承基类 | — |
| `RinHealthController` | 继承基类 | — |
| `LuoHealthController` | 继承基类 | — |

### 升级层（修改 StatModel）

| 类 | 修改方式 | 目标实体 |
|---|---|---|
| `LevelUpSO` | `ApplyTo(entity)` → AddModifier | 玩家/塔（通用） |
| `EnemyController` | `EnhanceWithWave()` → AddModifier | 自身（波次增强） |

### UI 层（触发升级应用）

| 类 | 调用链 |
|---|---|
| `LevelUpPanel` | 按钮点击 → `levelUpSO.ApplyTo(_player)` |
| `TowerLevelUpPanel` | 按钮点击 → `levelUpSO.ApplyTo(targetTower)` |
| `ChooseWeaponPanel` | 选择武器 → `onApplyEffect` → WeaponManager 激活武器 |

---

## 新增一个数值类型的步骤

1. **在 `StatType.cs` 中添加枚举值**
2. **在对应 `*DataSO.cs` 中添加字段**并在 `FillStatModel()` 中 `SetBaseValue`
3. **在业务代码中通过 `GetStat(StatType.YourNewStat)` 读取**
4. **（可选）创建 `LevelUpSO` 资产**供策划配置升级选项

> ⚠️ 不需要修改 `EntityStatModel` 或 `StatModifier` 的任何代码，所有新类型自动支持 Add/Multiply/Override 三种修饰方式。

---

## 关键设计决策

- **单一枚举 `StatType`**：所有实体共用同一套数值标识，便于统一管理和跨实体升级
- **Source 标记**：`StatModifier.Source` 标记来源对象，支持按来源批量移除（如卸载装备）
- **实时聚合**：`GetStat()` 每次调用时实时计算，确保修改立即生效，无需手动同步
- **事件通知**：`OnStatChanged` 让实体响应数值变化（如攻击范围扩大时自动重绘圆环）
- **与血量解耦**：`BaseHealthController` 独立为 MonoBehaviour，通过 `GetComponent` 关联，不污染 EDM 核心层
