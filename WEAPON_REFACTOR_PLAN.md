# 武器重构方案（Weapon / Attacker 分离）

> 起因：认为"武器只有 SO，没有 prefab、没有自己的 data，数值读实体的 StatModel，
> 导致武器升级没有自己的 modify"。
> 本文先把**现状事实**摆清楚，再给出真正需要改的地方与分阶段方案。

---

## 1. 现状事实（已核实，附证据）

### 1.1 武器**已经**是独立实体

| 用户判断 | 实际 | 证据 |
|---|---|---|
| 没有 prefab | ❌ 有 | `Weapon_Gun/Cannon/Spin/Laser.prefab`（Addressable，`WeaponEntitySO.prefab` 引用） |
| 没有自己的 data | ❌ 有 | `WeaponDataSO` 子类 + 资产：`GunWeaponDataSO`(Damage/BulletSpeed/BulletHitForce)、`SpinWeaponDataSO`(Damage/RotationSpeed/Size/LifeTime/HitPushForce)、`CannonWeaponDataSO`、激光 |
| 数值读实体的 StatModel | ❌ 读的是**武器自己**的 | `Weapon_Gun.prefab` 上 `GunWeapon` 与 `AttackDriver` **同物体** → `_self = GetComponentInParent<EntityBehaviour>()` 解析到**武器自身** → `AttackMethodSO` 里 `ctx.Self.GetStat(Damage)` 取的是武器模型 |
| 武器升级没有自己的 modify | ⚠️ 一半对 | Gun / Spin 的 `WeaponEntitySO.upgrades` 各 3 项，`ApplyTo(weapon)` 写的是**武器**的 StatModel；**但 Cannon / Laser 的 upgrades 是空的** |

`EquipAsync` 里也做了配置注入：`weapon.SetEntityConfig(slot.Config)` →
`WeaponDataSO.FillStatModel` 把 Damage/BulletSpeed/… 写进武器自己的 StatModel。

### 1.2 真正的问题：**两套模型并存**

| 谁 | `AttackDriver` 挂在哪 | "武器"是什么 | 有武器 prefab/data/升级吗 |
|---|---|---|---|
| 玩家 | **武器 prefab** 上 | 武器实体 | ✅ 有 |
| **塔** | **塔本体** 上 | 只是攻击 SO + `_extraAttacks` 槽位 | ❌ 没有 |
| 敌人 | 没有 driver（只有接触伤害） | — | — |

实测挂了 `AttackDriver` 的 prefab：`Weapon_Gun/Cannon/Spin/Laser`（4 把武器）
+ `Tower_Luo/Rin/Teto`（3 座塔本体）。

**所以"武器不是严格意义上的武器"的真正来源是塔那一侧** ——
塔的攻击长在塔身上，塔的"第二把武器"只是列表里的一个 SO，没有自己的 prefab、
没有自己的 data、没有自己的升级项。玩家那一侧反而是对的。

### 1.3 其它确凿的耦合点

| # | 位置 | 问题 |
|---|---|---|
| ① | `AttackDriver._self = GetComponentInParent<EntityBehaviour>()` | **靠层级猜"数值归谁"** —— 驱动挂错一层就静默换一整套数值，没有任何报错 |
| ② | `BaseWeapon.Owner → GetComponentInParent<PlayerController>()` | 武器基类**玩家专属**，塔无法复用武器 prefab |
| ③ | `AttackContext` 只有 `Self`（=数值来源） | "谁发起攻击"（击杀归属、塔账本）没有独立字段，靠 `Self` 兼任 |
| ④ | 目标检测在 `AttackDriver` 里（`_targetsInRange` + 索敌触发体） | 一个实体挂两把武器就要两份索敌圈；塔现在是"一个圈喂一个 driver" |
| ⑤ | `Weapon_Cannon` / `Weapon_Laser` 的 upgrades 为空 | 数据缺口（不是架构问题） |

---

## 2. 目标模型

一句话：**"会攻击的东西"统一叫武器（带 prefab / data / StatModel / 升级 / 攻击方式），
实体只是武器的宿主（位置、血量、归属、索敌、权威）。**

