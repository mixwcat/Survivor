# vendored Mirror 的本地补丁清单

> `Assets/Mirror/` 是 **vendored 源码**（不是 UPM 包），所以可以直接改 —— 但改了就与上游分叉。
> **本文件是这些分叉的唯一台账**：每打一个补丁就在这里登记一条，**升级 Mirror 后逐条重贴并复核**。
>
> 原则（见 `../Mirror/01-Mirror指南.md` §0）：**除本文件登记的补丁外，不要修改 `Assets/Mirror/` 下的任何文件**。
> 扩展一律走「子类 + 自定义 Transport + 自定义 `Writer<T>`/`Reader<T>`」。
>
> 每个补丁在源码里都带 `[本地补丁 / LOCAL PATCH — Survivor]` 标记，可以用 `grep -rn "本地补丁" Assets/Mirror/` 一次性查全。

---

## 检查补丁是否还在

```powershell
Select-String -Path "Assets\Mirror" -Include *.cs -Recurse -Pattern "本地补丁 / LOCAL PATCH"
```

升级 Mirror 后如果这条命令**没有**输出，说明补丁丢了，按下面的条目重贴。

---

## 补丁 1：`NetworkConnection()` 在序列化期间访问 `Time.time`

| 项 | 内容 |
|---|---|
| 文件 | `Assets/Mirror/Core/NetworkConnection.cs` |
| 位置 | ① 无参构造函数 `internal NetworkConnection()` ② `IsAlive(float timeout)` |
| 症状 | Unity 6 进入 Play 时（本项目**域重载是开启的**，见 CLAUDE.md 的项目事实），Unity 会序列化/反序列化带 `NetworkIdentity` 的资产，过程中构造 `NetworkConnection`，于是抛<br>`UnityException: get_time is not allowed to be called during serialization` |
| 影响 | 编辑器里每次进 Play 都报一条红错；连接对象的时间戳拿不到 |
| 状态 | ☑ 已打（2026-10） |

### 改动内容

**① 构造函数** —— 在**原有那行**外面包一层 `try/catch`：

```csharp
internal NetworkConnection()
{
    // set lastTime to current time when creating connection to make
    // sure it isn't instantly kicked for inactivity
    try
    {
        lastMessageTime = Time.time;
    }
    catch (System.Exception)
    {
        lastMessageTime = 0f;
    }
}
```

**② `IsAlive`** —— 让 0 表示「时钟还没取到」，**不能判死**：

```csharp
// 原实现：internal virtual bool IsAlive(float timeout) => Time.time - lastMessageTime < timeout;
internal virtual bool IsAlive(float timeout) =>
    lastMessageTime <= 0f || Time.time - lastMessageTime < timeout;
```

### 为什么必须连带改 `IsAlive`

`lastMessageTime` 是 `public float`（`NetworkConnection.cs:28`），`IsAlive` 用它做超时踢人判定。
如果只加 `try/catch`、让失败时落到 `0f`，那么在一个已经跑了很久的进程里新建连接时，
`Time.time - 0` 会远大于 `timeout` → **新连接被立刻踢掉**（正是原注释想避免的那件事）。
把 `<= 0` 视为存活，语义与原意图一致：**刚建好的连接永远不该被判死**。

`Time.time` 在真实会话里不可能是 0（连接总发生在启动之后），所以这个守卫不会掩盖正常情况。

---

## 升级 Mirror 的标准流程

1. 整目录替换 `Assets/Mirror/`（保留 `.meta` 的 GUID 一致性，否则 prefab/场景引用会断）。
2. `grep "本地补丁" Assets/Mirror/` → 对照本文件，**逐条重贴**。
3. 检查 `Assets/Mirror/version.txt` 并更新 `Docs/Mirror/README.md` 的基准版本号。
4. 重新核对 `Docs/Mirror/01-Mirror指南.md` §17「官方文档与本地源码不一致清单」与
   `02-API速查.md` 附录 A「源码中不存在的 API 清单」—— 这两张表是版本相关的。
5. 跑一次批处理编译（`Docs/MirrorPlan.md` P0.1）确认无 `error CS`。
