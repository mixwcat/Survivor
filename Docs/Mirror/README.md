# Mirror 联机开发文档（索引）

> 本目录是**联机开发的唯一参考资料**。改造进度表在上一级：`../MirrorPlan.md`。
> 基准：Mirror **96.11.3**（vendored 在 `Assets/Mirror/`）+ Unity **6000.0.44f1**。

## 文件清单

| 文件 | 内容 | 什么时候读 |
|---|---|---|
| **[01-Mirror指南.md](01-Mirror指南.md)** | Mirror 概念、生命周期、权威模型、RPC/SyncVar/序列化/场景/传输、**官方推荐 vs 常见坑**、性能。共 18 章 | 需要理解「Mirror 怎么工作」时 |
| **[02-API速查.md](02-API速查.md)** | 逐条从本地源码抄录的 public/protected 签名 + `文件:行号`，含 41 条 Weaver/运行时英文报错原文 | **写代码时查签名**（比记忆可靠） |
| **[03-项目落地注意.md](03-项目落地注意.md)** | 本项目专属红线：组合根、未激活 prefab、Addressables、对象池、静态状态、时序、暂停、相机、输入…… | **动手前必读** |
| **[04-影响面清单.md](04-影响面清单.md)** | 逐文件详表：现状 → 该在哪端跑 → 需要什么同步手段 → 风险；附 `NetworkIdentity`/`spawnPrefabs` 清单、静态状态清单、按玩家状态清单、25 条事件订阅、时序依赖、池与句柄冲突 | 要改某个具体文件时 |

## 30 秒版：本项目联机开发的铁律

1. **写完 API 先查 `02-API速查.md`** —— 官方文档有多处与 96.11.3 源码不符（`NetworkSceneManager`、`[SyncObject]`、`SendToClient`、`UnreliableSequenced`、`ReadPackedInt32` 等都不存在）。
2. **Host 模式下服务端与客户端同进程**：`ServiceLocator`、静态单例、C# `event` 是**共享**的。不要用「客户端不会跑这段」保证只执行一次。
3. **跨端状态只能走 `SyncVar` / `[Command]` / `[ClientRpc]` / `[TargetRpc]`**，进程内静态只做解耦。
4. **`NetworkServer.Spawn` 会强制 `SetActive(true)`** —— 而本项目 Player/武器/塔 prefab 靠「未激活 → 注入配置 → 激活」保数值。顺序必须是：`Instantiate`（未激活）→ 注入 → **自己** `SetActive(true)` → `Spawn`/`AddPlayerForConnection`。
5. **每个会被 `Spawn` 的 prefab 都要进 `NetworkManager.spawnPrefabs`**（玩家 prefab 放 `playerPrefab`，不要重复）。Addressables 动态注册是竞态，不要用。
6. **对象池与 Mirror 生命周期二选一** —— 联机路径关闭 `EnemyPool` / `ProjectilePool` / `ExpSpritePool`；`IAttackSpawner` 就是为此预留的替换点。
7. **`BaseHealthController.Heal/TakeDamage` 里直接调了 `DamageNumService`** —— 血量改服务端结算后，这条会在服务端进程里空跑，客户端看不到数字。改走「权威结算 + 本地表现」两段。
8. **`Time.timeScale = 0` 只冻本进程** —— 联机下暂停面板会让「我这静止、队友照打」。要么禁掉，要么只在单人 Host 允许。
9. **客户端不要自己 `LoadScene`** —— 走 `NetworkManager.ServerChangeScene`；`SceneFlow` 要按当前网络角色分派。
10. **`NetworkBehaviour.Awake` 早于 `OnStartServer`/`OnStartClient`**，那时 `isServer`/`isClient` 还没设置 —— 不要在 `Awake` 里做网络分支。
11. **`SyncVar` 的 hook 在客户端初次同步时也会调用**（`oldValue` 是默认值），hook 里不能假设「这是变化」。
12. **`assetId` 不会因为脚本改 prefab 而自动分配** —— 批处理保存后要读一次 `ni.assetId` getter 触发分配，否则 Build 客户端生成失败。
13. **场景分工已定死**（`../MirrorPlan.md` §1.3）：`Menu` 纯离线；**`Lobby` 既是房间也是离线大厅**（`offlineScene`）；`onlineScene` **留空**。
    ⇒ 在 Lobby 建房**不切场景**，`OnServerSceneChanged` **不会触发** —— 生成玩家要用 `OnServerReady(conn)` / `OnStartServer`。
14. **DDOL 里不能放 `NetworkBehaviour`**：`sceneId == 0` ⇒ 被当成动态对象 ⇒ `netId` 恒 0 ⇒ `[Command]`/`[SyncVar]` **静默失效**。
    跨场景的系统组件用纯 C#/`MonoBehaviour`，需要 `[Command]` 就写在**玩家对象**上。
15. **不要新增 additive 场景，也不要新建"持久场景"替代 `GameBootstrap`** —— Mirror 核心只做 single-mode 切场景，
    additive 得自己兜一套生命周期；项目已有组合根，再加一套常驻机制只会让规则分裂。

## 常见任务 → 去哪一节

| 我要做的事 | 去读 |
|---|---|
| 加一个会被服务端生成的 prefab | `01` §3/§4、`02` §2/§5、`03` §4 |
| 加一个同步字段 | `01` §6、`02` §3/§15 |
| 写一个 `[Command]` / `[ClientRpc]` | `01` §5、`02` §4/§5 |
| 让某个对象位置同步 | `01` §9、`02` §10/§11 |
| 切场景 / 大厅进关卡 / 建房入房 | `../MirrorPlan.md` §1.3、`01` §11、`02` §1、`03` §9 |
| 排查「客户端没生成出来」 | `02` §16（报错原文表）+ `01` §13 |
| 排查 `error CS` / Weaver 报错 | `02` §16 |
| 搞清楚某个现有文件要怎么改 | `04` 对应章节 |
| 决定「这个逻辑该在哪端跑」 | `01` §1（权威模型）+ `04` §0 结论摘要 |

## 维护约定

- 本目录的四份文档是 **Mirror 96.11.3 的快照**（编写日期 2026-10）。**升级 Mirror 后必须重新核对**，尤其是：
  - `01` §17「官方文档与本地源码不一致清单」
  - `02` 附录 A「源码中不存在的 API 清单」
  - `03` §15「Weaver / Domain Reload 的本地补丁」（升级后要重贴）
- 官方文档 URL 汇总在 `01` §18。抓 GitBook 原文的技巧：在页面 URL 后**加 `.md`** 可拿到原始 Markdown。
- 发现文档与代码不符时，**以代码为准**，并顺手改文档。