```
IWeaponHost（宿主，玩家 / 塔 / 未来的召唤物都实现它）
├─ Transform 发射基准点
├─ 索敌：AttackTargetRegistry（**一份**目标表，宿主持有）
├─ 权威/归属：Authority、击杀归属、塔账本
└─ 武器槽：IReadOnlyList<WeaponInstance>

WeaponInstance（武器实例 = prefab 上的 BaseWeapon + AttackDriver）
├─ EntityConfig → 自己的 WeaponDataSO → 自己的 StatModel（数值来源）
├─ AttackDriver（攻击方式 + 冷却 + 发射）—— **永远挂在武器上**
└─ upgrades → 写自己的 StatModel
```

关键决定：

1. **`AttackDriver` 一律挂在武器实例上**，宿主身上不再有驱动。
2. **`AttackDriver` 的数值来源显式化**：由装配方 `SetSelf(weapon)` 注入，
   不再 `GetComponentInParent` 猜（①）。缺注入时**明确报错**而不是静默用默认值。
3. **索敌上移到宿主**（④）：`AttackDriver` 向宿主要目标表；
   宿主一份表喂所有武器（塔两把武器不再需要两个索敌圈）。
4. **`AttackContext` 拆成 `Self`（武器）+ `Host`（宿主）**（③）：
   伤害结算读 `Self`，击杀归属/账本读 `Host`。
5. **`BaseWeapon.Owner` → `IWeaponHost Host`**（②）：塔也能挂 `BaseWeapon` 子类。
6. **塔的武器配置与角色对称**：`TowerEntitySO.defaultWeaponIds`（+ `maxWeaponSlots`），
   放置时按 id 生成武器 prefab —— 与 `CharacterDefinitionSO.defaultWeaponIds` 同一套写法。
7. **"改装武器"升级项**随之变成"切换激活武器实例"，语义比"切换攻击槽下标"更稳
   （不再依赖 prefab 上槽位顺序）。

---

## 3. 分阶段实施

### Stage 1：显式化数值归属（低风险，**行为零变化**）

| 改动 | 说明 |
|---|---|
| `AttackDriver` 增加显式数值来源 | `public void SetSelf(EntityBehaviour self)`（或 `WeaponHost` 字段），装配方注入；未注入时报错 |
| `AttackContext` 增加 `Host` | 伤害结算仍读 `Self`；归属改读 `Host`（塔账本/击杀统计） |
| `BaseWeapon.Owner` → `IWeaponHost` | 玩家/塔都能作为宿主；`GunWeapon`/`SpinWeapon` 里玩家专属的部分保留在子类 |
| 补数据 | `Weapon_Cannon` / `Weapon_Laser` 各补 2–3 个升级项 |

**验证**：批处理编译 + 体检 + 105 断言；Play 确认数值与升级效果不变。

### Stage 2：塔改为"挂武器实例"（中风险）

| 改动 | 说明 |
|---|---|
| 索敌上移 | 新增 `AttackTargetRegistry`（宿主持有）；`AttackDriver` 读宿主的表 |
| 塔武器化 | 为每座塔的每种攻击建武器 prefab + `WeaponEntitySO` + `WeaponDataSO`（Teto ×2：速射弹/溅射炮弹；Luo/Rin 各 1） |
| `TowerEntitySO.defaultWeaponIds` | 与角色对称；放置时生成 |
| 改装升级 | `TowerWeaponSwapUpgradeSO` 从"切槽"改为"切激活武器" |
| 旧路径清理 | 塔 prefab 上的 `AttackDriver` + `_extraAttacks` 删除（死代码直接删） |

**验证**：批处理 + Play 回归（塔的攻击/索敌/治疗/升级/改装全部走一遍）。

### Stage 3（可选，建议先不做）

- 敌人的接触伤害也做成"武器"：**不建议** —— 接触伤害不是"发射物 + 冷却"模型，
  强行统一只会多一层抽象（YAGNI）。
- 角色通用升级影响武器数值：需要明确设计（哪些通用数值对武器生效），先留空。

---

## 4. 成本与风险

| 项 | 评估 |
|---|---|
| 影响面 | **战斗核心**（攻击、索敌、升级、塔放置）+ 资产（约 4 个新武器 prefab/SO） |
| 批处理能验证的 | 编译、接线、层、断言 |
| **只能 Play 验证的** | 攻击数值、索敌范围、冷却、改装切换、多武器共存 |
| 回滚成本 | Stage 1 低（纯接口/注入）；Stage 2 中（资产新增，旧路径可暂时保留一版） |
| 数据生成物 | 新增武器 `WeaponEntitySO` 后需重跑 `Tools ▸ Export Entity Id Catalog` |

