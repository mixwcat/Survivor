---
name: unity-squad
description: Unity multi-agent squad for high-complexity tasks. Invoke when user explicitly asks, or when the task matches the routing criteria in ~/.claude/unity-convention.md (typically >8 files / >3 script directories / multiple subsystems) and the user has confirmed.
---

# Unity Squad

## 何时使用

**触发条件**：
- 用户显式说 "use unity-squad" / "unity-squad" / `/unity-squad`；
- 或任务满足 `~/.claude/unity-convention.md`「零」的高复杂度路由条件（通常：影响 >8 文件、跨 >3 脚本目录、涉及多子系统），且 **AI 已告知用户并征得同意**。

**不使用**：简单任务直接实施；中等任务用 plan mode（遵循全局 Design Before Code）；结构审计用 `unity-project-auditor`。具体阈值见 convention「零」。

## 工作流

复杂任务走 Intaker → Mapper → Analyst → Coder 流水线，各 Agent 产物写入 `.claude/` 共享工件。**阶段可按已有工件和项目 skill 裁剪**（详见 [REFERENCE.md](REFERENCE.md)「可组合流水线」）：需求明确可跳过 Intaker，地图新鲜可跳过 Mapper，设计已批准可只派 Coder。

| Agent | 产物 | 职责 |
|---|---|---|
| **Intaker** | `.claude/REQ.md` | 追问用户，澄清意图/约束/非目标 |
| **Mapper** | `.claude/PROJECT_MAP.md` | 探索 `Assets/Scripts/**`，汇总结构/模式/耦合点 |
| **Analyst** | `.claude/DESIGN.md` | 读 REQ+MAP，产出架构设计与分阶段计划 |
| **Coder** | 代码改动 | 按 DESIGN 分阶段实现 |

**规则**：先探索代码再提问，一次一个聚焦问题并给推荐答案，仅未决分支升级给用户；上一个工件路径传给下一个 Agent；`PROJECT_MAP.md` 存在且未过期则跳过 Mapper；阶段不清则回退 Analyst 而非问用户；非需并行探索时一次只派一个 Agent。

## 触发与委派（Orchestrator）

1. 评估复杂度：单文件→LOW 直接答；2-3 文件单子系统→MEDIUM 单回复内按 Intaker→Analyst→Coder 结构组织；>3 目录/>8 文件/重构/架构→HIGH 委派子 Agent。
2. HIGH 时先告知用户"此任务跨多子系统，将走 Unity Squad 流水线"，再检查工件新鲜度：REQ 缺/旧 → 派 Intaker；否则 MAP 缺/旧 → 派 Mapper；否则 DESIGN 缺/旧 → 派 Analyst；否则派 Coder 做下一阶段。
3. 每个子 Agent 返回后读其工件再决定下一步；仅升级问题或最终批准才回到用户。

## 子 Agent 派发

用 `Agent` 工具（`subagent_type` 见下），把对应 prompt 填入。完整 prompt 模板见 [REFERENCE.md](REFERENCE.md)。

| Agent | subagent_type | 摘要 |
|---|---|---|
| Intaker | `general-purpose` | 探索代码后逐个追问，写 `.claude/REQ.md`（Goal/Scope/Constraints/Non-goals/Acceptance） |
| Mapper | `Explore` | 探索项目写 `.claude/PROJECT_MAP.md`（结构/单例/接口/SO 数据/UI/耦合），不写码不设计 |
| Analyst | `general-purpose` | 读 REQ+MAP 写 `.claude/DESIGN.md`（模式选择/文件改动/分阶段/风险/升级问题），不写实现码 |
| Coder | `general-purpose` | 读 DESIGN 实现下一阶段，改完报变更与手动验证；设计不清停下回退 Analyst，不改设计 |

## 示例（精简）

> "重构输入系统，让游戏逻辑不直接依赖 InputReader 和 Joystick。"

- Orchestrator：跨 `InputSystem/`、`Entity/Player/`、`UI/`，>8 文件 → 派子 Agent。
- Intaker 问："本地分屏、客户端-服务端联网，还是先单机？" → 用户"先单机，后联网"。
- Mapper 探索写 `PROJECT_MAP.md`（目录布局、现有单例、平台相关代码）。
- Analyst 写 `DESIGN.md`：Adapter + Factory；新增 `IInputHandle`/`PCInputHandle`/`MobileInputHandle`/`InputHandleFactory`；分阶段迁移 Player→Weapon→Tower→UI。
- Coder 实现阶段 1，报"已加 `IInputHandle` 与平台适配器，验证：项目可编译"，等用户确认后再进阶段 2。
