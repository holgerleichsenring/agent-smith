# Phase 64: Typed Skill Orchestration

## Goal

Replace the current free-form discussion model with a typed orchestration system.
Each skill declares its role, position, and output type. The orchestrator builds
a deterministic execution graph from skill definitions — no LLM-based triage for
structured and hierarchical pipelines.

## Problem

Today all pipelines use the same interaction model: Triage selects participants
via LLM, skills "discuss" by accumulating free text, ConsolidatedPlan is produced
but never flows back into the output. The result is expensive, unpredictable,
and unaccountable — when something is wrong, it's unclear which skill failed.

The core issues:
- Skills communicate by appending text — no typed contracts
- Triage via LLM is expensive and non-deterministic for pipelines with fixed roles
- false-positive-filter participates in discussion but its decisions never filter findings
- No declared execution order — everything runs as peers

## Architecture

### Pipeline Types

Three interaction patterns, explicitly declared per pipeline:

```yaml
# agentsmith.yml
pipelines:
  mad-discussion:
    type: discussion      # open, accumulating, triage-led
  security-scan:
    type: structured      # typed handoffs, gates block, parallel contributors
  fix-bug:
    type: hierarchical    # lead first, contributors against plan, gate validates
```

| Type | Order | Communication | Triage |
|------|-------|---------------|--------|
| `discussion` | dynamic | free text, accumulated | LLM (as today) |
| `structured` | from skill graph | typed JSON handoffs | none — graph from skills |
| `hierarchical` | from skill graph | plan → contributors → gate | none — graph from skills |

---

### Skill Role Types

Declared in `agentsmith.md` per skill:

```
contributor  — analyzes, appends to shared context, no blocking
lead         — produces a plan/directive all subsequent skills receive
gate         — blocks pipeline progression, JSON output, has veto
executor     — acts in the world, produces artifact (report, PR, file)
```

### Output Types

What the orchestrator does with the skill's response:

```
list      — filtered/transformed collection → passed as input to next skill
plan      — directive → injected into context of all subsequent skills
artifact  — persisted to disk/pipeline, not forwarded
verdict   — boolean gate → pipeline continues or stops
```

### Gate Veto Mechanisms

Gates have veto authority but differ in how they express it:

| Gate output | Veto condition | Use case |
|-------------|---------------|----------|
| `verdict`   | `false` → pipeline stops | Binary pass/fail (e.g. tester) |
| `list`      | empty list → pipeline stops | Filtering gates (e.g. false-positive-filter) |

Both are gates with blocking authority — `verdict` is for yes/no decisions,
`list` is for gates that filter input and pass survivors downstream.

### Skill Definition: `agentsmith.md` Extension

```markdown
# Agent Smith Extensions

## orchestration
role: gate
runs_after: [contributor]
runs_before: [lead]
output: list
parallel_with: []          # empty = sequential after runs_after group

## convergence_criteria
- "All findings rated with confidence 1-10"
- "No finding without file/line reference"
```

---

## Skill Graph: All Pipelines

### MAD (`discussion`) — unchanged

All skills are `contributor`, output `artifact`. Order is dynamic, triage-led.
No changes needed here beyond adding `role: contributor` to `agentsmith.md`.

```
devils-advocate  ─┐
dreamer          ─┤ contributor (parallel, accumulating)
philosopher      ─┤
realist          ─┤
silencer         ─┘
```

Silencer is special: contributor that can respond `[SILENCE]`. No code change needed.

---

### Legal (`discussion`) — refined

`contract-analyst` becomes `lead` — must run first, sets the frame.
Others are `contributor`. `clause-negotiator` is `executor` — acts last.

```
contract-analyst   → lead       (runs first, output: plan)
     ↓ plan injected into all below
compliance-checker → contributor (parallel, output: artifact)
risk-assessor      → contributor (parallel, output: artifact)
liability-analyst  → contributor (parallel, output: artifact)
     ↓ all artifacts collected
clause-negotiator  → executor   (runs last, output: artifact)
```

agentsmith.md per skill:

```markdown
# contract-analyst
role: lead
runs_after: []
runs_before: [contributor, executor]
output: plan
```

```markdown
# compliance-checker / risk-assessor / liability-analyst
role: contributor
runs_after: [lead]
runs_before: [executor]
output: artifact
parallel_with: [compliance-checker, risk-assessor, liability-analyst]
```

```markdown
# clause-negotiator
role: executor
runs_after: [contributor]
runs_before: []
output: artifact
```

---

### Coding (`hierarchical`)

```
architect          → lead       (runs first, output: plan)
     ↓ plan injected
backend-developer  ─┐
frontend-developer ─┤ contributor (parallel, output: artifact)
dba                ─┤
devops             ─┤
product-owner      ─┤
     ↓
security-reviewer  → gate       (output: verdict — fail blocks AgenticExecute)
tester             → gate       (output: verdict — fail blocks CommitAndPR)
```

