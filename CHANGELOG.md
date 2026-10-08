# 决策日志

只记录**有长期影响的决策**及其理由。逐条 bug 修复不在此列（看 git 历史）。
被后续决策推翻的条目**保留并标注**，避免有人照着旧结论改代码。

---

## 2026-10-08 · 武器槽切换迁到新版 Input System

- 数字键 `1/2/3` 现在是 `.inputactions` 里 `Player` map 的三个 Button action（`Alpha1/2/3`），
  不再由 `PCInputHandle` 用旧版 `Input.GetKeyDown(KeyCode.AlphaN)` 轮询。
- **按键状态归 `InputReader`**：回调里只认 `Performed` 阶段，写入一个"待处理请求"，
  由 `ConsumeSlotSwitchRequest()` 取走即清空 —— 消费者（`PlayerController.Update`）与
  输入系统的更新点不是同一时刻，中间存一个值既不漏按也不重复触发。
- ⚠️ **`PCInputHandle` 里仍有旧版 API**（`Input.mousePosition` / `GetMouseButtonDown`，用于瞄准与
  世界点击）。它们能用是因为 Active Input Handling 是 **Both**（`activeInputHandler: 2`）；
  改成 "Input System Package (New)" 会让这些调用在**运行时抛异常**。迁移鼠标输入是独立的一件事。
- ⚠️ 动作名 `Alpha1/2/3` 描述的是**按键**而不是**意图**：新输入系统的价值在于可重绑定，
  一旦把槽位 1 绑到别的键，名字就变成假话（对比 `EPress` / `EscapePress` / `Move`）。
  改名要同时改 `.inputactions` 并让包装类重新生成，本次未做。
- 三个键、两个槽：当前没有角色能带 3 把（枪手 2 / 工程师 1，HUD 也只有两个按钮），
  按键 `3` 会被 `SwitchToSlot` 的越界判定拒绝（空操作）。留着是为了槽位数据化后不必回头加按键。

---

## 2026-10-08 · 武器系统审查整改（`Docs/WeaponSystemReview.md`）

按审查报告落实：把两处"静默故障"变成启动期红错、删掉已死的换装 API、收掉 `AttackDriver` 的残骸。
逐项状态与偏差见该文档 §6（含**未做项**：武器升级面板的动态列表改造）。

### `AttackDriver` 去槽化：一个实体一个攻击方式

- 删除 `AttackSlot` 类、`_runtimeSlots` 列表与各方法的 `index` 参数。多槽随"塔武器化"删除后
  只剩 1 个元素，却让 `_attack`（序列化字段）与 `_runtimeSlots[0].Method` 成了**同一件事的两个真相源**
  （`ResolveTarget` 读前者、`Tick` 读后者，靠 `SetAttack` 一处手工同步）。
- **字段名 `_attack` / `_prefab` / `_muzzle` 未动**，8 个武器 prefab 无需重接。
- ⚠️ **不要再把槽加回来**：多槽会让"这次开火用的是哪一套数值"重新变成需要推断的事，
  而武器化之后每把武器的数值本来就是独立的（Teto 的子弹与炮弹现在是两把武器）。

### 数值/资源体检：补上两个此前完全静默的来源

- `AttackMethodSO.RequiresPrefab`：需要投射物/环绕物的攻击方式漏配资源时，
  `AttackDriver` 在启动时报红错。此前表现是**武器永不开火且一条日志都没有**
  （`Execute` 拿到 null 只能空转）。
- 新增 `IStatRequirementsProvider`：**读它的组件声明它**（`BaseWeapon`、`TowerWeaponRangeSync`），
  由 `AttackDriver.ValidateRequiredStats` 统一校验。`RequiredStats` 只管攻击方式读的数值，
  载体自己逐帧读的（转速、射程）此前只能退化成 `GetStat` 的 `1f` 兜底 + 一条易被淹没的告警。
  校验点仍然只有一处 —— 这是没有让各组件自己报错的原因。

### 删掉运行时换装 API（`IWeaponManager`）

- 删 `AvailableSlots`（含 `_availableSlots` 记账）、`Unequip`、`SwapAsync`、`WeaponSlot.ReplaceConfig`。
  它们随局内 `ChooseWeaponPanel` 的删除失去全部调用方。
- **装备的唯一路径**：大厅 `RunSession`（武器台）→ `PlayerSpawner.EquipLoadoutAsync` → `EquipAsync`。
  要重新引入运行时换装，请**连入口 UI 一起设计**，不要再往接口上加无人调用的方法。
- `WeaponAssembler.Disassemble` 因此失去唯一调用方，**保留并标注**（装配的对称操作，重做换装时从这里接）。

### 装配语义收紧 + 并发闸

- `WeaponAssembler`：注入被拒（prefab 预接了别的 EntitySO）不再返回"跑旧数值"的实例，
  而是销毁半成品 + 归还句柄 + 返回 null —— 让「失败不会产生 `WeaponInstance`」成为真正的不变量。
- `WeaponSlot.IsAssembling` + `EquipAsync` 的 `try/finally`：并发装配会让后一次覆盖前一次的
  `Runtime`，被覆盖的实例既不销毁、句柄也不归还（"点两下"即可触发）。

### 塔武器声明服务端权威

- 4 个塔武器 prefab 的 `_authority` 由 `Local` 改为 `Server`（Teto 子弹/炮弹、Rin、Luo）；
  `TowerWeaponSetup` 新建时写入，并新增 `EnsureAuthority` 修复既有 prefab（工具可重跑）。
- `AttackAuthority` 写明**唯一的读取时机**：网络层在 spawn 之后读一次 `Authority` 来设置
  `HasAuthority`，不要有第二个写入方 —— 否则会出现"prefab 说 Server、运行时按 Local tick"的分叉。

### 表现与体检

- `BaseWeapon.SetActiveSlot` 一并切 `SpriteRenderer.enabled`：此前未激活的武器仍在渲染
  （两把已装备武器会同时画在手上，目前被"三把枪用同一张占位图"掩盖）。
  **只切 `SpriteRenderer`**：`LineRenderer` / 拖尾 / 粒子由各自的表现组件管状态
  （`BeamVisual` 的光束线平时是关的，一刀切打开会让它一直亮）。
- `EntitySOValidator` 新增武器体检：`attack` 非空、`RequiredStats ⊆ dataRef`、`prefab` 非空、
  `RequiresPrefab ⇒ 攻击资源已接`；成功文案带上"查了几把武器"，避免空循环伪装成通过。
- `SpinWeapon` 的尺寸升级改为推 `SpinWeaponController.SetTargetSize`（此前直接写 `localScale`，
  下一帧就被逐帧插值拉回旧尺寸 —— "升级了但当场没反应"）。

---

## 2026-10-03 · 推车模式第一阶段架构

把「无尽波次防守」改成「护送推车到下一个哨站」。下面只记**决策与理由**；
逐项进度与未完成项见 `Docs/CartPlan.md`。

### 跨场景：档案 / 出行配置 / 结果三者分离

- `PlayerProfile`（**落盘**：金币、解锁、当前哨站）与 `RunSession`（**不落盘**：本局角色、装备、种子）分开。
  合成一个会让"这一局的临时选择"被写进存档 —— 而它下次进大厅就该被清掉。
- `RunResult` 是 `readonly struct` 快照：结算面板与存档只消费它，不读可能已销毁的场景对象。
- 结算顺序固定 **冻结 → 计算 → 更新档案 → 保存 → 展示**：显示拿到奖励但重启后没有，比晚一秒看到结果严重得多。

### 关卡流程：阶段状态机 + 唯一胜负入口

- `StageDirector` 是阶段与胜负的**唯一权威**，`FinishRun` 幂等（Boss 死亡 / 抵达终点 / 全员阵亡可能同帧触发）。
- **推车耐久耗尽不是失败**（停摆 8 秒后恢复 50%）：否则一局会因为一次失误永久卡死 —— 既打不过去，也不结束。
- **第二个 Boss 之后先恢复行驶**，抵达终点才算胜利：在 Boss 死亡瞬间判胜会让"把物资运到哨站"凭空消失。
- 玩家死亡改为**全员检查**，并用 `_hadPlayers` 区分"玩家还没生成"与"全员阵亡"（否则开局第一帧就判负）。

### 敌人目标：注册表取代硬编码遍历

- 新增 `IEnemyTargetRegistry`：玩家 / 塔 / 推车在 `OnEnable` 注册，`EnemyTargetFinder` 只查这一份列表。
  原本硬编码"先找玩家、再找塔"，加推车就是第三次改同一个方法。
- 接触伤害判据从「白名单 `Player`/`Tower` tag」改成「**排除 `Enemy`**」：新增可攻击类型不必再回来改。
  ⚠️ 同时把 `GetComponent` 改成 `GetComponentInParent` —— 推车的碰撞体在子物体上，只查自身会静默打不到它。

### 生成：阶段化 + 可注入随机源

- `EnemySpawner` 从「无尽波次协程」重写为**累积器 + 四档压力**（Idle / Travelling / Stopped / CartDisabled）。
  **删掉了"等全场敌人清零才进下一波"**：Boss 战期间小怪一直在刷，清零永远不会发生，流程会直接卡住。
- 生成位置相对**推车**（锚点）的前后两侧，不围绕玩家：压力应当来自路线方向。
- 随机源是注入的 `System.Random`（种子来自 `RunSession`），**不用 `UnityEngine.Random`**。
- Boss **不走对象池**（`EnemyHealthController.UsePool = false`）：池化会让死亡事件与实例生命周期脱钩。
  它只认**这一只** Boss 的死亡事件，不用"全场敌人清零"判断。

### 成长、角色与塔

- 升级点从等级系统拆出：`IUpgradePointWallet`（`Balance` / `TrySpend` / `Grant` / `Refund`），
  `ExperienceLevController` 只管等级与经验。
  ⚠️ **`Refund` 不幂等** —— 幂等由调用方（塔的 `TowerLedger`）保证。
