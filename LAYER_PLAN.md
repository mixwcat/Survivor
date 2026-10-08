# Tier 2：物理层方案（Layer Plan）

> 目标：把"几何判定"从**代码**搬到**物理层** —— 让"谁能撞谁"由碰撞矩阵保证，
> 从而**删除**各攻击系统里的 tag 判定与 workaround。
> 非目标：**不用 layer 取代 tag**。tag 继续表达身份/玩法规则（"该不该打"），
> `Hurtbox` 标记继续表达语义（"哪个碰撞体是本体"），层只表达物理（"谁能撞谁"）。

---

## 1. 现状（已核实，2026-10）

| 项 | 现状 |
|---|---|
| 已用层 | 只有 `7 = Player`、`8 = Tower`；**其余全部 `0 = Default`** |
| 层碰撞矩阵 | **全 1**（等于没有过滤：任何重叠都会产生配对与回调） |
| 代码里的层依赖 | **零** —— 全项目没有 `LayerMask` / `gameObject.layer` 判定（只有两处 `ContactFilter2D.useLayerMask = false`）→ **可以自由重排层** |
| 相机 culling mask | `Everything`（`m_Bits: 4294967295`）→ 新层照常渲染，不会"物体消失" |
| 物理设置 | `QueriesHitTriggers = 1`、`QueriesStartInColliders = 1`、`SimulationMode = FixedUpdate` |
| 碰撞体分布 | 玩家根=7；塔（本体 + `SearchRange` + `PlayerDetectRange`）=8；敌人（本体 + `ColliderTrigger` 子物体）=0；推车=0；子弹/`TetoBullet`/经验球=0（**触发器**） |

**为什么值得做**：同一件事（"这个碰撞体算不算打到了这个实体"）现在有 6 套判据，
历史上对应 4 次线上问题（怪群互相掉血、玩家被火球"代打"掉血、怪隔空打塔、溅射双结算）。
Tier 1 已经把判据收敛成 1 套（`DamageTargetResolver` + `Hurtbox`）；
Tier 2 让**错误的配对根本不触发**，于是连那一套都不需要在每个调用点重复。

---

## 2. 层方案（10 个层，沿用 7/8 现有编号）

| 层号 | 名称 | 放什么 | 为什么单独一层 |
|---|---|---|---|
| 7 | `PlayerBody` | 玩家本体碰撞体 | 被接触伤害打；触发交互与拾取 |
| 8 | `TowerBody` | 塔本体碰撞体 | 被接触伤害打；治疗友军的目标 |
| 9 | `EnemyBody` | 敌人本体碰撞体 | 被玩家攻击打；被塔索敌 |
| 10 | `CartBody` | 推车本体碰撞体 | 被接触伤害打 |
| 11 | `WeaponHitbox` | 子弹、火球、光束/炮弹判定体 | **只与 `EnemyBody` 配对** —— 从物理上杜绝"武器判定体被当成玩家本体" |
| 12 | `EnemyDetector` | 敌人的 `ColliderTrigger` | **只与三个本体配对** —— 从物理上杜绝"怪打到武器/检测圈" |
| 13 | `TowerDetector` | 塔的 `SearchRange` / `PlayerDetectRange` | 只与 `EnemyBody`/`TowerBody`/`PlayerBody` 配对 |
| 14 | `InteractZone` | 交互触发区（武器台/塔台/选角台/传送门/修车区） | 只与 `PlayerBody` 配对 |
| 15 | `Pickup` | 经验球 | 只与 `PlayerBody` 配对 |
| 16 | `Boundary` | 关卡边界 / 清怪区 | 只与 `EnemyBody` 配对 |

> 本体与检测体**分不同层**是关键：同一个实体的"本体"和"触发体"必须在物理上可区分，
> 否则"怪碰到检测圈"与"怪碰到本体"永远分不开。

---

## 3. 碰撞矩阵

