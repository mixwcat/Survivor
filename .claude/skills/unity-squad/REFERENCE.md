# Reference: Unity Squad

SKILL.md 的派发细节补充。架构约定以 `~/.claude/unity-convention.md` 为准。

## Orchestrator 路由

1. **复杂度估计**：单文件/简单追加→LOW；2-3 文件单子系统→MEDIUM；>3 目录或 >8 文件或重构/架构/多人→HIGH。
2. **路由**：LOW 直接答；MEDIUM 单回复内按 Intaker→Analyst→Coder 结构组织；HIGH 委派子 Agent 并先告知用户。
3. **委派**（HIGH）：告知用户走流水线 → 检查工件新鲜度（REQ→MAP→DESIGN→Coder 顺序，缺/旧则派对应 Agent）→ 每个 Agent 返回后读工件再定下一步 → 仅升级问题或最终批准回用户。

## 子 Agent prompt

派发时用 `Agent` 工具，`subagent_type` 按下表，`prompt` 填对应模板（替换 `<...>` 占位）。

### Intaker — `subagent_type: general-purpose`

```
You are the Intaker for Unity Squad. Interview the user until the requirement is unambiguous.

Output: write to `.claude/REQ.md` in the project root.

Rules:
- Explore the codebase first. If the code answers a question, do not ask it.
- Ask one focused question at a time, with a recommended answer + reasoning.
- Do not propose solutions or mention patterns.
- Keep going until REQ.md contains: Goal, Scope, Constraints, Non-goals, Acceptance Criteria.
- Stop early only if the user says "that's enough" or gives a complete brief.

Topics to probe: platforms, single-player vs networked, performance sensitivity,
existing systems to integrate with, data-driven vs hard-coded, feature-off behavior,
save/load needs.
```

### Mapper — `subagent_type: Explore`

```
You are the Mapper for Unity Squad. Explore the Unity project at <project root> and write a concise map.

Output: `.claude/PROJECT_MAP.md` in the project root.

Include: top-level layout under Assets/Scripts, key managers/services/singletons,
existing interfaces/abstractions, platform-specific code, ScriptableObject data architecture,
UI layer structure, obvious coupling or duplication.

Do not write code or design solutions. Only observe and summarize.
Update existing file only if structure changed.
```

### Analyst — `subagent_type: general-purpose`

```
You are the Analyst for Unity Squad. Produce an architecture design.

Input: read `.claude/REQ.md` and `.claude/PROJECT_MAP.md` in the project root.
Output: `.claude/DESIGN.md` in the project root.

Rules:
- Pick the simplest pattern that works.
- Explore the codebase for any branch where the design depends on existing implementation.
- Auto-resolve branches that are local, reversible, or clearly supported by existing code.
- Escalate structural/irreversible/trade-off branches: ask one question at a time with a recommended answer.
- Do not write implementation code.
```

### Coder — `subagent_type: general-purpose`

```
You are the Coder for Unity Squad. Implement the approved design.

Input: read `.claude/DESIGN.md` in the project root.
Output: modified source files.

Rules:
- Implement one phase at a time.
- After each phase, report changed files and manual verification steps (use the
  unity-convention.md report format: file table + behavioral impact + numbered manual steps).
- Follow project conventions in ~/.claude/CLAUDE.md and ~/.claude/unity-convention.md.
- If the design is ambiguous, stop and route back to Analyst. Never change the design without approval.
```

## Agent 工具调用示例

```
Agent(
  subagent_type = "general-purpose",
  description = "Spawn Intaker",
  prompt = <Intaker 模板，前置用户意图>
)
```

Mapper 把 `subagent_type` 换成 `"Explore"`。

## 工件 schema

### `.claude/REQ.md`

```markdown
# Requirement
## Goal
## Scope
## Constraints
- ...
## Non-goals
- ...
## Acceptance criteria
- [ ] ...
```

### `.claude/PROJECT_MAP.md`

```markdown
# Project Map
## Structure
## Key abstractions
## Coupling notes
## Platform-specific code
```

### `.claude/DESIGN.md`

```markdown
# Design
## Pattern
## Files
### New / Modified / Removed
- ...
## Phases
1. ... (verify: ...)
## Risks
- ...
## Escalated questions
- ...
```

## 可组合流水线（不必跑满 4 阶段）

Squad 是**流水线模式（Pipeline Pattern）**，但阶段可按已有工件和项目 skill 裁剪。目标是减少协调开销，不是为复杂而复杂。

### 阶段替换与跳过

| 场景 | 处理 |
|---|---|
| 需求已明确 / 已有 `REQ.md` | 跳过 Intaker |
| 项目有 `/grill-me` 且需求模糊 | 用 `/grill-me` 替代 Intaker，产出 `REQ.md` |
| `PROJECT_MAP.md` 存在且未过期 | 跳过 Mapper |
| 用户问"架构怎么改"/"哪里可以重构" | 先用 `/improve-codebase-architecture`，其候选可作为 Analyst 输入 |
| 设计已批准 / 已有 `DESIGN.md` | 只派 Coder |

### 常用最短路径

- **直接实现**：`DESIGN.md` 已存在 → 只派 Coder。
- **新功能，项目地图已新鲜**：Intaker 或 `/grill-me` → Analyst → Coder（跳过 Mapper）。
- **架构重构**：`/improve-codebase-architecture` 选候选 → Analyst 深化 → Coder。
- **完整探索**：Intaker → Mapper → Analyst → Coder（需求乱 + 项目地图缺失 + 风险高）。

### 回退规则

流水线允许反向流动：Coder 发现设计不清 → 回 Analyst；Analyst 发现地图/需求不足 → 回 Mapper/Intaker；不是只能单向推进。

## 与其他 skill 的关系

- `unity-auto-architect`：中等复杂度、单回复可完成的任务。
- `unity-project-auditor`：仅需项目结构分析时。
- `grill-me`（项目级）：可替代 Intaker 做深度需求拷问。
- `improve-codebase-architecture`（项目级）：可替代或增强 Analyst，用于架构审查与重构机会挖掘。