- 角色能力用 `[Flags] CharacterCapability`：枪手（武器升级 + 切换）、工程师（建塔 / 升级 / 拆除）。
- 武器支持 **2 个可切换槽位**，只有激活槽会攻击。停用必须停**召唤物**：
  `SpinWeapon` 的环绕物是发射点下的独立实例，不随武器组件的 `enabled` 一起停。
- **塔有投入账本**（`TowerLedger`，运行时 `AddComponent`，不挂 prefab）：记录 owner / 建造 / 升级累计，
  退款 `max(0, 总投入 − 1)` 且**幂等**。只有 owner 能拆，否则队友的投入会被别人一句话退掉。
  拆除前必须 `PrepareForRemoval()`：`Destroy` 帧末才生效，期间塔仍会攻击、仍被敌人当作目标。

### 场景接线踩坑（新增两条）

- **`PlayerSpawner` 之后，摄像机的跟随目标必须懒查找**：原实现在 `Start` 里取一次 `LocalPlayer`，
  而玩家是运行时生成的 → 相机一动不动，且不报任何错。
- 脚本化接线**场景对象**时用 public 字段直接赋值，不要走 `SerializedObject`（对象引用不落盘，
  `arraySize` 写了、元素仍是 null）。prefab **资产**上用 `PrefabUtility.LoadPrefabContents` + `SerializedObject` 是可靠的。

---

## 2026-10-02 · 验收复审整改（`Docs/CartArchitectureReview.md`）

复审列出的 P0/P1 全部处理，P2 处理了 4 项（第 5 项按复审意见留待 Profiler 数据）。
下面只记**决策与坑**。

### ⚠️ 备份只在"主档不存在"时才会被用（数据安全）

- **现象**：`profile.json` **存在但损坏**时直接退回默认档，从不看 `.bak`；
  紧接着的任意一次 `Save()` 又会用 `File.Replace` 把这份坏档写进 `.bak`。
- **后果**：玩家静默回到默认进度，而且**唯一能恢复的好备份被覆盖**，不可逆。
- **修法**：拆出 `TryReadProfile`（任何失败只返回 false，不回退默认档），
  `LoadOrDefault` 先读正式档、失败再读备份，二者都失败才建默认档；
  恢复时**复制**而不是 `Move`（备份必须留着，否则下次保存还是会把它覆盖成坏档）。

### 暂停必须带所有权（令牌集合）

- `IGameLevelManager` 的无参 `PauseGame`/`ResumeGame` 换成
  `AcquirePause(token)` / `ReleasePause(token)`：每个持有者拿自己的令牌申请一次、
  只释放自己那一次，集合空了才恢复 `Time.timeScale`。
- `BasePanel.WantsPause` 声明模态意图；**令牌由 `UIService` 按面板生命周期申请/释放** ——
  它才是面板生命周期的唯一所有者。不放在 `BasePanel.OnDestroy` 里是因为
  **子类写 `private void OnDestroy()` 会隐藏基类实现**（`LobbyHudPanel` 就漏了 `base.OnDestroy`），
  那样模态面板一旦漏写就会永久占着暂停。
- `LevelUpCoordinator` 另外持有一个令牌覆盖"面板 prefab 首次加载"的几十毫秒窗口；
  它与面板的令牌是**两个不同持有者**，所以先还回去不会把面板那次一起释放。
- `RunResultPanel` 也改成模态：本局已结束，背后的关卡不该继续刷怪、继续打车。
- `LevelUpPanel` 连升多级时**不再"关掉再开"**：那条路径中间会有一帧没有任何模态面板持有暂停
  （玩法推进一帧，还可能被别的面板插进来），现在改成原地换一批选项继续选。

### 重复 Manager 会顶掉服务入口

- `ManagerSingleton.Awake` 现在只有主实例才执行 `OnSingletonAwake()`（那一步会注册服务）；
  重复实例直接返回。归属判定抽成 `TryClaimInstance`（纯逻辑，可被 CLI 校验器断言）。
- 各 Manager 的注销统一改成 `ServiceLocator.UnregisterIfSelf<T>(this)` ——
  重复实例被销毁时无条件注销会把主实例的服务入口清掉，症状是"主实例还在、服务却空了"，
  且发生在切场景后一帧。
- 编辑模式下 `Destroy` 会被拒绝（并打红错），`InitializeSingleton` 改为
  `Application.isPlaying ? Destroy : DestroyImmediate`，否则重复实例在编辑器里根本不会被销毁。

### UI 面板加载失败会泄漏句柄

- `LoadPrefabInternalAsync` 的句柄原来声明在 `try` 内：资源返回 null 或 await 抛异常时
  句柄没进 `_prefabHandles`，也没人释放 —— 每重试一次引用计数 +1，bundle 永不卸载。
- 改为"句柄在 try 外 + `ownershipTransferred` 标记，未转交则在 `finally` 释放"。

### 池化复用入口的稳定分配

- `EntityStatModel.RemoveModifiersFromSource` 每次新建 `HashSet` + 为每个列表分配
  闭包与委托（`RemoveAll(lambda)`）。它在**敌人池取出路径上被调两次**（波次 + 哨站难度），
  按最高约 20 次/秒的生成压力就是持续 GC。
- 改为复用通知缓冲 + 倒序 `for` 原地删除。字典里每个 `StatType` 只出现一次，
  所以原来那个 `HashSet` 本来就是多余的。重入（订阅者在回调里又改修饰符）时退化成一次性列表。

### 其余

- **Laser 按实体去重**：一个敌人有多个碰撞体，`GetComponentInParent<BaseHealthController>()`
  会把它们解析成同一个血量组件 —— 不去重时一发扣两次血、`MaxHits` 也被重复消耗。
  与 `BulletController` 溅射的做法（`useTriggers = false`）不同，激光不能关掉 trigger
  （实体碰撞体可能比 trigger 小，会漏掉本该命中的目标）。
- **敌人贴近目标 / 目标消失时清零速度**：原来直接 `return` 会保留上一帧速度，
  敌人沿原方向滑行到下一次寻敌（0.5s）。
- **Boss 血量检查读的是兜底值**：`health.MaxHealth` 走 `BaseHealthController._entity`，
  而 `_entity` 要到 `Start` 才赋值 —— `ConfigureBoss` 早于 `Start`，读到的是"没有实体"分支的 100，
  等于什么都没检查。改为直接查 `controller.StatModel` 的 `HasStat/GetStat`。
- **伤害数字的重复日志**：`GetFromPool` 与 `SpawnDamageNum` 各打一条，密集命中时双倍开销。
  改为一次性告警，并区分"还在加载"（warning）与"加载失败"（error）。
- **经验球逐帧 `Vector2.Distance`**：改为 `sqrMagnitude` + 缓存玩家 Transform。
  拾取半径**不缓存** —— 它是可升级数值，缓存会让升级后已掉落的球继续用旧半径。
- **多槽目标类型校验**：索敌列表只按主槽 `TargetTag` 过滤，所以多槽必须打同一类目标。
  设计上确实禁止混用，就把这条约束变成启动期红错，而不是让其中一个槽静默打不到东西。
- **投射物池与句柄的所有权**：`ProjectilePool` 是静态的、比 `AttackDriver` 的句柄活得久。
  释放句柄前先 `ProjectilePool.RetainsInstancesOf` 判断，有池化实例就先 `DropPool`
  再释放 —— 顺序反了会让池中实例的 Sprite/材质变成空壳（"子弹看不见"）。
- **Addressables 内容构建入口**：新增 `AddressablesSetup.BuildContentFromCommandLine`。
  地址建好不等于打得出来（条目漏进组、AssetReference 指向未打包资产在编辑器下全部正常）。

### 新增自动断言

`CartLogicVerifier` 从 78 项增加到 **104 项**，新增：
主档损坏 + 有效备份的恢复（含"备份必须保留"）、暂停令牌的所有权语义、
重复 Manager 的归属判定与注销语义。

⚠️ 写这些断言时踩到一个**校验器自身的坑**：编辑模式下 MonoBehaviour 的 `Awake` **不会执行**
（除非标了 `ExecuteAlways`），所以"AddComponent 后观察是否注册服务"只会得到一个永远通过的假象。
改为直接断言抽出来的纯逻辑（`TryClaimInstance` / `UnregisterIfSelf`）。

---

## 2026-10-02 · 清单复核整改（P0 流程 / P1 事务 / 新内容）

按 `Docs/CartArchitectureChecklist.md` 的顺序整改。下面只记**决策与踩到的坑**。

### ⚠️ 存档从来读不回来（既有 P0，本次才发现）

- **现象**：`Save()` 写出的 JSON 完好，但每次读档都抛
  `Cannot set a constant field`，被 `catch` 成"档案损坏"→ 静默退回默认档。
- **后果**：金币、武器解锁、哨站进度**每次重启全部归零**，磁盘上的存档其实是好的，
  线索只有一条 `LogError`。
- **根因**：`PlayerProfile.CurrentSchemaVersion` 是 `public const`。LitJson 的
  `AddTypeProperties` 用 `type.GetFields()` 取属性表 —— 它默认**同时返回实例字段与静态字段**，
  const（`IsLiteral`）与 `static readonly` 都在内。它们写不回去（const 抛异常、
  readonly 抛 `FieldAccessException`），而失败的不是"少读一个字段"，是**整个对象反序列化失败**。
- **修法**：`JsonMapper.AddTypeProperties` 跳过静态字段与静态属性（静态成员不属于实例数据）。
  **不要**靠"把 const 改成 internal"绕过：下一个往数据类里加 `public const` 的人会重新踩进来。
- **护栏**：`CartLogicVerifier` 里有一条断言检查存档 JSON 中不含 `CurrentSchemaVersion`，
  以及"写入的金币能读回来"。

### 胜负只有一个入口

