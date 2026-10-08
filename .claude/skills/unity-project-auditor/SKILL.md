---
name: unity-project-auditor
description: 当用户要求审计 Unity 项目结构规范性、检查文件/文件夹组织是否合理、或询问"这个文件应该放在哪里"时使用。不自动触发，仅在用户手动调用或明确询问项目结构相关问题时启用。
---

# Unity 项目结构审计师

## Overview

审计 Unity 项目的目录结构和文件摆放规范性。**不自动触发**，仅用户调用时生效（如"审计我的项目结构"、"检查文件摆放是否规范"）。

架构约定（依赖访问优先级、命名规范、SO 只读铁律等）以 `~/.claude/unity-convention.md` 为准；目录组织规范详见 `references/unity-project-conventions.md`。本文不重复，仅给出审计维度与流程。

## 审计工作流

### 步骤 1：扫描项目结构

扫描整个 `Assets/` 目录，重点：`Scripts/` 子目录、`Prefabs/` 分类、`Scenes/` 命名、`Art/`、`Resources/`、其他顶级目录。

### 步骤 2：按维度逐项审计

> 说明：以下「目录/资产摆放」类维度（1-7）的规范细则见 `references/unity-project-conventions.md`；「架构合规」类维度（8-10）的细则见 `unity-convention.md`。此处仅列检查规则。

#### 审计项 1：顶级目录结构
- [ ] 是否有文件散落 `Assets/` 根目录？
- [ ] 顶级目录是否过多（>15）或命名风格不一致（如 `Scripts` 与 `script` 混用）？

#### 审计项 2：脚本文件组织
- [ ] 脚本是否都在 `Scripts/` 下，按功能模块分目录（非按类型堆叠）？
- [ ] 单目录文件是否过多（>10 应细分）？文件名与类名是否一致？

#### 审计项 3：ScriptableObject 摆放
- [ ] `.asset` 是否误入 `Scripts/`？是否整齐分类到 `Resources/Databases/<领域>/` 子目录？
- [ ] `.asset` 是否散落在 `Prefabs/` 等不属实目录？

#### 审计项 4：Prefab 组织
- [ ] Prefab 是否按类型分目录（Characters/Props/UI/Projectiles/VFX）？
- [ ] 单目录 Prefab >20 需细分；命名是否统一（`Enemy_Goblin.prefab` 而非 `goblin.prefab`）？

#### 审计项 5：资源文件管理
- [ ] 是否有 `Art/` 且按 Model/Texture/Material 细分？资源是否混入 `Scripts/` 或 `Prefabs/`？
- [ ] `Resources/` 内文件是否确实需内置加载？

#### 审计项 6：场景文件
- [ ] 场景是否散落根目录？命名是否规范（`Level_01.unity`、`MainMenu.unity`）？是否有未使用残留？

#### 审计项 7：命名规范一致性
- [ ] 目录是否统一 PascalCase？Prefab/Script 命名是否匹配所引用组件？是否有 `New Folder`/`新建文件夹`/`test123` 等无意义名？

#### 审计项 8：模块间依赖与耦合
- [ ] 模块 A 的脚本是否误入模块 B 目录？系统间是否交叉引用（如 `SkillSystem/` 含 `UI/` 代码）？各系统是否自包含？

#### 审计项 9：依赖访问规范性（→ unity-convention.md §2.1）
- [ ] `Update()`/`FixedUpdate()` 中是否调用 `GetComponent`/`Find`（应 Awake/Start 缓存）？
- [ ] 业务逻辑是否用 `GameObject.Find`/`FindObjectOfType`（仅引导期允许）？
- [ ] 是否有明显可合并或滥建的 `public static Instance` 单例（如 5+ 个、或为随处访问而建）？是否有跨脚本拖拽？是否有链式取值 `Services.Get<A>().b.c.d`？

#### 审计项 10：ScriptableObject 使用规范（→ unity-convention.md §2.3）
- [ ] 运行时是否写入 SO 字段（编辑器会持久化脏数据）？
- [ ] SO 是否被拖到多个业务脚本（应注册进 `Services`）？是否有 `GameBootstrap` 单一组合根装配所有库？跨系统 SO 是否经 `Services.Get<Database>()` 访问？

#### 审计项 11：Manager 命名（→ unity-convention.md §2.2）
- [ ] 是否有名不副实的 `XxxManager`（如只封装领域规则却无编排职责）？
- [ ] Manager 是否符合"主动 + 编排全局流程"特征（被动订阅的应是 System）？

### 步骤 3：输出审计报告

```markdown
## Unity 项目结构审计报告

### 审计时间
<时间>

### 总体评价
<优/良/中/差，1-2 句>

### 发现的问题（按严重程度排列）

#### [严重/中等/轻微] <问题标题>
- **位置：** <文件/目录路径>
- **问题：** <描述>
- **建议：** <如何修复>

### 推荐行动清单
1. [ ] <待办1>
2. [ ] <待办2>
```

**注意**：报告中涉及具体架构问题时，必须显式命名所违反的设计原则或模式（如"违反依赖倒置原则 DIP"、"应使用服务定位器模式 Service Locator"、"这是注册表模式 Registry Pattern"、"应用组合根 Composition Root"）——遵循全局 CLAUDE.md 设计知识注解要求，中英并标。

---

## Resources

- `references/unity-project-conventions.md` — 目录组织规范详细参考
