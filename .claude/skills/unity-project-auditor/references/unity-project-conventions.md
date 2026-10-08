# Unity 项目目录组织规范参考

本文档提供 Unity 项目中文件/目录组织的行业最佳实践，供审计时参考。架构约定（依赖/命名/数据）以 `~/.claude/unity-convention.md` 为准，本文不重复。

---

## 推荐顶级目录结构

```
Assets/
├── Scripts/           # C# 脚本（Unity 默认复数）
├── Prefabs/           # 预制体
├── Scenes/            # 场景
├── Art/               # 美术资源
│   ├── Models/        # 3D 模型 (.fbx, .obj)
│   ├── Textures/      # 贴图
│   ├── Materials/     # 材质
│   ├── Shaders/       # 着色器
│   └── Animations/    # 动画
├── Audio/             # 音频（Music/、SFX/）
├── Resources/         # 内置资源；SO 库统一放 Resources/Databases/
├── StreamingAssets/   # 流式资源（仅在需要时创建）
├── Plugins/           # 原生插件
├── Settings/          # 项目设置
├── Editor/            # 编辑器扩展脚本
└── ThirdParty/        # 第三方资源（未通过 Package Manager 安装时）
```

- 顶级目录 **PascalCase**、英文命名，避免中英混用。

---

## Scripts/ 内部组织

按系统/功能模块划分（依赖方向：Core ← Systems ← Characters ← UI）：

```
Scripts/
├── Core/                # 核心基础设施：Services、EventBus、ObjectPool
├── Systems/             # 业务系统，每系统独立子目录
│   ├── CombatSystem/
│   ├── SkillSystem/
│   └── InventorySystem/
├── Characters/          # 角色（按角色细分）
│   ├── Player/
│   └── Enemies/
├── UI/                  # View/ ViewModel/ Widgets/
├── Data/                # ScriptableObject C# 定义（非 .asset）
├── Services/            # 服务层
└── Utils/               # 工具类与扩展方法
```

- 每模块按职责分文件；单目录文件 >15 应建子目录；文件名与类类名一致。
- **全局服务优先走 `Services`**；`Manager` 后缀类按领域归入对应模块，允许按需使用 `public static Instance` 单例（尤其需要 Inspector 配置或生命周期绑定时），禁止滥建。

---

## Prefabs/ 内部组织

```
Prefabs/
├── Characters/   (Player/、Enemies/)
├── Props/
├── UI/
├── Projectiles/
└── VFX/
```

PascalCase，类型前缀可选（`Enemy_Goblin.prefab`、`UI_Button_Close.prefab`）；避免空格与中文。

---

## ScriptableObject 数据规范

- `.asset` 实例放 `Assets/Resources/Databases/<领域>/`（统一 `Resources.Load`；规模大了换 Addressables）。
- `.cs` 定义放 `Scripts/Data/`。
- **绝不**把 `.asset` 放进 `Scripts/`。

```
Resources/Databases/
├── Skills/    (Fireball.asset、Heal.asset)
├── Enemies/   (Goblin.asset、Dragon.asset)
└── Items/     (Sword.asset、Potion.asset)
```

---

## 场景文件规范

```
Scenes/
├── 00_Boot/
├── 01_MainMenu/
├── 02_Gameplay/   (Level_01.unity、Level_02.unity)
└── 99_Testing/    (Test_Battle.unity)
```

可用数字前缀排序；PascalCase + 下划线；Build Settings 按加载顺序排列。

---

## 模块间依赖原则

- `Core` 不依赖任何模块；`Systems` 依赖 `Core`；`Characters` 依赖 `Systems`+`Core`；`UI` 依赖下层。
- UI 不含业务逻辑；角色不直接依赖 UI；低层工具不依赖高层业务。

---

## 常见不规范案例

| ❌ | ✅ |
|---|---|
| `Scripts/Skills/Fireball.asset` | `Resources/Databases/Skills/Fireball.asset` |
| `Assets/PlayerController.cs`（散落根目录） | `Assets/Scripts/Characters/Player/PlayerController.cs` |
| `Scripts/PlayerSkill_Fireball.cs`、`Scripts/EnemySkill_Fireball.cs`（技能散落） | 可复用技能入 `Systems/SkillSystem/`，角色专有入 `Characters/<角色>/Skills/`（见下） |
| `Managers/` 目录集中所有 `Singleton.cs` | 全局服务优先走 `Services`；Manager/单例按领域归入对应模块 |

**技能归属决策**：可被多角色共用 → 入 `Systems/SkillSystem/`；角色专有 → 入 `Characters/<角色>/Skills/`；混用 → 组合模式或接口分离。

---

## 审计检查清单（快速扫描用）

```
[ ] 顶级目录规范、命名一致，无文件散落 Assets/ 根目录
[ ] Scripts/ 按模块分目录，单目录 <15 文件，.asset 不在 Scripts/ 下
[ ] 无集中式 `Managers/` 目录（全局服务优先走 Services；Manager/单例按需归入对应模块）
[ ] Prefab 按类型分类，美术/音频不与脚本混放
[ ] 场景在 Scenes/ 下，命名统一
[ ] 目录统一 PascalCase，无中文/空格/New Folder
[ ] 模块间无逆向依赖（Core ← Systems ← Characters ← UI）
[ ] 未使用资源已清理
```