- 删掉 `GameLevelManager.NotifyPlayerDied → GameOver → DeadPanel` 整条旧路径（含 `IGameLevelManager`
  上的 `GameOver` / `OnGameOver` / `IsGameOver`），`DeadPanel.cs` 与 `DeadPanel.prefab` 一并删除。
- 玩家死亡只做一件事：对象销毁 → `PlayerController.OnDisable` 注销 → `StageDirector` 读到
  "一个玩家都不剩" → `FinishRun`。**判据只有 `PlayerManager.AllPlayers`**。
- 为什么必须删干净：两个结束入口并存时，单人局会同时弹死亡面板与结算面板，多人局第一个人阵亡就结束全队。

### 传送门：显式状态机 + 会话锁定时机

- `PortalController` 增加 `Idle / CountingDown / Departing` 三态。`Departing` 是**终态**：
  `LoadSceneAsync` 是异步的，没有这个状态时倒计时归零后会**逐帧重复发起场景加载**。
- **锁定移到"倒计时开始"那一刻**（原来锁在 `Depart` 里）：倒计时期间玩家仍能换装备，
  而关卡读的是出发那一刻的值 —— 界面与关卡里实际拿到的武器不一致，只在进关卡后暴露。
- 取消倒计时 → `Unlock()`，玩家能回大厅重新配置。
- 出发判据只有一份：`IRunSessionService.IsReadyToDepart`（角色 / 关卡 / 装备）。
  传送门不再自己检查角色 —— 三处各写一份检查，漏一处就是"空装备也能出发"。
- 站圈里配置不全时的告警按**原因去重**，否则会逐帧一条 `LogWarning` + 字符串插值。

### 升级三选一：入口 + 关闭规则

- 新增场景级 `LevelUpCoordinator`（挂在 `Level0.unity`）：订阅本地玩家的
  `PlayerProgressionController.ChoicePending` 打开 `LevelUpPanel`，玩家切换/场景销毁时退订。
  之前 `ChoicePending` **只有触发、没有订阅者**，升级只累计计数、永远不弹面板。
- `IPlayerManager` 增加 `LocalPlayerChanged`：退订旧玩家是"谁负责"的问题，靠每帧比对是隐式的。
- `LevelUpPanel` 在有待选次数时**拒绝关闭**（ESC 与关闭按钮都提示而不是丢弃）——
  连升三级只选一次会静默吞掉两次成长。
- 消费掉一个选项后必须 `RerollPlayerOptions()`：不换一批的话刚买过的那项还在池子里，
  连升三级能把同一个升级叠三次。

### Boss 必须注入关卡选定的 EntitySO

- `BossEncounter.SpawnBoss` 在 `Instantiate` 后调 `SetEntityConfig(so)`，并校验
  `EnemyController` / `EnemyHealthController` / `StatModel` 就绪；失败则**销毁半成品实例**。
- Snake / Wolf 的 prefab 上 `entityConfig` 是空引用（它们是"内容"不是"配置"）。不注入时
  StatModel 建不起来，所有数值走 `GetStat` 的默认值 1 —— Boss 一枪就死，且没有任何报错。
- 注入之后 `MaxHealth <= 0` 也算失败：那说明 DataSO 漏配，关卡同样推进不下去。

### 武器槽编号：候选下标 ≠ 已装备序号

- `PlayerWeaponController` 增加 `EquippedSlots` / `ActiveWeapon` / `ActiveEquippedSlot`，
  按键 `1/2` 与 HUD 按钮都映射到**第 1/2 把已装备的武器**。
- 旧实现直接拿按键当**候选下标**：候选 Gun/Spin/Cannon、实际装备 Gun/Cannon 时按 2 会访问候选
  第 2 项（Spin，未装备）→ 切换被拒 → 表现为"Cannon 切不过去"，且不报错。
- `HasAimWeapon` 只看**激活武器**：查全部已装备武器会让"枪 + 火球"时永远显示瞄准摇杆。
- `WeaponUpgradePanel` 升级 `ActiveWeapon`（原来取"第一把已装备的"，按 2 切过去后升级仍打在另一把上）。
- 删掉 `_weapons` / `RegisterWeapon` / `UnregisterWeapon`（与 `_equippedSlots` 是两份真相）。

### 塔放置：一次性事务

- 新增 `TowerPlacementTransaction`（`Loading → Ready → Committed/Cancelled`，退款只认第一次迁移），
  记录**付款人本身**而不是"退款时的 LocalPlayer"（异步期间它可能已经换人/销毁）。
- `ConfirmPlacement` **先提交再实例化**：同帧双击第二次会因状态已终结而被拒绝，只生成一座塔。
- 四条终结路径（确认 / 取消 / 加载失败 / 切场景销毁）全部走同一个 `TryCancel()`：
  旧实现只在取消按钮里退款，加载失败与切场景**不退**（点扣了、塔没出来），
  而取消与销毁兜底又可能各退一次（点数凭空变多）。
- `InitAsync` 失败时**自己**收尾（退款 + 归还幽灵），调用方不要再 `ReleaseInstance` ——
  幽灵的实例归还只有 `_ghostReleased` 一个判据，两处各还一次会让引用计数被多减。

### 死亡与接触伤害幂等

- `BaseHealthController` 增加 `IsDead`：死亡是一次性事件。没有它时同一帧的多颗子弹会走多次
  `Die()` —— 击杀点发多份、经验掉多份、统计多记（Boss 不走池，帧末销毁前能被打很多次）。
  **池化取出必须复位**（`ResetHealth`），否则复用出来的敌人一出生就是死的、打它不掉血。
- `EnemyHealthController` 的接触列表改为**只存碰撞体、结算时再归一化去重**：
  一个目标（推车/塔）有多个子碰撞体时，旧实现按 `other.gameObject` 记录会把它当成多个接触对象
  （每次周期结算多受一份伤害），而退出其中一个时又只移除一条记录。
  不用"以目标为键的字典"是因为目标被销毁后会留下无法清理的残留键。
- 塔击杀归属接 `TowerLedger.Owner`：塔不是玩家的子节点，`GetComponentInParent<PlayerController>`
  永远找不到建造者 —— 塔的击杀全部落进"只记团队击杀"，玩家只会觉得"我的塔白打了"。
  owner 已离场时仍然只记团队击杀，**不退化成发给本地玩家**。

### 档案：原子写入 / 购买事务 / 离场补试

- `Save()` 改为 `File.Replace`（原子替换 + 保留 `.bak`），平台不支持时退化为
  「正式 → 备份 → 临时 → 正式」。**顺序不能反**：旧实现 `Delete(正式) → Move(tmp, 正式)`
  两步之间进程退出就两份都没有了。读档时若正式文件缺失而备份存在则**从备份恢复**。
- `TryUnlockWeapon` 只在**保存成功**时返回 true，失败回滚金币与解锁状态。
  旧实现"保存失败也返回 true"会让购买界面显示成功、重启后钱和武器一起消失。
- 结算保存失败的补试统一走 `RunSettlement.RetrySaveIfNeeded`，由 `RunResultPanel.Leave()`
  在**所有出口**（返回大厅 / 返回菜单 / ESC）调用 —— 只在"重开"按钮上重试，
  会让从其它出口离开的玩家静默丢掉本局奖励。

### 出行会话：受控写入取代约定式锁定

- `IRunSessionService.Current` 改为**只读快照** `RunSessionSnapshot`，写入走
  `TrySetCharacter / TrySetLoadout / TrySetStage / TryLockForDeparture / Unlock`。
  旧实现把可变对象整个交出去，"锁"只是注释里的约定 —— 任何调用方都能在倒计时期间改角色。
- `TrySetCharacter` **原子化规范装备**：按角色的 `allowedWeaponIds` 清理、按 `maxWeaponSlots`
  截断、空了补 `defaultWeaponId`。`CharacterDefinitionSO` 因此新增 `allowedWeaponIds`
  （空 = 不限制）。切角色不换装备的表现是"工程师带着枪出门"，而枪他永远升不了级。
- 所有 `TrySetXxx` 失败时**不改动任何字段**（全有或全无）。

### 组合根：关键服务 vs 可降级服务

- 关键服务（Asset / UI / Profile / RunSession）失败 → `Ready` **fault**，场景入口
  （`Main` / `LobbyDirector` / `PlayerSpawner` / `GameLevelManager`）用
  `GameBootstrap.TryWaitReadyAsync()` 拿到 false 后停止推进；`Main` 把失败原因显示出来。
- 音频是**可降级**服务：失败只静默继续，并留一条明确的降级日志。
- 旧策略"任一服务失败都不阻塞其余服务"对**所有**服务成立，于是档案初始化抛异常时
  `Ready` 照样成功，大厅按默认档跑 —— 玩家看到的是进度凭空消失。
- 光看"没抛异常"不够：`UIService` 内部会把加载异常消化成日志后正常返回，
  所以组合根额外校验 `UIService.IsOperational`（画布是否真的就位）与 `Profile != null`。

### 性能

- `BaseWeapon.SetActiveSlot` 增加 `IsActiveSlot`，`GunWeapon` / `SpinWeapon` 的 `Update`
  快速返回：停用只关 `AttackDriver` 不够，备用枪仍会逐帧读输入抢瞄准、备用火球仍会逐帧写 Transform。
- `GamePanel.Update` 先判 `IsShown`（面板被隐藏后 GameObject 仍在 UIService 缓存里，否则永远空转）；
  FPS 文本只在编辑器 / Development Build 刷新，Release 一次性关掉（周期 `ToString` + TMP 重建）。
- `TowerPlacementController` 缓存主摄像机（拖动期间不再逐帧 `Camera.main`），
  并把节流条件修成**真正的固定频率**（旧写法"累计超过 0.1 秒就 return 一次"只跳过一帧）；
  输入轮询仍保持逐帧，否则会丢点击。

### 新内容

