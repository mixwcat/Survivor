# references —— 可直接复制到新项目的脚本模板

这两个 `.ps1` 是**可移植的**：拷到任何 Unity 项目里直接用，不用改任何一行。

> ⚠️ 两个 `.ps1` 里**只有 ASCII**。这不是风格问题：Windows PowerShell 5.1 会把
> **无 BOM 的 UTF-8** 当 ANSI 读，中文注释会让解析器直接报语法错。
> **往这两个文件里加中文之前先想清楚**（或者把文件存成带 BOM 的 UTF-8）。

## 它们各自解决什么

| 脚本 | 用途 |
|---|---|
| `compile-check.ps1` | 批处理编译校验：跑一次 `-batchmode -nographics -quit`，然后从日志里提取 `error CS` / `Tundra build` / 编译失败标记，给出 `RESULT: PASS\|FAIL` |
| `run-unity-method.ps1` | 跑一个 `-executeMethod`（幂等的 Editor 配置脚本），并汇总 `CLI_OK` / 异常 / 自定义日志前缀 |

## 怎么用

```powershell
# 放进新项目的 Tools\ 下（位置其实随意，见下面"自动定位"）
& .\compile-check.ps1
& .\run-unity-method.ps1 -Method "MySetup.SetupFromCommandLine"
& .\run-unity-method.ps1 -Method "MySetup.SetupFromCommandLine" -AlsoMatch "\[MySetup\]"
```

**前置：Unity 编辑器必须关着。** 工程被 GUI 占用时批处理会直接失败
（报 `another Unity instance is running with this project open`）。这是环境问题，不是代码问题。

## 自动定位（不用配路径）

- **工程路径**：从脚本所在目录**逐级往上找** `ProjectSettings/ProjectVersion.txt`。
  所以脚本放在 `<proj>\Tools\` 或 `<proj>\.claude\skills\...\references\` 里都能找到工程 ——
  **直接把 skill 整个拷进新项目也能跑**。
- **Unity.exe**：`-UnityExe` 参数 → 注册表（Unity Hub 的安装记录，
  key 是 `HKLM:\SOFTWARE\Unity Technologies\Installer\Unity <version>`，
  value 是 `Location x64`）→ 常见安装根目录里找最新的。
  版本号从工程自己的 `ProjectVersion.txt` 读。

两个都支持显式覆盖，定位失败时也会明确告诉你该传什么：

```powershell
& .\compile-check.ps1 -ProjectPath "D:\other\Proj" -UnityExe "C:\...\Editor\Unity.exe"
```

## 日志放哪

一律写到 `<proj>\Logs\<LogName>`。**建议把 `Logs/` 加进 `.gitignore`** ——
它是易变的构建产物，而且 Unity 的日志很大。

`run-unity-method.ps1` 的 `Show "exceptions"` 会把记录到的所有 `Exception|Error:` 都打出来，
其中可能包含与本次运行无关的噪声（例如 Unity 授权客户端的 `Licensing::Client` 报错）。
**判断成败看 `CLI_OK` / `RESULT:`，不要看这一段有没有内容。**

## 这两个脚本**不**覆盖什么

它们只做「配置 + 编译」。**进 Play 跑运行时断言**（例如联机冒烟测试）是另一套东西，
方法写在父目录的 `SKILL.md`「无头 Play 模式验证」一节里 ——
包括域重载会清掉 Editor 侧状态、为什么不能加 `-quit`、双进程为什么要镜像工程。

那类脚本**没法直接复用**，因为它必然引用新项目自己的入口方法与端口。
需要时**照结构改**：起进程 → 轮询日志里的 ASCII 标记等就绪 → 汇总两边的结果。
一个完整的参考实现是本仓库的 `Tools\run-network-2p.ps1`（双进程联机测试）。

## 保持同步

本仓库 `Tools\compile-check.ps1` / `Tools\run-unity-method.ps1` 与这里的两个文件
**内容完全一致**（可以用哈希比对）。改任一处记得同步另一处 ——
这里的版本是"给新项目复制的模板"，`Tools\` 里的是"本项目在用的"。