agentsmith.md:

```markdown
# architect
role: lead
runs_after: []
runs_before: [contributor, gate]
output: plan
```

```markdown
# backend-developer / frontend-developer / dba / devops / product-owner
role: contributor
runs_after: [lead]
runs_before: [gate]
output: artifact
parallel_with: [backend-developer, frontend-developer, dba, devops, product-owner]
```

```markdown
# security-reviewer
role: gate
runs_after: [contributor]
runs_before: [executor]
output: verdict
```

```markdown
# tester
role: gate
runs_after: [executor]       # skill name or role type — both supported
runs_before: []
output: verdict
```

Note: The Coding pipeline demonstrates why `runs_after` must accept both role
types and skill names — there are two gate stages with an executor between them.
`tester` uses `runs_after: [executor]` (role type), but could also use
`runs_after: [agentic-executor]` (skill name) for precision.

---

### Security Scan (`structured`)

Static tools run first (no skills), then contributors in parallel,
then false-positive-filter as gate, then vuln-analyst as lead for final synthesis.

```
[StaticPatternScan]     ─┐
[GitHistoryScan]        ─┤  tool steps (no skills)
[DependencyAudit]       ─┘
     ↓ raw findings
secrets-detector        ─┐
injection-checker       ─┤  contributor (parallel)
config-auditor          ─┤  each receives only their category slice
compliance-checker      ─┤
ai-security-reviewer    ─┤
supply-chain-auditor    ─┤
auth-reviewer           ─┘
     ↓ contributor outputs (JSON lists per category)
false-positive-filter   →   gate (receives all, output: list — confirmed only)
     ↓ confirmed findings
vuln-analyst            →   executor (final synthesis, output: artifact → DeliverFindings)
```

agentsmith.md:

Each contributor declares only its own categories in its own `agentsmith.md`:

```markdown
# secrets-detector
role: contributor
runs_after: []
runs_before: [gate]
output: list
parallel_with: [injection-checker, config-auditor, compliance-checker,
                ai-security-reviewer, supply-chain-auditor, auth-reviewer]
input_categories: [secrets, history]
```

```markdown
# injection-checker
role: contributor
runs_after: []
runs_before: [gate]
output: list
parallel_with: [secrets-detector, config-auditor, compliance-checker,
                ai-security-reviewer, supply-chain-auditor, auth-reviewer]
input_categories: [injection, ssrf]
```

Other contributors follow the same pattern with their respective categories:
- `config-auditor`: `[config]`
- `compliance-checker`: `[compliance]`
- `ai-security-reviewer`: `[ai-security]`
- `supply-chain-auditor`: `[dependencies]`
- `auth-reviewer`: `[secrets, injection]`

```markdown
# false-positive-filter
role: gate
runs_after: [contributor]
runs_before: [lead]
output: list

## output_format
```json
{
  "confirmed": [{ "file": "", "line": 0, "pattern": "", "severity": "", "reason": "" }],
  "rejected":  [{ "file": "", "line": 0, "pattern": "", "reason": "" }]
}
```
```

```markdown
# vuln-analyst
role: executor
runs_after: [gate]
runs_before: []
output: artifact
```

---

### API Security Scan (`structured`)

```
[LoadSwagger]           → tool step
[SpawnNuclei]           → tool step
     ↓
api-design-auditor      ─┐
auth-tester             ─┤  contributor (parallel)
api-vuln-analyst        ─┘  (also gets nuclei findings)
     ↓
dast-false-positive-filter → gate (output: list)
dast-analyst               → lead (final synthesis, output: plan)
```

---

## Communication: Compact by Design

The token problem today: skills get everything, produce free text, nothing is filtered.

With typed orchestration:

**contributor** receives:
- Only its `input_categories` slice (not all findings)
- No prior contributor outputs (they run in parallel)
- Output: compact JSON list, max 50 items

**gate** receives:
- All contributor JSON outputs merged
- Output: filtered JSON list with reasons

**lead** receives:
- Gate output only (already filtered)
- Summary of contributor artifact counts
- Output: plan/synthesis

**Parallelism**: "parallel" means no accumulated context between contributors —
each runs independently with only its input slice. Execution is sequential
for now (no `Task.WhenAll`). True parallel execution is a later optimization.

This means:
- Contributors never see each other's output (no accumulation)
- Gate gets structured input, produces structured output
- Lead gets only what survived the gate
- No free-text accumulation for structured/hierarchical pipelines

Token estimate vs today:
- Today: 8 skills × ~5000 tokens = 40k tokens
- After: 6 contributors × ~800 tokens + 1 gate × ~2000 tokens + 1 lead × ~1500 tokens = ~8k tokens
- **~80% reduction**