---

## 5. 建议

- **Stage 1 立刻做**：它把"数值归谁"从隐式变显式（①③），是后面所有改动的地基，
  且行为零变化、风险最低。
- **Stage 2 单独排期做**：它是"武器彻底独立"的实质部分，需要一次完整的 Play 回归。
- **Stage 3 不做**：接触伤害与元成长通道都不该现在统一。

---

## 6. 实施进度

### ✅ Stage 1（完成，已验证：编译 + 体检 + 105 断言）

| 改动 | 落点 |
|---|---|
| 显式数值来源 | `AttackDriver.SetSelf()` / `SetHost()`；未注入时退回层级推断并**告警一次**（迁移完成后不应再出现） |
| 数值与归属分离 | `AttackContext` 新增 `Host`；`AreaDamage` / `Beam` / `Projectile` / `Orbit` / `HealAllies` 五处改用 `ctx.Host` 做归属 |
| 武器基类去玩家化 | 新增 `IWeaponHost`；`BaseWeapon.Host` + `BindAttackDriver()`；`PlayerWeaponController` 实现该接口并在装配时注入 |
| 补数据缺口 | `Weapon_Cannon` 补 3 项、`Weapon_Laser` 补 2 项（工具 `WeaponUpgradeSetup`，幂等） |

### ✅ Stage 2a（代码完成，已验证：编译 + 体检 + 105 断言）

| 新增 | 作用 |
|---|---|
| `TowerWeaponDataSO` | 塔用武器的数值 data（字段与 `TowerDataSO` 的攻击部分同名同义） |
| `TowerWeaponRangeSync` | 把武器自己的 `TowerAttackRange` 写进它的检测圈（**升级射程才有效**） |
| `TowerWeaponController` | 按数据装配武器实例 + `SetActiveWeapon()`；实现 `IWeaponHost` |
| `TowerWeaponSwapUpgradeSO`（改） | 从"切攻击槽"改为"切激活武器"；**迁移期回退**到旧槽位，塔未武器化前功能不受影响 |

**与原方案的一处偏差**：**索敌不集中到宿主**，仍随武器走（每把武器自带检测圈）。
理由：塔同时只有一把武器激活，集中式目标表要引入"版本号 + 每驱动过滤快照"才能避免分配，
复杂度换不来收益；而且"射程随武器"本来就是更正确的语义（每把武器有自己的射程）。

### ✅ Stage 2b/2c/2d（完成，已验证：编译 + 体检 + 105 断言 + 目录重生成）

| 产物 | 内容 |
|---|---|
| 4 把塔武器 | `Weapon_TetoBullet`（速射弹）、`Weapon_TetoShell`（溅射炮弹）、`Weapon_RinAoE`、`Weapon_LuoHeal` —— 各自带 prefab（`BaseWeapon` + `AttackDriver` + 索敌圈 + `TowerWeaponRangeSync`）、`TowerWeaponDataSO`（数值从塔 data **逐项搬过来**）、`WeaponEntitySO` |
| 3 座塔 | 移除塔身上的 `AttackDriver`，挂上 `TowerWeaponController`（Teto 2 把 / Rin 1 把 / Luo 1 把）；塔的 `_detectionCollider` 保留（体检要求，现在只喂范围可视化） |
| 旧路径清理 | `AttackDriver` 的 `_extraAttacks` / `ActiveSlot` / `SetActiveSlot` / `SlotCount` / `ValidateSlotTargetTags` 全部删除 |
| 目录 | `Assets/EntityIdCatalog.csv` 重新生成：**21 条**（新增 4 把塔武器） |

**✅ 已解决**：塔面板改为从**两处**取选项 —— 塔自己的 `upgrades`（目标 = 塔）+ **当前装配武器**的
`upgrades`（目标 = 武器实例）。攻击类升级项已迁到武器上（Teto 两把各 4 项、Rin 3 项、Luo 3 项），
`*RecoverHealth` 与 `TetoSwapShell` 留在塔上（它们作用的本来就是塔）。