| 层 | 与之开启碰撞 | 说明 |
|---|---|---|
| `PlayerBody` | `EnemyDetector`、`InteractZone`、`Pickup`、`EnemyBody`、`TowerBody`、`CartBody` | 前三个是触发关系；后三个**保留物理阻挡**（不穿模） |
| `TowerBody` | `EnemyDetector`、`TowerDetector`、`EnemyBody`、`PlayerBody`、`CartBody` | 接触伤害 + 治疗友军 + 保留阻挡 |
| `CartBody` | `EnemyDetector`、`EnemyBody`、`PlayerBody`、`TowerBody` | 接触伤害 + 保留阻挡 |
| `EnemyBody` | `WeaponHitbox`、`TowerDetector`、`Boundary`、`PlayerBody`、`TowerBody`、`CartBody` | 被打 / 被索敌 / 被清理 / 被阻挡 |
| `WeaponHitbox` | **仅** `EnemyBody` | ★ 与 `EnemyDetector` **不配对** → 根治"玩家被火球代打" |
| `EnemyDetector` | **仅** `PlayerBody`、`TowerBody`、`CartBody` | ★ 与 `WeaponHitbox`/`TowerDetector`/`InteractZone` **不配对** → 根治"怪打武器"与"怪打检测圈" |
| `TowerDetector` | `EnemyBody`、`TowerBody`、`PlayerBody` | 索敌 / 治疗友军 |
| `InteractZone` | **仅** `PlayerBody` | 交互与提示只可能由玩家触发 |
| `Pickup` | **仅** `PlayerBody` | 拾取 |
| `Boundary` | **仅** `EnemyBody` | 清怪 |

**刻意关闭的配对**：

| 配对 | 后果 | 理由 |
|---|---|---|
| `EnemyBody × EnemyBody` | 怪可以互相重叠 | 玩法取舍（包围感更强）+ 物理开销骤降。**别当成遗漏补上**（体检会盯着它） |
| `WeaponHitbox × WeaponHitbox` | 子弹/火球互不碰撞 | 无意义配对，白费物理 |
| `TowerBody × TowerBody` | 塔之间不阻挡 | 放置校验本来就禁止重叠 |

**为什么"触发体不会造成阻挡、本体不会产生触发回调"**：非触发器只产生碰撞响应、触发器只产生回调 ——
所以把"阻挡对"与"触发对"混在同一张矩阵里是安全的（例如 `PlayerBody × EnemyBody` 开着只影响阻挡）。

---

## 4. 因此可以删掉的代码

| # | 文件 | 删除内容 | 为什么可以删 |
|---|---|---|---|
| 1 | `BulletController.OnTriggerEnter2D` | `target.CompareTag("Enemy")` | 子弹只可能与 `EnemyBody` 配对 |
| 2 | `BulletController.ApplySplashDamage` | `CompareTag("Enemy")`；过滤器改为 `layerMask = EnemyBody` | 同上（并去掉"靠 `useTriggers` 绕开"的历史注释） |
| 3 | `SpinWeaponController.OnTriggerEnter2D` | `CompareTag("Enemy")` | 同上 |
| 4 | `EnemyHealthController.ResolveDamageTarget` | "排除 Enemy" 那条规则 | `EnemyDetector` 在物理上就看不见敌人 |
| 5 | `BeamAttackSO` | `layerMask = EnemyBody`（替换 `useLayerMask = false`）；保留 `TargetTag` 规则 | 同上 |
| 6 | `ExpSpriteController.OnTriggerEnter2D` | `CompareTag("Player")` | `Pickup` 只与 `PlayerBody` 配对 |
| 7 | `EnemyBoundary` | `CompareTag("Enemy")` → **改成组件确认**（保留一次身份检查） | 层已经保证只可能是敌人本体；但销毁不可逆，而 `Default` 刻意没隔离 —— 必须再挡一次 |
| 8 | `PortalController.ResolvePlayer` | "自身没有就向上找"的双重兜底 | 触发区只会有玩家本体 |
| 9 | `TowerPlacementController`（2 处） | `CompareTag("Player")||Enemy||Tower` → `layerMask` 查询 | 放置阻挡是几何问题 |
| — | `AttackDriver.ResolveTarget` | **保留** tag 判定 | 它是"打谁"的玩法规则（治疗要治塔），不是几何 |
| — | `Hurtbox` 标记 | **保留** | 它表达语义；层是物理过滤，两者互补 |