- **塔升级改为确定性定向列表**：`IPlayerUpgradeController.GetTowerOptions`（原 `RollTowerOptions`
  是随机三选一，"想升的那项一直抽不到"）。`TowerLevelUpPanel` 不再继承 `BaseOptionPanel`
  （固定三槽装不下 Teto 的 5 项），改为自带可滚动列表 + `TowerOptionItem` 行模板；
  prefab 由 `CartUiSetup` 生成。
- **HUD 武器槽按钮**：`GamePanel.btnWeaponSlot1/2` + `SwitchWeaponSlot`，
  与键盘 `1/2` 共用同一条 `IWeaponManager.SwitchToSlot`；高亮订阅 `ActiveSlotChanged`。
- **Cannon / Laser**：`CannonWeaponDataSO`（多一个 `SplashRadius` → `ShellExplosionRadius`）、
  `LaserWeaponDataSO`（只有伤害与速率）、`Attack_Cannon`（复用 `ProjectileAttackSO` + 溅射）、
  `Attack_Laser`（复用已有的 `BeamAttackSO`）+ `BeamVisual`（`LineRenderer` 闪一下）。
  资产与 prefab 由 `CartWeaponSetup` 生成，并自动接进枪手白名单与玩家候选列表。
  ⚠️ 美术是**占位**（复用枪的图标），等美术资源到位后替换。
- **哨站难度**：`OutpostDifficulty.Apply(StatModel)` 注入 `MaxHealth`/`Damage` 的乘算 modifier，
  来源标记固定 → **幂等、池化复用不累计**。Boss 不走池，所以 `BossEncounter` 也要单独调一次，
  否则"小怪随哨站变强、Boss 还是老样子"。

### 跨场景残留（顺手修掉的两条）

- **关卡 HUD 要显式隐藏**：`GamePanel` 挂在 `DontDestroyOnLoad` 的画布上、实例被 `UIService` 缓存，
  不隐藏会跟着玩家进大厅/菜单继续显示上一局的等级、时间与摇杆。结算出口（`RunResultPanel.Leave`，
  三个出口共用）与 `GameSettingPanel` 的重开按钮现在都会收掉它。
- **HUD 绑定必须在每次显示时重做**：面板实例跨场景复用 → `Init` 只跑一次 →
  第二局开始时 HUD 仍绑在**上一局那个已被销毁的玩家**身上，等级/经验/升级点全部停在旧值且不报错。
  改为 `GamePanel.ShowMe()` 里重新 `Bind`，`PlayerHudBinder.Bind` 第一步退订旧玩家。

### 序列化分层：删掉 `JsonMgr`，不再另写 Json 管理类

- 现状澄清：存档走的是 **LitJson 的裸 API**（`PlayerProfileService` 里直接 `JsonMapper.ToJson` /
  `ToObject<T>`），项目里那个 `JsonMgr` 封装**一次都没被调用过**，已删除。
- **决策：不再写通用的 JsonManager。** 需要被"封装"的不是序列化，而是**落盘**：
  原子写入、备份恢复、版本迁移、损坏修复、失败可见 —— 这五件事 `PlayerProfileService` 已经做了，
  而 `JsonMgr` 一件都没有（裸 `File.WriteAllText` 覆盖、无版本、无修复、`SaveData` 返回 `void`
  连失败都传不出去）。
- 再加一层通用 Json 层的代价是**造出第二条"怎么落盘"的路径**，与「服务是唯一入口」的约定冲突
  （`IPlayerProfileService` 的文档已明文禁止业务代码自己碰 JSON 文件）。
- 附带清掉一个名字歧义陷阱：`JsonMgr.cs` 在**全局命名空间**声明了 `public enum JsonType`，
  与 `LitJson.JsonType` 同名 —— `using LitJson;` 之后写 `JsonType` 会解析到哪一个并不直观。
- 将来真需要第二个落盘对象（独立的设置文件、对局历史）时：**复用原子写实现**，
  各自仍只有一个写入入口，而不是新开一个 JsonManager。

### 自动验证

- 新增 `CartLogicVerifier`（`Tools ▸ Verify Cart Logic`，
  `-executeMethod CartLogicVerifier.VerifyFromCommandLine`，失败退出码 1）：78 项断言，
  覆盖奖励计算、出行会话状态迁移、塔放置事务一次性、档案原子写入与备份恢复、哨站难度幂等。
  **它抓到上面那条"存档读不回来"**。
- 为什么不是 NUnit EditMode 测试：本工程没有 asmdef，游戏脚本全在预定义程序集 `Assembly-CSharp`，
  而 asmdef 化的测试程序集**无法引用预定义程序集**；要加就得先把 `Assets/Script` 拆成 asmdef，
  那又会连带要求给第三方源码目录（Joystick Pack）补 asmdef —— 为测试基础设施改第三方资产不划算。
  本工程既有的自动校验（`EntitySOValidator` / `SoFieldAudit` / `AddressablesSetup`）走的就是
  「CLI 校验器 + 退出码」，这里延续同一套。
- `CartUiSetup` / `CartWeaponSetup` 是幂等的资产生成器：手拖 prefab 不可复现也无法 review，
  而漏接一处（例如 EntitySO 没进候选列表）的表现是"买得到但装不上"，不报任何错。
- `AddressablesSetup` 增加**清理失效条目**：本脚本只增不删，删掉一个 prefab 后会留下
  永远解析不到地址的孤儿条目，让 Analyze 的"缺失地址"告警一直挂着。

---

## 2026-10-02 · 武器 / 塔 / SO 数据层架构改造

把武器系统从「场景节点 + 硬编码流程」改成「数据驱动 + 可替换执行器」，并统一了塔与全库 SO 的数据层。
下面按**决策**归类，每条带「为什么」与「改的时候要注意什么」。

### 攻击方式：SO 策略 + driver

- **怎么打**在 `AttackMethodSO` 子类资产（`GetInterval` + `Execute`），**什么时候打 / 打谁 / 拿什么打**在 `AttackDriver`。新增攻击手段 = 建资产挂上去，零代码。
- **`Execute` 必须返回「这次是否真的打出去了」**，driver 只在 true 时发 `OnPerformed` —— 否则塔会对着空气挥动画。
- **「打谁」（`TargetTag`）与「读哪些数值」（`StatType` 字段）都在 SO 上**，不在 driver 上：否则给塔换上治疗技能后，driver 仍只找 `Enemy`，治疗技能会去治敌人。
- **`RequiredStats` 是机器契约**：声明本攻击方式读取的全部数值（含 `GetInterval` 读的），`AttackDriver.Start` 逐项核对并报错。缺一个数值会静默退化成 `1`。
- **SO 无状态、不持有资源**：计时器 / 目标列表 / 句柄全在 driver。SO 是常驻资产，没有可靠的 `OnEnable`/`OnDisable`。
- **发射点只有一处配置**（`AttackDriver._muzzle`）。两处各配一个迟早会漂移，而漂移是静默的。
- **动画音效走 `AttackDriver.OnPerformed`**，由载体（`BaseTower` / `GunWeapon`）订阅后自己播。

### 速率语义：攻击节奏用「次/秒」

- `StatType.AttackSpeed` / `HealSpeed` 单位是**次/秒**（越大越快），`GetInterval` 内部做 `1/speed`，并封顶 20 次/秒。
- ⚠️ **不要再引入任何「间隔秒数 / 冷却秒数」型数值**：`Add` 型升级会把间隔**加大** = 变慢，数据层面完全察觉不到（`ShootGunInterval` 的「增加攻击频率」曾经实际让射速减半）。
- ⚠️ 编号 **13（旧 AttackInterval）与 16（旧 HealInterval）** 是这两个数值留下的空洞，**禁止复用**。

### 伤害与目标

- **伤害必须带来源**：`TakeDamage(in DamageInfo)`，`DamageInfo` = `Amount`/`HitForce`/`Attacker`/`Source`。新增攻击方式或投射物必须把 `ctx.Self` 传下去（击杀归属 / 统计 / 联机反作弊都靠它）。`readonly struct` + `in` 传参，命中路径零分配。
- **范围攻击遍历 driver 拍的快照**（`ctx.TargetsInRange`），不要遍历原列表：打死目标会让它回池、碰撞体失效，随后触发 `OnTriggerExit2D` 去改原列表 → 越界。

### 装配：武器与塔都是「按需加载的 prefab」

- 武器 `WeaponEntitySO.prefab` → `EquipAsync`（`LoadAssetAsync` + 同步 `Instantiate`），句柄按「玩家 × 已装备武器」持有并归还。
- 塔 `TowerEntitySO.prefab` → `TowerPlacementController.InitAsync` 加载（幽灵外观与最终放置共用同一份），句柄在幽灵销毁时归还。**放置流程因此是异步的**：prefab 未就绪时 `ConfirmPlacement` 忽略本次点击，**不能退还点数**（那会让点数凭空增加）。
- 两者都**不要**改用 `InstantiateAsync`（要求逐实例 `ReleaseInstance`，且与 Mirror 的 Spawn/UnSpawn 生命周期冲突）。
- **装配方注入 EntitySO**（`EntityBehaviour.SetEntityConfig`，在 `Instantiate` 之后、`Start` 之前）：prefab **不必**再反指自己的 SO。已是同一份配置时静默跳过；StatModel 已建立则告警拒绝（重建会让子类订阅指向旧实例）。
- **多攻击槽**（`_extraAttacks`，各自独立计时）：⚠️ 主槽必须保持 `_attack` / `_prefab` / `_muzzle` 三个**单值字段**，合并进 List 会让既有 prefab/场景配置变成孤儿键、静默丢失。
- **UI 动态槽位**：`ChooseWeaponPanel` 按候选数量克隆槽位；`img` / `txt` / `btn` 在 prefab 里是**平级**节点，三者都要克隆。

### 联机接缝

