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