预计净删除 **~9 处 tag 判定 + 3 处历史 workaround 注释**。

---

## 5. 需要改层的数据清单（约 15 项）

| 对象 | 碰撞体 | 新层 |
|---|---|---|
| `Player.prefab` | 根节点 CapsuleCollider2D | `PlayerBody` |
| `Enemy_Cloud/Slime/Soil/Snake/Spoil/Wolf.prefab`（6） | 根节点本体碰撞体 | `EnemyBody` |
| 同上（6） | 子物体 `ColliderTrigger` | `EnemyDetector` |
| `Tower_Luo/Rin/Teto.prefab`（3） | 根节点本体碰撞体 | `TowerBody` |
| 同上（3） | `SearchRange` | `TowerDetector` |
| 同上（3） | `PlayerDetectRange`（带 `InteractionSensor`，驱动交互提示） | `InteractZone` —— 由**组件语义**决定，而不是"它长在塔上" |
| `Cart.prefab` | 根节点 BoxCollider2D | `CartBody` |
| `Bullet.prefab`、`Common/TetoBullet.prefab`、`Weapon/Spin.prefab`（环绕火球） | 碰撞体 | `WeaponHitbox` |
| `ExpSprite.prefab` | 碰撞体 | `Pickup` |
| `Lobby.unity`：武器台/塔台/选角台/传送门 | 触发体 | `InteractZone` |
| `Level0.unity`：`RepairZone` | 触发体 | `InteractZone` |
| `Level0.unity`：`EnemyBoundary` 的 `Bound` 子物体 | 触发体 | `Boundary`（组件在父物体上，工具按**层级**找） |

**刻意保持 `Default` 的（不是遗漏）**：`InteractionPrompt`（世界空间提示的触屏点击体）、
`SpriteToHandle`（表现用）—— 它们不参与伤害/交互判定，隔离它们只会制造假故障。

> 注意：`RepairZone` 等是**场景里加到 prefab 实例上的子物体**，层要在场景里改（属于实例覆盖）。

---

## 6. 实施顺序（每步独立可验证）

| 步 | 内容 | 行为变化 | 验证方式 |
|---|---|---|---|
| 1 | 写层名 + 设置矩阵（工具 `Editor/LayerSetup.cs`） | **零**（还没有碰撞体用新层） | 体检：矩阵与层名符合本方案 |
| 2 | 改 prefab / 场景的碰撞体层 | ⚠️ **开始变化** | Play 回归（见 §7） |
| 3 | 删除 §4 的判据 | 无（等价替换） | 编译 + 体检 + Play 回归 |
| 4 | （可选）关闭 `EnemyBody × EnemyBody` | 怪可以互相重叠 | Play 看手感 + 性能 |

**第 1 步可以立刻做且行为零变化**；第 2 步起必须一次做完并完整回归，不要和功能开发混在一起。

---

## 7. Play 回归清单（物理回归无法用批处理验证）

1. 子弹 / 火球 / 炮弹溅射 / 光束都能正常打到敌人
2. 玩家、塔、推车照常被怪**接触掉血**
3. 四个历史症状不回归：怪不互相掉血、玩家不被火球代打、怪不隔空打塔、溅射不双结算
4. 经验球照常被吸附与拾取
5. 三个台子 + 传送门 + 修车区照常出提示、能交互
6. 怪越过边界时照常被清理
7. 敌人与玩家/塔/推车**照常互相阻挡**（不穿模）
8. 塔的索敌与治疗（Luo）照常工作