- **生成走 `IAttackSpawner`**（`AttackContext.Spawner`，由 `AttackDriver` 实现）：有父级 = 召唤物（普通 `Instantiate`，随载体销毁）、无父级 = 投射物（走 `ProjectilePool`）。攻击方式**不要**自己 `Instantiate` —— 联机时这一处换成 `NetworkServer.Spawn`，策略类与资产一行不用改。
- **计时有权威归属**：`AttackDriver._authority`（`Local` / `Server`）+ `HasAuthority`（默认 true），不通过就不 tick。玩家武器 = `Local`，塔 = `Server`（否则两端各结算一份伤害、敌人血量分叉）。
- **随机不用 `UnityEngine.Random`**：进程级全局状态，联机各端各抽各的、无法复现。`UpgradeSelector` 用可注入的 `System.Random`。

### 对象池

- **投射物走 `ProjectilePool`**（按 prefab 分池、上限 64、接 `ResetStatics`）：`BulletController` 是 `IPoolable`，超时与命中都**归还**而不是 `Destroy`；移动由 Kinematic `Rigidbody2D.linearVelocity` 驱动（prefab 上必须有刚体 + `Continuous`）。
- ⚠️ 复用实例带着上一发的残留状态：`OnGetFromPool` 要复位，`ProjectileAttackSO` 里 **scale 每次都要重设**（上一发可能是炮弹 Scale 3）。

### UI 与输入解耦

- **瞄准方向由输入层给**（`IInputHandle.TryGetAimDirection`）：武器不要判断平台、不要取 `Camera.main`。`GunWeapon` 曾经的 `#if UNITY_STANDALONE_WIN / #elif UNITY_ANDROID` 在别的 Build Target 下**两个分支都不成立** → 方向恒为零、且不报错。
- 武器选择面板的候选**只能用 `IWeaponManager.AvailableSlots`**：用 `WeaponSlots` 会把已拥有的武器也列出来，玩家点了没反应还白关一次面板。
- 删掉了 `MobileInputDriver`、触摸缓存与 `NullInputHandle` 的本地回退；网络玩家用 `NullInputHandle`。

### 数据层统一

- **实体 SO 就是它自己的配置单**：展示信息与 prefab 引用都放实体 SO。为此删掉了 `WeaponSelectSO` —— 它只装两个展示字段，却让每把武器多一个资产，还把关联拆到两处（容易误以为「它应该指向 prefab」）。
- **标识符规范**（判据：**这个值会不会被读到**，不要一刀切）：

  | 类别 | 例子 | 规则 |
  |---|---|---|
  | 落盘且被读取的枚举 | `StatType`、`EModifierType` | 显式赋值、只追加、删除留编号空洞 |
  | 只当名字用的枚举 | `ResourceEnum`（地址拼 `Music/<成员名>`） | 值不承载语义，中段增删安全；**重命名成员要同步重命名资产** |
  | 跨边界稳定标识 | `BaseEntitySO.id` | `^[a-z][a-z0-9_]*$` + 类型前缀；**只用于跨边界**（存档/联机），不做运行时查找键 |
  | 资源引用 | prefab / sprite / clip | 优先 `AssetReference`（GUID）；地址字符串只用于 `UI/<类名>` 与 `Music/<枚举名>` |

- **两个只读体检器**：`EntitySOValidator`（id 唯一/格式/前缀、`dataRef`、升级有效性）、`SoFieldAudit`（**孤儿字段** —— Unity 会静默保留已删除的键，它们看起来能改其实没用）。删/改 SO 类字段后跑一次 `SoFieldAudit`。

### 迁移注意（一次性动作已完成，留档备查）

- `AttackInterval` → `AttackSpeed`（语义反转，旧值取倒数）、`HealInterval` → `HealSpeed`；13 个升级资产的目标数值已修正。
- `WeaponEntitySO.prefab` / `TowerEntitySO.prefab` 改为 `AssetReferenceGameObject`：⚠️ **AssetReference 要求目标必须在 Addressables 组里** —— `Weapon` / `Tower` 组里的条目不能删。
- `Level0.unity` 的武器场景节点已删除，改为运行时装配；`Weapon_Gun.prefab` / `Weapon_Spin.prefab` 由原场景节点提取而成。
- 全库清理了 11 个资产的孤儿字段（`EntityName` ×8、`Damage`/`AttackInterval`、`targetTags` ×3）。
- ⚠️ **Teto 现在每 5 秒同时发射子弹与炮弹**（`_extraAttacks`）；清空该列表即可退回只发子弹。

### 未验证 / 待确认

- 以上改动**全部未经 Play 验证** —— 批处理只能证明编译与资产接线。重点确认：武器装配与射击、塔放置（流程已异步化）、Teto 双攻击、UI 动态槽位、升级是否真的生效。
- `Enemy_Default` / `Snake` / `Spoil` / `Wolf` 有 DataSO 与 prefab、但没有 `EnemyEntitySO`，进不了 `EnemySpawner` 的波次配置。
- `EnemyEntitySO.prefab` 仍是 `GameObject` 直接引用；玩家的候选武器列表仍配在场景组件 `PlayerWeaponController._candidates` 上。

---

## 2026-09-29 · 攻击方式 SO 化（第 3 步：武器侧）

**现象**：
- `GunWeapon` 与 `SpinWeapon` 各自实现「计时 + 生成投射物」，而塔侧的 `Teto` 是同一段代码 —— 两条线无法共用，想给玩家换攻击方式必须改类。
- **`BaseWeapon.TryFire` 的告警没有节流**：它被 `Update` 逐帧调用，`interval <= 0` 时每帧一条 `LogWarning` + 字符串插值，正踩在性能红线上（`BaseTower.TryOperate` 有 `_intervalWarned`，武器侧漏了）。合并到 `AttackDriver` 后这个缺陷自动消失。
- 武器的瞄准/自转与攻击逻辑混在同一个 `Update` 里，职责不清。

**修法**：
1. 新增 `OrbitAttackSO`（环绕物）：生成物挂到发射点下，`GetInterval` = `AttackInterval + SpinWeaponLifeTime` —— 与旧 `SpinWeapon.GetFireInterval` 完全一致（这条耦合是故意的，不要「顺手优化」）。
2. `BaseWeapon` 删掉 `TryFire` / `GetFireInterval` / `GetAttackInterval` / `GetBaseDamage` / `CanOperate`，只剩身份：`Owner` + 武器槽注册。
3. `GunWeapon` 只留输入与瞄准：方向写进 `driver.AimDirection`，音效走 `driver.OnPerformed`。
4. `SpinWeapon` 只留自转与尺寸同步。
5. `AttackDriver` 新增 `Muzzle` 只读访问器。
6. `Level0.unity` 的 `Weapon_Gun` / `Weapon_Spin` 加 `AttackDriver`，配好攻击方式、资源与发射点。

**关键决定：发射点只有一处配置**
`GunWeapon.firePoint` 与 `SpinWeapon.SpinWeaponPosition` **都删掉了**，统一由 `AttackDriver._muzzle` 持有。两处各配一个 Transform 迟早会漂移（改了一边忘了另一边，投射物就生成在错的地方），而漂移是静默的。`SpinWeapon` 通过 `driver.Muzzle` 读它来遍历环绕物做尺寸同步。

`Muzzle` 返回**配置值本身（可能为 null）**，不返回会退回 `transform` 的 `Origin` —— 后者会让「忘了配发射点」变成一个看不出来的默认行为。

**迁移注意**：
- 删字段后，场景里的序列化值变成 **`SerializedObject` 读不到的孤儿键**（字段不在类里，`FindProperty` 返回 null）。所以迁移脚本改用**子物体名**定位发射点（`GunWeaponPosition` / `SpinWeaponPosition`，都是武器根节点的直接子物体）。**以后做这类「先删字段再迁移」的操作要记住这一条** —— 要么先迁移再删字段，要么只能用名字/路径定位。
- 场景没有可回退的基线（`Level0.unity` 在本次改动前相对 HEAD 就已经是 modified），所以迁移前把原文件复制到了 `Logs/Level0.before-step3a.unity`（`Logs/` 在 `.gitignore` 里）。

**验证（实际输出）**：
```
[WeaponAttackCheck] === Weapon_Gun：攻击方式=Attack_Gun 发射点=GunWeaponPosition 资源=Weapon/Bullet 间隔=1
  Damage=10 / BulletSpeed=20 / BulletHitForce=20 / AttackInterval=1
[WeaponAttackCheck] === Weapon_Spin：攻击方式=Attack_Spin 发射点=SpinWeaponPosition 资源=Weapon/Spin 间隔=5
  Damage=5 / SpinWeaponSize=1 / SpinWeaponLifeTime=4 / HitPushForce=20 / AttackInterval=1
```
`Weapon_Spin` 的间隔 **1 + 4 = 5**，正好等于旧 `SpinWeapon.GetFireInterval` 的结果 ✓
批处理编译 `*** Tundra build success`、0 个 `error CS`；`[EntitySOValidator] 通过：9 个 EntitySO` + `CLI_OK`；场景里两个武器对象各有 `GunWeapon`/`SpinWeapon` + `AttackDriver`，`_muzzle` 的 fileID 与迁移前的 `firePoint` / `SpinWeaponPosition` **完全相同**；全场景 0 个 missing script。

**运行时表现需 Play 验证**：枪照常朝鼠标/摇杆方向射击 + 播 `PlayerShoot`；火球照常环绕、尺寸随升级变化、间隔 5 秒；`SpinWeapon` 转速为 0 时不写 Transform。

**没做**：数值模型重构（攻击方式 `statBonuses` 注入实体模型）与 `IUpgradeTarget`、武器不再是 `EntityBehaviour` —— 那是第 3b 步，会波及全部升级资产，单独做单独验。

---

## 2026-09-29 · 攻击方式 SO 化（第 2 步：Rin + Luo 迁移，`BaseTower` 瘦身）

