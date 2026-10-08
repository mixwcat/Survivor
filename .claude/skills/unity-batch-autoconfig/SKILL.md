---
name: unity-batch-autoconfig
description: Automate Unity scene/prefab/asset configuration, compile-checking, and headless Play-mode verification from the CLI without touching the GUI, by generating an idempotent Editor script and running it with Unity in batch mode. Use when a Unity task requires creating GameObjects, attaching components, wiring Inspector references, creating prefabs, registering assets, or editing ScriptableObjects, and the agent cannot click in the Editor. Also covers offline C# compile checks against a generated .csproj, entering Play mode headlessly to run runtime assertions, and two-process client/server tests.
---

# Unity 批处理自动配置（无 GUI）

在无法操作 Unity 图形界面的情况下，用「Editor 脚本 + 批处理 `-executeMethod`」完成场景/预制体/资产配置，并用批处理编译校验。

> 📦 **`references/` 里有两个可直接复制到新项目的脚本**，拷贝即用、不用改一行：
> `compile-check.ps1`（编译校验）与 `run-unity-method.ps1`（跑 `-executeMethod`）。
> 它们会**自己往上找工程**（靠 `ProjectSettings/ProjectVersion.txt`）、**自己找 Unity.exe**
> （注册表 → 常见安装目录）。用法与边界见 `references/README.md`。
> **把整个 skill 目录拷进新项目也能跑**，因为定位是往上找而不是写死路径。

## 何时使用

- 需要新建 GameObject、挂组件、拖引用、做预制体、注册预制体/资产、填 ScriptableObject 列表。
- 需要反复、可重复（幂等）地配置，而不是让用户手工拖拽。
- 需要在改代码后验证“能编译/能构建”。

## 核心流程

1. **先定位 Unity 安装**
   - Windows 注册表：`HKLM:\SOFTWARE\Unity Technologies\Installer\Unity <version>` 的 `Location x64`。
   - 编译器路径：`<Editor>/Data/DotNetSdkRoslyn/csc.dll`，运行时：`<Editor>/Data/NetCoreRuntime/dotnet.exe`。
   - 版本以 `ProjectSettings/ProjectVersion.txt` 为准。

2. **写一个幂等的 Editor 脚本**（放在 `Assets/Editor/`，`#if UNITY_EDITOR` 包裹）
   - 提供 `[MenuItem("...")]` 方法供手动运行，并提供 `public static void SetupFromCommandLine()` 供批处理调用；批处理入口里显式 `EditorApplication.Exit(0/1)` 以返回退出码。
   - 场景编辑：`EditorSceneManager.OpenScene(path, OpenSceneMode.Single)` → 改对象 → `MarkSceneDirty` → `SaveScene`。
   - 预制体编辑：`PrefabUtility.LoadPrefabContents(path)` → 改 → `PrefabUtility.SaveAsPrefabAsset(root, path)` → `UnloadPrefabContents`。
   - 新建对象/组件：`new GameObject(...)`、`AddComponent<T>()`；字段直接赋值（`NetworkManager.transport = kcp` 这类）。
   - 填列表：直接给 `List<T>` 字段赋值，`AssetDatabase.LoadAssetAtPath<T>(path)` 取资产。
   - ScriptableObject：`new SerializedObject(so)` + `FindProperty("field").intValue = ...` + `ApplyModifiedPropertiesWithoutUndo()`。
   - **幂等**：可重复运行不产生重复对象/重复注册；对“首次从场景生成预制体、之后只更新”的分支要分别处理。
   - 结束时 `AssetDatabase.SaveAssets()` + `AssetDatabase.Refresh()`。