---

## Orchestrator Changes

### SkillGraphBuilder (new)

```csharp
public sealed class SkillGraphBuilder
{
    public SkillGraph Build(IReadOnlyList<SkillDefinition> skills)
    {
        // 1. Group by role
        // 2. Resolve runs_after / runs_before into execution stages
        // 3. Within each stage, parallel_with defines parallel groups
        // 4. Return ordered list of execution stages
    }
}

public sealed record SkillGraph(
    IReadOnlyList<ExecutionStage> Stages);

public sealed record ExecutionStage(
    IReadOnlyList<SkillDefinition> Skills,  // parallel within stage
    bool IsGate,
    bool IsLead);
```

### SkillRoundHandler changes

Behavior based on `role`:

| Role | Input | Output handling |
|------|-------|----------------|
| `contributor` | category slice | JSON list → stored per skill in context |
| `gate` | all contributor outputs merged | JSON list → replaces input in context, pipeline stops if empty |
| `lead` | gate output + artifact summaries | plan → injected into `DomainRules` for executor |
| `executor` | lead plan | artifact → persisted |

### Triage changes

- `discussion` pipelines: Triage via LLM as today
- `structured` / `hierarchical` pipelines: Triage replaced by `SkillGraphBuilder`
  — no LLM call, deterministic graph

### Fallback Behavior

Skills without an `orchestration` block default to `role: contributor` in a
`discussion`-type pipeline. This ensures backward compatibility — existing
pipelines and skills work unchanged without adding orchestration metadata.

### Convergence in Structured/Hierarchical Pipelines

`convergence_criteria` applies only to `discussion` pipelines. In `structured`
and `hierarchical` pipelines, each contributor makes a single LLM call and
produces its JSON output — no rounds, no convergence checks. "Done" means
the skill produced valid output.

---

## agentsmith.md: Updated Schema

```markdown
# Agent Smith Extensions

## orchestration
role: contributor | lead | gate | executor
runs_after: []                    # role types OR skill names this skill waits for
runs_before: []                   # role types OR skill names that wait for this skill
output: list | plan | artifact | verdict
parallel_with: []                 # skill names that run alongside this one
input_categories: []              # for structured pipelines: which data slice to receive

## output_format                  # required for gate and contributor in structured pipelines
(JSON schema inline)

## convergence_criteria           # for discussion pipelines only
- "..."
```

---

## Files to Create

- `AgentSmith.Application/Services/SkillGraphBuilder.cs`
- `AgentSmith.Contracts/Models/SkillGraph.cs`
- `AgentSmith.Contracts/Models/ExecutionStage.cs`
- `AgentSmith.Contracts/Models/SkillOrchestration.cs` — orchestration block model
- Updated `agentsmith.md` for all 25 skills (see skill graph above)

## Files to Modify

- `AgentSmith.Contracts/Models/SkillDefinition.cs` — add `SkillOrchestration` field
- `AgentSmith.Application/Services/Handlers/SkillRoundHandlerBase.cs` — role-aware execution
- `AgentSmith.Application/Services/Handlers/SecuritySkillRoundHandler.cs` — JSON output
- `AgentSmith.Application/Services/Handlers/TriageHandlerBase.cs` — skip for structured/hierarchical
- `AgentSmith.Infrastructure/Services/Skills/SkillLoader.cs` — parse orchestration block
- `AgentSmith.Contracts/Commands/PipelinePresets.cs` — pipeline type field
- All `config/skills/**/agentsmith.md` — add orchestration block

## Definition of Done

- [ ] `agentsmith.md` orchestration block parsed by SkillLoader
- [ ] `SkillGraphBuilder` builds deterministic execution graph from skill definitions
- [ ] `structured` and `hierarchical` pipelines skip LLM triage
- [ ] Contributors run in parallel, receive only their input slice
- [ ] `gate` role blocks pipeline if output is empty, logs rejected items
- [ ] `lead` role receives only gate-filtered output
- [ ] `executor` role receives lead plan, produces artifact
- [ ] `discussion` pipelines unchanged (triage + accumulation as today)
- [ ] All 25 `agentsmith.md` files updated with orchestration block
- [ ] Token usage reduced ~80% for structured pipelines
- [ ] false-positive-filter output flows into DeliverFindings
- [ ] All existing tests green
- [ ] New tests: SkillGraphBuilder, role-aware SkillRoundHandler

## Dependencies

- p57a (SKILL.md format migration) — agentsmith.md must exist per skill
- p55 (findings compression) — input_categories slicing reuses this logic.
  Category labels on raw findings come from StaticPatternScanner's pattern files
  (secrets.yaml, injection.yaml etc.) — this tagging is already in place.