**现象**：
- Step 1 只接了 Teto，Rin/Luo 还是 `Xxx : BaseTower` 子类 ——「塔有确定的攻击模式」这条只解掉了三分之一：**同一套攻击方式无法在塔之间互换**（想把范围伤害挂到 Teto 上做不到）。
- `BaseTower` 里同时存在**两套索敌**：它自己的 `enemyInRange` + `OnTriggerEnter2D`，和 `AttackDriver` 的目标列表。对已迁移的 Teto 来说，前者是纯粹的逐帧死代码。
- `Luo.cs:21-31` 靠「遍历子物体的 Animator、看哪个有 `Heal` 参数」**猜**出治疗动画挂在子物体 `Bao` 上 —— 改个参数名就静默失效。
- `BaoController` 是死代码：全库无调用方，所有 `.anim` 的 `m_Events` 都是空的（不是 AnimationEvent 调的），而且它 `SetTrigger("Attack")` 的参数在 `Bao.controller` 上**根本不存在**（那个 controller 只有 `Heal`）。

**后果**：塔的攻击方式仍然焊在类型上；`BaseTower` 293 行里有一半对已迁移的塔毫无作用。

**修法**：
1. 新增 `AreaDamageAttackSO`（范围内全体伤害）与 `HealAlliesAttackSO`（群体治疗，`TargetTag => "Tower"`）。
2. **`AttackMethodSO.Execute` 改为返回 `bool`** —— 只有真的打出去了 driver 才发 `OnPerformed`。
3. `AttackDriver` 新增 `OnPerformed` 事件与**目标快照**（`BuildContext` 里把 `_targetsInRange` 复制进复用的 `_targetsSnapshot`）。
4. `BaseTower` 删掉索敌、周期行为骨架、目标列表工具（**293 → 178 行**），新增攻击表现字段（`_attackAnimator` / `_attackTrigger` / `_playSfx` / `_attackSfx`）并订阅 `OnPerformed`。
5. `Tower_Rin.prefab` / `Tower_Luo.prefab` 从「Rin/Luo 子类」改成 **`BaseTower` + `AttackDriver`**；删 `Rin.cs` / `Luo.cs` / `BaoController.cs`。

**关键决定**：

- **`Execute` 必须返回「是否真的打出去了」**：旧 `Rin.OnOperate`/`Luo.OnOperate` 都是「范围内没目标就直接 return，不播动画」。如果 driver 无条件发 `OnPerformed`，塔会对着空气挥动画。返回 `bool` 让这条行为原样保留。
- **快照遍历由 driver 提供，而不是让 SO 自己 `ToArray()`**：`BaseTower.ForEachValidTarget` 当年就是因为「范围攻击打死敌人 → 回池 → 碰撞体失效 → 塔收到 `OnTriggerExit2D` 改列表 → 按索引遍历越界」才改成快照的（见 2026-09-21 条目）。这个保护现在必须在 driver 侧，而且要用**复用的 List** 复制（`AddRange` 到已扩容的 List 不分配），不能让每次攻击都 `ToArray()`。
- **动画/音效走事件而不是塞进 SO 或 driver**：放进 SO 会让常驻资产持有 animator 参数名与音效枚举；放进 driver 又让「攻击执行器」承担表现职责。由 `BaseTower` 订阅 `OnPerformed` 自己播，顺带把 Luo 那个「猜 Animator」的脆弱查找换成 Inspector 直接拖。

**迁移注意**：
- **`EntityBehaviour` 没有声明 `OnDestroy`**，所以 `BaseTower.OnDestroy` 是普通私有方法而不是 `override` —— 第一版写成 `protected override void OnDestroy()` 直接 `CS0115`。
- `Tower_Luo.prefab` 的 `Bao` 上原有 `BaoController` 组件，删脚本后它变成 missing script；迁移脚本显式清掉了（没有依赖 `RemoveMonoBehavioursWithMissingScript` 是否覆盖子物体）。
- Rin/Luo 的 `AttackDriver._prefab`（投射物资源）留空 —— 范围伤害与治疗都不需要投射物，`ctx.Prefab` 返回 null 时两个新 SO 都直接返回 false 空转。
- `Attack_TetoBullet.asset` / `Attack_TetoShell.asset` 不受影响：`Execute` 返回 `bool` 是签名变化，资产里没有对应字段。

**验证（实际输出）**：
```
[TowerAttackCheck] === Tower_Rin：攻击方式=Attack_RinAoE TargetTag=Enemy 间隔=2（读 AttackInterval）
  Damage=10 / TowerHitForce=0 / AttackInterval=2 / TowerAttackRange=2
  表现：Animator=Tower_Rin trigger=Attack 音效=RinAttack   投射物资源=(无，符合预期)
[TowerAttackCheck] === Tower_Luo：攻击方式=Attack_LuoHeal TargetTag=Tower 间隔=1（读 HealInterval）
  HealAmount=5 / HealInterval=1 / TowerAttackRange=2
  表现：Animator=Bao trigger=Heal 音效=Heal   投射物资源=(无，符合预期)
[TowerAttackCheck] === Tower_Teto：攻击方式=Attack_TetoBullet TargetTag=Enemy 间隔=5
  Damage=10 / BulletSpeed=8 / TowerHitForce=0 / AttackInterval=5 / TowerAttackRange=2
  表现：Animator=(未配) 音效=(关闭)   ← 与旧 Teto 一致（它本来就没有攻击动画/音效）
```
批处理编译 `*** Tundra build success`、0 个 `error CS`；`[EntitySOValidator] 通过：9 个 EntitySO` + `CLI_OK`；三个塔 prefab 均**无 missing script**；`entityConfig` / `_detectionCollider` / `_highlightMaterial` 三个引用与迁移前逐字节一致（`Tower_Rin` 的 `_attackAnimator` 指向根节点、`Tower_Luo` 的指向子物体 `Bao`，与旧代码的查找结果一致）。

**运行时表现需 Play 验证**：Rin 打范围内全体并播 Attack 动画 + `RinAttack` 音效；Luo 治疗范围内**其他**塔（不治自己）并播 Heal 动画 + `Heal` 音效；范围内无目标时**不播**动画；选中高亮与索敌圈仍正常。

**没做**：武器侧（`GunWeapon`/`SpinWeapon`）仍未迁移，所以「换攻击方式」目前只能给塔用；`LevelUpSO` 的 `replaceAttack`（升级换攻击方式）未做；数值模型重构（实体通用数值 + 攻击方式加成）未做；buff/状态效果系统按决定不做。

---

## 2026-09-29 · 炮弹：攻击方式与塔解耦的第一次验证

**动机**：目标形态是「塔不再有确定的攻击模式」——同一座塔换一个攻击方式资产就换成另一种打法（Teto 装子弹 or 炮弹、Rin 装对群 or 对单）。Step 1 已让 Teto 走 `AttackDriver`，但当时只有一种攻击方式，「插拔」没有被真正验证过。这次做出第二种攻击方式作为验证。

**做了什么**：
1. **`AttackMethodSO.TargetTag`（虚属性，默认 `"Enemy"`）——「打谁」从 driver 搬到攻击方式上。** 原先 `AttackDriver._targetTag` 是 driver 的序列化字段，那么给塔换上治疗攻击方式后 driver 仍然只找 `Enemy`，治疗技能会去治敌人。放在 SO 上，换攻击方式时目标类型自动跟着换。
2. `StatType` 末尾新增 `ShellDamage = 18` / `ShellExplosionRadius = 19`。炮弹**不复用** `Damage` 的语义：这样「子弹伤害低、炮弹伤害高」是两套可独立升级的数值，而不是「同一份 `Damage` 乘一个隐藏倍率」——后者只能整条远程线一起升，也没法给炮弹伤害单独做升级。
3. `TowerDataSO` 新增 `ShellDamage`(40) / `ShellExplosionRadius`(3) 并写进 StatModel。
4. `ProjectileAttackSO` 新增 `Scale`（表现）、`SplashOnHit`、`SplashRadiusStat`。
5. `BulletController.Init` 新增带默认值的 `splashRadius` 参数：`> 0` 走溅射、`<= 0` 走原来的单体。
6. 新资产 `Assets/Game/SO/Attack/Attack_TetoShell.asset`（`Scale=3`、伤害读 `ShellDamage`、溅射半径读 `ShellExplosionRadius`）。

**关键决定：炮弹和子弹共用一个 prefab**
需求是「仍然用相同的美术资产，只是体形变大」。所以不新建 prefab、不新建 Addressables 条目：`Scale` 直接缩放实例（`Physics2D` 会按 transform 缩放碰撞体，命中面一起变大），溅射由 `BulletController` 内部按 `splashRadius` 判断。**换攻击方式不需要动 prefab。**

**溅射实现踩到的两个坑**：
- `ContactFilter2D` 默认构造出的 `layerMask` 是 **0**，会过滤掉**所有**层——必须显式 `useLayerMask = false`，否则查询永远返回 0 个结果**且不报错**。
- `useTriggers = false` + tag 过滤：6 个敌人 prefab 都是「根节点非 trigger 碰撞体（tag=Enemy）+ 子物体 `ColliderTrigger`（trigger，Untagged）」，所以按 tag 过滤天然保证每个敌人只结算一次。**直击的那个敌人也在半径内，因此溅射模式下不再额外结算一次单体伤害**，否则会打两下。

**迁移注意**：
- `Tower_Teto.prefab` 里的 `_targetTag: Enemy` 现在是孤儿键（字段已从 driver 删除）：Unity 忽略、Inspector 不显示、下次保存自动清除。行为不变——`ProjectileAttackSO.TargetTag` 默认就是 `"Enemy"`。
- `BulletController.Init` 新增的是**带默认值**的参数，所以 `GunWeapon` 的 4 参调用点不用改，拿到的仍是单体行为。
- `TetoBulletController.Init` 的签名必须跟着改成 5 参：否则它会**隐藏**基类方法而不是重写（`CS0108`），炮弹走 5 参调用时就不会旋转。