3. **运行（项目必须先关闭 GUI 编辑器）**
   ```powershell
   & "<Editor>\Unity.exe" -batchmode -nographics -quit `
     -projectPath "<Project>" `
     -executeMethod SomeSetup.SetupFromCommandLine `
     -logFile "<temp>\setup.log"
   ```
   - 若报 `another Unity instance is running with this project open` → 必须先关闭编辑器。
   - 检查日志里的自定义成功标记（如 `CLI_OK`）与 `error CS`。

4. **离线编译校验（不用启动 Unity）**
   - Unity 生成的 `*.csproj` 里有全部 `<Reference><HintPath>` 与 `<Compile Include>`。用脚本解析出引用与源文件，拼一个 `csc` 响应文件：
     ```
     -target:library -langversion:9.0 -noconfig -nostdlib+ -out:check.dll
     -define:<csproj 的 DefineConstants>
     -r:"<每个 HintPath>"
     "<每个源文件>"
     ```
   - 运行：`dotnet exec "<Editor>\Data\DotNetSdkRoslyn\csc.dll" @rsp`。
   - Assembly-CSharp 用的 Mirror 以源码形式在 `Assets/Mirror`，其 asmdef 会 `autoReferenced`；csproj 里以 `ProjectReference` 出现，需要用 `Library/ScriptAssemblies/*.dll` 补上跨程序集引用。
   - 校准 Editor 程序集时，引用**刚编译出的** `Assembly-CSharp-check.dll`（而不是 `Library/ScriptAssemblies/Assembly-CSharp.dll`，后者可能过时）。
   - 注意：PowerShell 5.1 读无 BOM 的 UTF-8 中文注释可能解析出错，校验脚本内用 ASCII 注释。
   - `csc` 只做类型检查，**不跑 Mirror Weaver**；最终仍需一次含 Weaver 的 Unity 编译（批处理运行 setup 时就会触发）。

## 无头 Play 模式验证（⭐ 批处理**能**进 Play）

> 更正：本文件早期版本写的是"批处理只能做配置 + 编译/Weaver，**不能进入 Play 模式**"。
> **那是错的。** `-batchmode -nographics` 下 `EditorApplication.EnterPlaymode()` 之后
> Unity 会**真的跑 Play 循环**，可以跑运行时断言、可以开网络、可以起两个进程互连。

**关键：不要加 `-quit`** —— 加了会在进入 Play 后立刻退出。让编辑器活着，由断言代码
在结束时自己退出。

### 陷阱 1：域重载会清掉 Editor 侧的静态状态（最容易卡住的一条）

如果工程是 `m_EnterPlayModeOptionsEnabled: 1` + `m_EnterPlayModeOptions: 0`
（选项启用但**没有任何 flag** ⇒ 域重载**照常发生**；先查 `ProjectSettings/EditorSettings.asset`），
那么挂在 `EditorApplication.update` 上的状态机会连同静态字段一起被清掉。

**表现**：日志停在"进入 Play 模式…"之后**再无任何输出**，进程烧 CPU 但不结束 ——
看起来像死锁，其实是状态机已经不存在了。

**正解：把状态机放进运行时程序集**，Editor 入口只负责置位开关并进 Play：

| 文件 | 程序集 | 职责 |
|---|---|---|
| `Assets/Editor/XxxTest.cs` | `Assembly-CSharp-Editor` | 开场景、置 `SessionState` 开关、`EditorApplication.EnterPlaymode()` |
| `Assets/Script/.../XxxDriver.cs`（`#if UNITY_EDITOR`） | `Assembly-CSharp` | 断言状态机 |

- 交接靠 **`SessionState`**（`SetBool`/`SetString`/`GetBool`/`GetString`）—— 它**能跨域重载存活**。
- 运行时那侧用 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` 自举，
  读到开关就 `new GameObject` + `DontDestroyOnLoad` + `AddComponent<驱动>()`，并**立刻清掉开关**
  （这样中途崩了也不会污染下一次普通的 Play）。
- 驱动用 `StartCoroutine` + `Update`，**不要**用 `EditorApplication.update`。
- 跨程序集可见性：Editor 入口读的那个常量必须是 `public`（`internal` 跨不了程序集，
  会报 "does not contain a definition for"）。

### 陷阱 2：怎么结束、怎么拿结果

- 正常路径末尾：`EditorApplication.ExitPlaymode()` → `EditorApplication.Exit(code)`。
- **判定用日志里的 ASCII 标记，不要用退出码** —— 实测 `Start-Process -PassThru` 拿到的
  `.ExitCode` 可能是空的。约定 `SMOKE_OK` / `SMOKE_FAIL: <原因>` / `SERVER_READY` 这类记号。
- 需要**常驻**的角色（例如双进程测试里的服务端，要等客户端跑完断言）就不要自己退出，
  由编排脚本在客户端结束后 `Stop-Process`。

### 陷阱 3：PowerShell 怎么启动 Unity

`Unity.exe` 是 **GUI 子系统**程序：`& "<path>" args` **不会等待**、也拿不到退出码
（表现为"命令立刻返回、日志是空的"）。

```powershell
# 前台等它跑完
Start-Process -FilePath $exe -ArgumentList $args -Wait -PassThru -NoNewWindow
# 后台起、稍后 kill
$p = Start-Process -FilePath $exe -ArgumentList $args -PassThru -NoNewWindow
Stop-Process -Id $p.Id -Force
```

**脚本里只用 ASCII**：PowerShell 5.1 把**无 BOM 的 UTF-8** 当 ANSI 读，
中文注释/中文字符串会让解析器报 `The string is missing the terminator` 之类的语法错。
（所以编排脚本也不能去 grep 中文日志行 —— 让被测代码额外打一行 ASCII 标记。）

### 陷阱 4：两个实例不能开同一个工程

工程锁会直接拒绝（`another Unity instance is running with this project open`）。
需要**双进程**（服务端 + 真客户端）时，维护一份镜像工程：

```powershell
robocopy <proj> <proj>Client /MIR /MT:16 /XD .git Logs Temp obj .vs
# 之后每次运行前只同步 Assets / Packages / ProjectSettings
robocopy <proj>\Assets <proj>Client\Assets /MIR /MT:16
```

**不要动副本的 `Library`** —— 这样只有改动的资源需要重新导入，才便宜到能当回归工具用
（首次建镜像是一次性的大拷贝）。

### 陷阱 5：断言必须"有鉴别力"（这是最值钱的一条）

单进程 Host 测试里服务端与客户端是**同一个对象**，所有 `if (!IsAuthority) return;`
都会提前返回 —— **"客户端侧路径"在 Host 下原理上就测不到**。

而更隐蔽的是：**"看起来在动"不算证据**。权威判据写错时，客户端会**自己**推进状态，
于是"推车在动 / 时钟在走"照样通过 —— **测试全绿而 bug 仍在**（本项目真的这样漏过一个：
判据对"没有 NetworkIdentity 的场景对象"恒为 true，一整批客户端守卫全部失效）。

有鉴别力的判据只有两类：

1. **哨兵值**：服务端设一个客户端**不可能自己算出来**的值（例如把波次设成 42），
   客户端断言自己看到了它。
2. **只在"权威数据真的被应用"时才增长的计数器**（例如 `AppliedNetworkStateCount`），
   客户端断言它 > 0。

另外：断言要**钉住间接层**。例如相机/HUD/面板读的是 `PlayerManager.LocalPlayer`
而不是直接读 `isLocalPlayer` —— 只断言后者，前者回退成"第一个注册的"时抓不到。

## 必踩的坑与对策

- **必须先关 Unity**：GUI 占用项目时批处理直接失败。
- **编程式改预制体不会触发 Mirror 的 assetId 分配**：`NetworkIdentity._assetId` 仍是 0，导致 Build 客户端无法生成。对策：保存后 `AssetDatabase.LoadAssetAtPath<GameObject>(path)`，读取 `ni.assetId` getter（编辑器下会触发 `SetupIDs/AssignAssetID`），再 `EditorUtility.SetDirty` + `SaveAssets`。
- **`AddComponent<NetworkTransform/NetworkRigidbody>()` 会触发 `Reset()`，此时 `target` 为空而抛 NRE**：属噪声，组件仍会加上；随后显式设置 `target`、`syncDirection`、`updateMethod`、`syncScale` 等即可。
- **Unity 6 进入 Play 的 domain reload 会序列化带 `NetworkIdentity` 的预制体**，触发 Mirror `NetworkConnection()` 里的 `Time.time` → `UnityException: get_time is not allowed to be called during serialization`。可在 vendored Mirror 的该构造里 try/catch 回退为 0（记录为本地补丁，升级 Mirror 时需重贴）。
- **Mirror `SpawnMessage` 不包含父子关系**：不要依赖“服务器 SetParent 后 Spawn，客户端自动跟随父子”。要么客户端在 `OnStartClient` 自行重建父子，要么服务器算好世界位置用 `NetworkTransform` 同步。
- **场景里的对象引用会被删除操作置空**：例如删除场景 Player 后，Cinemachine 相机的 `TrackingTarget` 变 null，相机不再跟随；联机下应由运行时脚本把目标指向“本地玩家”，而不是固定引用。
- **`NetworkBehaviour` 没有 NetworkIdentity 会在编辑器 OnValidate 报错**：凡是把某个基类改成 `NetworkBehaviour`，其所有子类所在预制体都要有（根节点）`NetworkIdentity`。
- **每个要用 `NetworkServer.Spawn` 的预制体都要注册**：`NetworkManager.spawnPrefabs`（Player 预制体单独放 `playerPrefab`）。
- **静态单例/事件不能跨端**：跨端状态用 `SyncVar`/`[Command]`/`[ClientRpc]`，静态事件总线只做进程内解耦。

## 验证闭环

- 批处理能做「配置 + 编译/Weaver」，也能**进 Play 跑运行时断言**（见上一节）——
  但仍有两类东西**必须由人 Play 确认**：输入/相机/画面等表现层，
  以及"跑完一整局"这种长流程。
- 分了层就别偷懒：**动了服务端侧路径**跑单进程 Host 冒烟；**动了客户端侧路径**必须跑双进程。
  只跑前者会给"客户端是对的"这种**虚假信心**。
- 每阶段都跑一次纯远程客户端（不要只测 host），并留意 Console 红错。

## 汇报格式（配合本流程）

每次改动后说明：改了哪些文件/行为、自动配置了什么、编译/Weaver 是否通过、需要用户手动确认的运行时步骤。