---

## 8. 性能预期

- **现在**：矩阵全 1 → 每个触发体都要与所有重叠体配对。上百敌人时，敌人之间的配对是最大的一块。
- **改后**：`EnemyDetector` 只与 3 个层配对、`WeaponHitbox` 只与 1 个层配对 → 宽相位/窄相位工作量显著下降。
- **第 4 步（可选）**：关闭 `EnemyBody × EnemyBody` 可再省掉最大的一块，但怪会互相重叠 —— **需要你定**（这是玩法取舍，不是纯优化）。

---

## 9. 工具与回归防护

新增 `Assets/Editor/LayerSetup.cs`：

- `Tools ▸ Setup Physics Layers`：写层名 + 按 §3 设置矩阵（幂等；可重复运行）
- `VerifyAll(...)`：供 `SceneWiringAudit` 调用 —— 检查层名与矩阵是否符合本方案（防止有人手改后无人发现）
- `HurtboxSetup.VerifyAll(...)` 继续保留：标记仍是"本体"的语义来源

**回滚成本**：低。层名与矩阵由工具一键重设；代码改动是"删判据"，恢复即加回。

---

## 10. 实施结果（已完成）

| 步 | 结果 |
|---|---|
| 1 层名 | ✅ 10 个层写入（`PlayerBody` … `Boundary`） |
| 2 碰撞体层 | ✅ 33 个对象（含场景里的实例与场景对象） |
| 3 矩阵 | ✅ 方案内 10 层共 55 个无序对 → **16 开启 / 39 关闭**（`Physics2D.GetIgnoreLayerCollision` 实测） |
| 4 删判据 | ✅ **8 处 tag 判定 + 2 处 workaround**；`EnemyBoundary` 保留一次组件确认（销毁不可逆） |

**最终层归属（实测）**

| 对象 | 层 |
|---|---|
| `Player.prefab` 根 | `PlayerBody` |
| 敌人（6）本体 / `ColliderTrigger` | `EnemyBody` / `EnemyDetector` |
| 塔（3）本体 / `SearchRange` / `PlayerDetectRange` | `TowerBody` / `TowerDetector` / `InteractZone`（它带 `InteractionSensor`，驱动交互提示） |
| `Cart.prefab` 根 | `CartBody` |
| `Bullet` / `TetoBullet` / `Spin`（环绕火球） | `WeaponHitbox` |
| `ExpSprite` | `Pickup` |
| Lobby：`TowerBench` / `WeaponBench` / `CharacterSwitchBench` / `Portal` | `InteractZone` |
| Level0：`RepairZone` / `EnemyBoundary` | `InteractZone` / `Boundary` |

**实测关闭的关键配对**（= 四个历史问题在物理层被根治）：
`WeaponHitbox × EnemyDetector`、`WeaponHitbox × PlayerBody`、`EnemyDetector × EnemyDetector`、
`EnemyDetector × InteractZone`、`EnemyBody × EnemyBody`。

**刻意保持 `Default`（3 项）**：`SpriteToHandle`、`InteractionPrompt`、
Level0 的 `Bound`（场景里另一个根对象的碰撞体，与 `EnemyBoundary` 无关 —— 工具会列出来供人工确认）。

**迁移过程中体检抓到的真 bug**：把"找组件"扩到**子物体**后，塔本体被误判成 `InteractZone`
（塔本体是"带 `InteractionSensor` 的子物体"的父物体）→ 后果是**怪打不到塔** ✗。
规则改成「**本体优先** + 只向**父级**找」后修复 ✓ —— 这正是 §9 那条回归检查的价值。

**已知无害残留**：`Weapon_Gun/Cannon/Laser/Spin` 这些**挂载** prefab 的根仍在 `PlayerBody` 层（历史遗留），
但它们没有碰撞体，层是惰性的 ✓。