**验证（实际输出）**：
```
[ShellAttackCheck] 炮弹：DamageStat=ShellDamage(18) SpeedStat=BulletSpeed(10) ForceStat=TowerHitForce(6)
                    SplashRadiusStat=ShellExplosionRadius(19) Scale=3 SplashOnHit=True TargetTag=Enemy
[ShellAttackCheck] 子弹：DamageStat=Damage(2) Scale=1 SplashOnHit=False TargetTag=Enemy
[ShellAttackCheck] Tower_Teto 的 StatModel（来自 Tower_Teto）：
  子弹伤害 10 / 炮弹伤害 40 / 炮弹溅射半径 3 / 投射物速度 8 / 击退力度 0 / 攻击间隔 5 / 索敌半径 2
```
批处理编译 `*** Tundra build success`、0 个 `error CS`；`[EntitySOValidator] 通过：9 个 EntitySO` + `CLI_OK`；一次性校验器 `ShellAttackCheck` 断言「炮弹读的 4 个 `StatType` 在 Teto 的模型里都存在」通过（`CLI_OK`），跑完即删。

**怎么试**：把 `Attack_TetoShell.asset` 拖到 `Tower_Teto.prefab` 的 `AttackDriver._attack` 槽位即可，不改代码、不改 prefab 结构。**Teto 默认仍是子弹**（`_attack` 未被改动），所以这次改动不改变现有玩法。

**运行时表现需 Play 验证**：炮弹是否真的 3 倍大、命中后是否对周围敌人一起结算。

**没做**：`AttackDriver._attack` 没有运行时 setter，所以「升级时换成炮弹」还做不到——那需要 `LevelUpSO` 支持「换攻击方式」这一新的升级类型，是独立的一步。Rin/Luo 也仍未迁移（Step 2）。

---

## 2026-09-29 · 攻击方式 SO 化（第 1 步：只接 Teto）

**现象**：
- 「攻击」在两个互不相干的类族里各写了一遍：塔走 `BaseTower` 的 `TryOperate`/`OnOperate` 模板方法，武器走 `BaseWeapon.TryFire` + 自己的 `Update`。两边的计时器几乎逐行相同，而 `Teto.OnOperate` 与 `GunWeapon.SpawnBullet` 是同一段「读三个数值 → `Instantiate` → `Init`」的重复代码。
- 想给塔或玩家加一种新的攻击手段，必须新写一个 C# 子类并改预制体上的 MonoBehaviour，无法只靠加资产完成。
- `BaseWeapon.TryFire` 在 `interval <= 0` 时直接 `LogWarning`，而它被 `Update` **逐帧**调用——配置缺失会退化成每帧一条日志 + 字符串插值，正踩在性能红线上（`BaseTower.TryOperate` 有 `_intervalWarned` 节流，武器侧漏了）。

**后果**：
- 加攻击手段的成本固定在「写类 + 改预制体」，与机制是否真的新无关。
- 塔与武器无法共用同一份攻击实现（Teto 的子弹和枪的子弹是两套代码）。

**修法**（第 1 步只接 Teto；Rin/Luo 与武器侧留待第 2、3 步）：
1. 新增 `AttackMethodSO`（抽象 SO）：只描述「一次攻击做什么」——`GetInterval(self)` 给间隔、`Execute(in AttackContext)` 给行为。
2. 新增 `AttackContext`（`readonly struct`）：`Self` / `Origin` / `Direction` / `Target` / `TargetsInRange` / `Prefab`。**朝向由实体给**（玩家写输入方向、塔写指向目标），SO 因此不依赖 `IInputHandle`，同一份攻击方式塔和玩家都能用。
3. 新增 `AttackDriver`（MonoBehaviour，挂在实体根节点）：攻击系统里**唯一**持有实例状态的地方——计时器、范围内目标列表、Addressables 句柄、`AimDirection`。
4. 新增 `ProjectileAttackSO`：数值来源（`DamageStat`/`SpeedStat`/`ForceStat`）是 SO 上的 `StatType` 字段，读的是**发起攻击的实体自己**的 StatModel。
5. `Tower_Teto.prefab` 的 `Teto` 组件换成 **`BaseTower` + `AttackDriver`**，`Teto.cs` 删除；攻击方式资产 `Assets/Game/SO/Attack/Attack_TetoBullet.asset`（Damage / BulletSpeed / TowerHitForce，与旧 `Teto.OnOperate` 的读取一一对应）。

**迁移注意**：
- **`BaseTower` 必须留在预制体上**：它是塔的身份组件，`TowerManager` / `DetectPlayer` / `EnemyTargetFinder` / `GamePanel` / `TowerLevelUpPanel` / `PlayerUpgradeController` 全都按 `BaseTower` 引用塔。删塔子类时不能连它一起删。
- **投射物 prefab 不放 SO**：`WeaponDataSO` 里已经写明「统一走 Addressables 加载」，本设计延续该决策——SO 只持有语义，driver 用 `AssetReferenceGameObject` 持有资源并负责句柄的获取与归还。`Addressables.LoadAssetAsync<T>(AssetReference)` 是合法的（`AddressablesImpl.GetResourceLocations` 会识别 `key is AssetReference` 并取 `RuntimeKey`），`IAssetService.LoadAssetAsync<T>(object key)` 的签名直接能吃。
- **SO 上不能有实例状态**：计时器、目标列表、句柄一律在 driver。SO 是常驻资产，没有可靠的 `OnEnable`/`OnDisable`。
- 索敌判据**刻意与旧实现一致**：只认「自身 GameObject 既带 `_targetTag`、又挂有 `BaseHealthController`」的碰撞体。塔与敌人的碰撞体都挂在子物体上（塔的 `SearchRange`、敌人的 `ColliderTrigger`），带 tag 的只有各自根节点，于是每个实体恰好入列一次。改成按父级找组件会让子碰撞体也入列，enter/exit 计数不对称（第一个 exit 就把仍在范围内的目标移走）。
- `TetoBulletController` 保留在 `TetoBullet.prefab` 上即可：`GetComponent<BulletController>()` 拿到的是子类实例，虚方法 `Init` 会正确派发，因此不需要「要不要旋转」的开关。

**验证**：批处理编译 `*** Tundra build success`、0 个 `error CS`；`[EntitySOValidator] 通过：9 个 EntitySO，id 唯一且完整。` + `CLI_OK`；预制体逐字段复核（`entityConfig`/`_detectionCollider`/`_highlightMaterial` 三个引用与迁移前完全一致，`_attack` 指向新资产，`_prefab.m_AssetGUID` 指向已注册的 `Common/TetoBullet`）。**运行时表现（Teto 是否照常射击、子弹朝向是否正确）需 Play 验证。**

**没做**：Rin/Luo 未迁移（`BaseTower` 的 `TryOperate`/`OnOperate` 骨架仍在，对 `BaseTower` 自身是空转）；武器侧未迁移；`AttackDriver` 暂无 `OnPerformed` 事件——Rin 的动画与 Luo 的音效需要它，第 2 步再加。

---

## 2026-09-29 · 升级目标实体化 + DataSO 按类别拆分

**现象**：
- 武器升级（枪械 3 个 + 火球 3 个，共 6 个 `LevelUpSO`）**抽得到、买得起、扣技能点，但没有任何效果**。
- 每个 DataSO 都在 Inspector 里暴露一批与自己无关、改了没用的字段：塔有 `MoveSpeed`、Luo 有 `Damage`、武器有 `MaxHealth`/`MoveSpeed`。测试无法从 Inspector 判断哪些字段真的生效。
- 另有 4 处「配了没人读」：`BaseEntityDataSO.EntityName`、`WeaponDataSO.projectilePrefab`、`PlayerDataSO.Damage`/`AttackInterval`、`EnemyDataSO.ExpReward`。

**后果**：
- 玩家把技能点花在无效升级上，全程零报错；`BaseWeapon` 的类注释还写着「通用属性从玩家 StatModel 读取，实现升级同步」，把排查方向直接带偏（实现从引入起就一直读自己的模型）。
- 测试改了 Inspector 数值后行为不变，只能靠读代码判断字段是否承重。

**根因**：
1. 升级池把玩家升级与武器升级混成一批选项，但应用时统一 `ApplyTo(_player)`；而武器读的是**武器自己的** StatModel，修饰符永远到不了武器——选项这个概念里根本没有「目标实体」。
2. `BaseEntityDataSO` 持有 `MaxHealth`/`MoveSpeed`/`Damage` 三个只对部分类别成立的字段，所有子类无条件继承；`LuoTowerDataSO` 又继承攻击塔 `TowerDataSO`，于是连 `HitForce`/`BulletSpeed` 也一并继承。

**修法**：
1. 新增 `UpgradeOption`（`LevelUpSO` + 目标 `EntityBehaviour`）；`IPlayerUpgradeController` 三个方法改返回 `UpgradeOption[]`；`PlayerUpgradeController` 组装池时给每项带上目标（玩家升级 → 玩家，武器升级 → **那个武器**）；`LevelUpPanel`/`TowerLevelUpPanel` 改为 `option.So.ApplyTo(option.Target)`。
2. `BaseEntityDataSO` 掏空（只剩空的 `FillStatModel`），数值按「有没有血量」分两支：`BaseActorDataSO`（MaxHealth）→ `PlayerDataSO` / `EnemyDataSO` / `BaseTowerDataSO`（AttackRange + Cost）→ `TowerDataSO`（攻击塔）/ `LuoTowerDataSO`（治疗塔）；武器走 `WeaponDataSO`。
3. 删掉「配了没用」的字段；`EnemyDataSO.ExpReward` 改为**接线**：经验掉落数量走 `StatType.ExpReward`（原先硬编码 1，全部资产也是 1，因此行为不变）。
4. 附带修掉 `EntityBehaviour.InitStatModel` 的失败告警未节流——预制体漏配 EntitySO 时，`GetStat` 会在逐帧路径上每帧打一条 `LogWarning`，正踩在性能红线上；幂等判据同时从 `HasAnyStat()` 改为显式标志位（原判据在「DataSO 一个数值都没填」时会反复重建模型并重复触发 `OnStatModelInitialized`，导致子类重复订阅）。

