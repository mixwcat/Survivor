# Tools —— 批处理验证脚本

> 全部是 **Windows PowerShell 5.1** 脚本，**只用 ASCII 字符**。
> 这不是风格洁癖：PowerShell 5.1 会把**无 BOM 的 UTF-8** 当 ANSI 读，
> 脚本里的中文注释与中文字符串会被解析成乱码并报 `Missing terminator` 之类的语法错
> （踩过一次，见 `.claude/skills/unity-batch-autoconfig/SKILL.md`）。
>
> 日志一律写到 `<proj>/Logs/`（已 gitignore）。

## 前置：编辑器必须关着

任何一个脚本都会启动 `Unity.exe -batchmode`。**GUI 编辑器占用工程时批处理会直接崩**
（报 `another Unity instance is running with this project open`）——这是环境问题，不是代码问题。

```powershell
Get-Process Unity -ErrorAction SilentlyContinue   # 有输出就先关掉
```

Unity 路径写死在脚本里：`D:\unity\unitydownload\6000.0.44f1\Editor\Unity.exe`（随版本升级要改）。

## compile-check.ps1 —— 编译校验

```powershell
& Tools\compile-check.ps1 -LogName compile.log
```

跑一次 `-batchmode -nographics -quit` 编译，然后从日志里提取并打印：
`error CS` / `Tundra build` / Weaver banner / 编译失败标记，最后给 `RESULT: PASS|FAIL`。

**改完任何 C# 都要跑一次。** 判据是日志里没有 `error CS` 且出现 `Tundra build success`。

## run-unity-method.ps1 —— 跑一个 `-executeMethod`

```powershell
& Tools\run-unity-method.ps1 -Method "AddressablesSetup.SetupFromCommandLine" -LogName addr.log
```

用于跑项目里的幂等配置脚本（`Assets/Editor/` 下那些 `*FromCommandLine`）。
成功判据是日志里有 `CLI_OK`。

## run-network-smoke.ps1 —— 端到端联机冒烟测试

```powershell
& Tools\run-network-smoke.ps1 -LogName smoke.log
```

**会真的进入 Play 模式**（所以脚本里刻意**没有** `-quit` —— 编辑器要活着才能跑断言）。
验证链路：组合根装配 NetworkManager → 大厅生成离线玩家 → `StartHost()` 不切场景 →
服务端生成恰好一个玩家 → 切到 Level0 重建 → 切回 Lobby 重建 → `timeScale == 1`。

成功判据：日志里有 `SMOKE_OK` 且退出码 0；失败是 `SMOKE_FAIL: <原因>`。

实现分两半，**不要把它们合并**：

| 文件 | 程序集 | 职责 |
|---|---|---|
| `Assets/Editor/NetworkSmokeTest.cs` | `Assembly-CSharp-Editor` | 开场景、置 `SessionState` 开关、`EnterPlaymode()` |
| `Assets/Script/Core/Network/NetworkSmokeDriver.cs` | `Assembly-CSharp`（`#if UNITY_EDITOR`） | 断言状态机 |

**为什么驱动必须在运行时程序集里**：本项目**域重载是开启的**（见 `CLAUDE.md` 项目事实），
进入 Play 时 Editor 脚本的静态字段会被整个清掉 —— 挂在 `EditorApplication.update` 上的状态机
会在"进入 Play"的瞬间静默消失，表现是**进了 Play 之后再无任何输出**（第一次踩就是这个）。
两者的交接只能靠 `SessionState`（跨域重载存活）。

它**验证不了**的：真正的第二个进程、输入、相机、UI、画面 —— 那些必须由人 Play 确认。

## run-network-2p.ps1 —— 双进程联机测试 ⭐

```powershell
& Tools\run-network-2p.ps1
```

**为什么必须有它**：Host 单进程测试里服务端与客户端是**同一个对象**，
所有 `ApplyNetwork*` 都会因权威守卫提前返回 —— 「客户端真的按广播走」这条路径
**在 Host 下原理上就测不到**。已经因此漏过一个真 bug：`NetworkAuthority` 对场景对象
恒为 `true`，整批客户端守卫失效，而 Host 测试全绿。

**怎么绕开"两个 Unity 不能开同一个工程"**：脚本维护一份**镜像工程**（默认
`D:\unity\proj\SurvivorClient`），每次运行前把 `Assets` / `Packages` / `ProjectSettings`
用 `robocopy /MIR` 同步过去（不动它的 `Library`，所以只有改动的资源需要重新导入）：

| 角色 | 工程 | 入口 |
|---|---|---|
| 服务端 | 本仓库 | `NetworkSmokeTest.RunServerFromCommandLine` |
| 客户端 | 镜像副本 | `NetworkSmokeTest.RunClientFromCommandLine` |

镜像的**首次**创建不在脚本里（8.8 GB，一次性）：

```powershell
robocopy D:\unity\proj\Survivor D:\unity\proj\SurvivorClient /MIR /MT:16 /XD .git Logs Temp obj .vs
```

**客户端断言什么**（这些是 Host 测不到的）：

1. 连接建立，房间里看到**两个**玩家；
2. **恰好一个**角色的 `NetworkIdentity.isLocalPlayer` 为真（认错人 ⇒ 相机跟错、输入给错）；
3. 跟着服务端切到关卡（客户端不自己切场景）；
4. 关卡里也是两个玩家；
5. **推车在客户端也在动** ⇒ `CartController.ApplyNetworkState` 真的在执行；
6. **关卡时钟在客户端也在走** ⇒ `GameLevelManager.ApplyNetworkClock` 真的在执行；
7. **敌人副本 `netId != 0`** ⇒ assetId 与 `spawnPrefabs` 都对
   （漏一个的症状是客户端报 "Failed to spawn server object"，而 Host 端一切正常）。

**服务端**跑完同样的关卡断言后**常驻不退出**（客户端要留在关卡里跑断言），
由脚本在客户端退出后杀掉。所以服务端的判定看日志里的 `SMOKE_OK`，**不看退出码**。

**编排靠 ASCII 标记**：脚本用 `SERVER_READY` / `SMOKE_OK` / `SMOKE_FAIL` 这三个
纯 ASCII 记号来判进度 —— 它不能去 grep 中文日志行，理由见本文件开头。