**迁移注意（为什么既有资产数值没丢）**：
- 字段只换所属类、**不改名**：Unity 按字段名反序列化，`.asset` 里的 `Damage: 10` 会被新位置正确读到。
- `TowerDataSO` / `LuoTowerDataSO` 的类名与 GUID 都未变——把它们改成 `abstract` 会让既有资产直接报 `script class cannot be abstract`。
- 资产里会留下孤儿键（武器的 `MaxHealth`/`MoveSpeed`、塔的 `Heal*`/`MoveSpeed`、玩家的 `Damage`/`AttackInterval`）：Unity 忽略且**不在 Inspector 显示**，下次保存自动清除。
- 验证方式：`Assets/Editor/DataSOMigrationCheck.cs`（一次性体检，跑完即删）逐资产比对 `FillStatModel` 后的 `StatModel` 数值，并断言「不该存在的数值」确实不存在。

**附带发现（未修，属数据缺口不是代码问题）**：体检 23 个 `LevelUpSO` 资产发现 **12 个是空壳**（`statModifiers` 为空且无回血标记）——Teto 全部 5 个、Rin 全部 4 个、Luo 的 `LuoHealIncrease`/`LuoHealInterval`/`LuoRecoverHealth`；塔升级里只有 `LuoHealRange`（TowerAttackRange +1）真正有效。这些资产的 `levelUpText` 写着效果（如「提高1点攻击力」）但没有任何 `StatModifier` 支撑，塔升级面板会照常扣技能点。**注意**：`TetoRecoverHealth` 的文案是「恢复所有血量并增加最大血量」，但它连 `fullHeal` 标记都没有。

**状态**：生效。**取代** 2026-06-12「数据归属」中「数值放 DataSO 基类」的部分（表现信息仍在 EntitySO）。运行时表现（升级是否真的生效、经验掉落数量）需 Play 验证。

---

## 2026-09-29 · 对象池：契约集中，实例分散

**决策**：抽出 `IPoolable` 契约 + `ObjectPool<T>` 实现，但**不做全局池服务**；每个类型自己持有 `ObjectPool<T>` 字段。

**理由**：
- 每种对象的重置逻辑都不同（伤害数字重置文本与计时、经验球重取玩家引用、敌人重置血量/击退/波次增强），泛型池表达不了这些差异——**中心化的只该是「不变量」，不是「容器」**。
- 抽公共实现的时机是「第 3 个池出现」（Rule of Three）。此前经验球与伤害数字两个池**各自违反了同一条不变量**（归还计时放 `Start`、归还非幂等），结果都出现「同一对象被两个使用者同时取用」——说明共享的确实是规则。
- 全局池服务会制造「跨场景持有 GameObject」问题，并把爆炸半径从「一个池」扩大到「所有池」；且必须用 prefab/类型做 key，等于引入字符串键的全局注册表（与「禁止 enum key 事件总线」的结论冲突）。

**附带确立的红线**：
- 重置逻辑**必须**走 `IPoolable` 钩子，不能挂 `OnEnable`/`Start`——`Instantiate` 出来的对象本就是激活的，`SetActive(true)` 是空操作，**首次创建时 `OnEnable` 不会触发**。
- 每个池必须定保留上限。
- 池化对象的副作用（掉经验/结算）放显式 `Die()`，不放 `OnDisable`（后者同时意味着「销毁」与「回收进池」）。

**状态**：生效。

---

## 2026-09-29 · SO 入口去注册表：实体直接引用 EntitySO

**决策**：删除 `SOManager` / `ISOManager` / `EntityCatalogSO` / `EntityType` 枚举，改为 `EntityBehaviour` 直接序列化引用 `BaseEntitySO`，用稳定 `id` 做存档/网络标识，另设只读校验器保证全库一致性。

**理由**：
- 「枚举 → 注册表」的间接层是**静默错位**的温床：枚举值改动或漏配会指向另一个实体且不报错。这与 `StatType` 整数落盘的坑同源。
- 消除「`SOManager` 尚未 Awake」的时序依赖后，数值初始化可以提前到 `Awake`，顺带修掉「子类声明 `Start` 会隐藏基类 `Start`」的老陷阱。
- 校验器只**读**资产、不复制数据，不会像注册表那样与真实引用分叉，且可批处理执行（退出码可进 CI）。

**同时确立**：升级池组装与「当前展示的 N 个选项」从配置服务搬到**按玩家实例化**的 `PlayerUpgradeController`——前者是玩法逻辑，后者是可变会话状态（放全局单份会让联机下玩家互相覆盖）。

**状态**：生效。**取代** 2026-06-12「实体目录集中化」与 2026-06-11「实体配置系统重构」中的注册表部分。

---

## 2026-09-29 · 数据契约：枚举一律显式赋值，已用编号永不重用

**决策**：`StatType` 全成员显式赋值并冻结编号（删除成员时保留编号空洞）。

**理由**：`LevelUpSO`/`StatModifierData` 把枚举以**整数**落盘。一次中段增删导致 **6/7 个已接线的武器升级指向错误属性**——火球升级去改枪械子弹速度、枪械攻速升级落在编号空洞上静默无效，全程零报错。显式赋值让「新增成员」不可能移动既有编号。

**状态**：生效。此结论**沿用并强化**了 2026-06-12 对已删除的 `EntityType` 枚举做过的同类修复——**凡是落盘为整数的枚举，都适用本条**。

---

## 2026-09-28 · 服务规范统一：`UIManager` → `UIService`，不暴露 `Instance`

**决策**：UI 管理器改名为 `UIService` 并对齐 `AssetService`/`AudioService` 的形态；**取消 `Instance`**，访问一律 `XxxService.Service?.…`（纯 `ServiceLocator.TryGet`）。

**理由**：
- 三套服务此前形态各异（有的 `Instance`、有的 `Service` 带回退），调用点写法不统一；`Instance` 是绕过服务定位器的第二条访问路径，会让人图省事而破坏接口边界。
- `ServiceLocator.Get<T>()` 在未注册时抛异常，业务代码应统一用返回 null 的 `Service` 属性 + `?.`。

**后续推广**：`ManagerSingleton<T>` 的 `Instance`/`HasInstance` 也已移除；`InputReaderManager` 为此补了 `IInputReaderManager` 接口，让输入工厂不必保留后门。

**状态**：生效。

---

## 2026-09-28 · 面板按需加载，删除反射全量预加载

**决策**：面板在首次 `ShowPanelAsync<T>()` 时按 `UI/<类名>` 地址加载并缓存；**不提供任何全量预热入口**。

**理由**：反射枚举无法感知场景，必然把当前场景用不到的面板也拉进内存（Level0 会加载 `MenuPanel`），且句柄持有到服务销毁、永不归还——这是常驻开销而非缓存。

**若将来实测首次打开有卡顿**：正确做法是**按场景声明**预热该场景真正会用到的少数面板，而不是全量枚举。

**状态**：生效。**取代** 2026-09-21「阶段 3」引入的反射预加载。

---

## 2026-09-28 · Addressables 句柄纪律：获取与归还必须成对

**决策**：`LoadAssetAsync` 的句柄由调用方 `Release`；`InstantiateAsync` 的实例必须用 `IAssetService.ReleaseInstance` 归还（直接 `Destroy` 不递减引用计数）；失败分支同样要归还；随场景销毁的对象在 `OnDestroy` 补一次归还。

**理由**：引用计数只增不减时 bundle 永不卸载，属随游玩时长单调增长的内存问题。**Editor 走 Asset Database 模式看不出来，只在真机 Player Build 暴露**——所以「编辑器没问题」不能作为通过标准。

**状态**：生效。

---

## 2026-09-21 · 组合根 + 服务定位器（阶段 1-6）

**决策**：新增 `GameBootstrap` 作为唯一组合根（`[RuntimeInitializeOnLoadMethod]` 自建，暴露 `Ready`）；删除 `Main`/`GameLevelManager` 各自的重复引导；UI/音频/事件总线全面改为「接口 + 强类型事件」。

**附带决策**：
- **删除 `EventCenter`** 全局枚举事件总线：enum key 难重构，且联机下只服务本进程。状态流转改强类型接口（如 `NotifyPlayerDied`）。
- **音频改为 `IAudioService` + 对象池**，不再每次 `new GameObject`。
- **按玩家实例化**：经验控制器与武器槽下放 Player（`PlayerWeaponController`），删除全局武器管理器。
- **塔不做 owner/team**：合作模式下塔是共享资源（YAGNI）。

**状态**：生效。

---

## 2026-06-12 · 武器攻击循环：协程 → `Update` 计时器

**决策**：删除 `Start` 里启动的攻击协程，改为 `BaseWeapon.TryFire()` 在 `Update` 中累计计时。

**理由**：`Start` 启动的协程在组件生命周期内只执行一次，武器被 `SetActive(false)` 后再激活时攻击循环不会恢复。

**状态**：生效。这条经验后来泛化为通用红线：**凡是「每次激活都要重做」的逻辑都不能挂 `Start`**（对象池的重置逻辑同理）。

---

## 2026-06-12 · 数据归属：表现信息在 EntitySO，数值/消耗在 DataSO

**决策**：塔的名称/描述/图标放 `TowerEntitySO`，购买消耗放 `TowerDataSO`；面板从这两处读取，不再依赖额外的 `LevelUpSO`。

**理由**：单一事实源——同一份展示信息不要在两处维护。

**状态**：生效。

---

## 2026-06-12 · Manager 单例基类统一

**决策**：所有 MonoBehaviour Manager 继承 `ManagerSingleton<T>`，初始化入口统一为 `OnSingletonAwake()`，核心 Manager 用负 `[DefaultExecutionOrder]` 保证先于业务脚本。

**状态**：生效（`Instance` 部分已被 2026-09-28 的服务规范取代，只保留「重复实例处理 + 场景生命周期绑定 + 自注册」职责）。
