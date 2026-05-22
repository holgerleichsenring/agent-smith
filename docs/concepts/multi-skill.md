# Multi-Skill Architecture

For complex tasks, Agent Smith assembles a panel of AI specialists. Depending on the pipeline type, these specialists may debate an approach, execute in a deterministic graph, or follow a lead-then-contribute pattern.

## Pipeline Types

Agent Smith supports three pipeline types. Each pipeline selects its execution strategy automatically based on the orchestration metadata declared in skills.

### Discussion Pipelines

Used by: **mad-discussion**, **legal-analysis**

1. **Triage** — LLM analyzes the ticket and codebase, selects relevant roles
2. **Skill Rounds** — each role analyzes the problem and contributes typed observations
3. **Convergence** — `ConvergenceCheckHandler` evaluates aggregated observations and decides whether to run another round (hard cap, default 3)
4. **Execution** — the consolidated plan goes to the agentic loop

Observations are typed (see [Multi-Agent Orchestration](multi-agent-orchestration.md)): each carries `Concern`, `Confidence`, `Blocking`, and `EvidenceMode`. The free-text `OBJECT` / `AGREE` / `SUGGEST` prose-regex convergence path from earlier versions was retired in p0146c. Discussion pipelines still iterate rounds — but the iteration signal is the structured observation, not parsed prose.

### Structured Pipelines

Used by: **security-scan**, **api-security-scan**

1. **Graph Construction** -- `SkillGraphBuilder` reads orchestration metadata from all skills and produces a deterministic execution graph via topological sort
2. **Stage Execution** -- skills execute stage by stage; skills within a stage may run in parallel
3. **Gate Check** -- gate skills can veto the pipeline (see Roles below)
4. **Output** -- typed JSON handoffs between stages, no free-text accumulation

Structured pipelines skip LLM triage entirely. Each skill receives a single LLM call with typed input/output. The execution order is fully determined by the `runs_after`, `runs_before`, and `parallel_with` declarations in each skill's orchestration metadata.

### Hierarchical Pipelines

Used by: **fix-bug**, **add-feature**

1. **Lead First** -- the lead skill runs first and produces a plan/directive
2. **Contributors** -- contributor skills run next; the lead's plan is injected into each contributor's context
3. **Gates** -- gate skills evaluate the accumulated output and can block the pipeline
4. **Execution** -- executor skills act in the world based on the consolidated plan

Like structured pipelines, hierarchical pipelines use `SkillGraphBuilder` for deterministic ordering and skip LLM triage.

## Skill Roles

Every skill has a role that determines its behavior in the pipeline. The role is declared in the skill's `agentsmith.md` orchestration section.

| Role | Output Type | Blocking | Behavior |
|------|------------|----------|----------|
| **contributor** | list | No | Analyzes and produces a JSON list. The default role for skills without an orchestration block. |
| **lead** | plan | No | Runs first in hierarchical pipelines. Produces a plan/directive that is injected into all subsequent skills. |
| **gate** | verdict or list | Yes | Can block the pipeline. With `output: verdict`, emits true/false. With `output: list`, writes typed `List<Finding>` to `ExtractedFindings`; an empty list stops the pipeline. |
| **executor** | artifact | No | Acts in the world (creates files, runs commands). Produces an artifact as output. |

### Gate Veto Mechanics

Gates have two veto mechanisms depending on their output type:

- **verdict gate** -- returns a boolean. `false` stops the pipeline.
- **list gate** -- writes findings to `ExtractedFindings`. An empty list means "nothing to act on" and stops the pipeline. When a gate produces findings directly, the `ExtractFindingsHandler` step is skipped since the gate already populated the data.

## Execution Graph

For structured and hierarchical pipelines, the `SkillGraphBuilder` constructs an execution graph:

1. Reads each skill's `runs_after`, `runs_before`, and `parallel_with` declarations
2. Performs a topological sort to determine stage ordering
3. Groups skills into `ExecutionStage` instances, each labeled with its role (lead, gate, executor, or contributors)
4. Skills within a stage run in the same phase; the `parallel_with` declaration allows concurrent execution

The graph is fully deterministic -- the same set of skills always produces the same execution order.

## Backward Compatibility

Skills without an `## orchestration` block in their `SKILL.md` default to `role: analyst` with `output: list`. LLM triage selects them and they contribute observations per round; convergence is checked structurally on the aggregated observations.

## Skill Categories

Agent Smith ships with role sets for different domains:

| Directory | Roles | Used by | Pipeline Type |
|-----------|-------|---------|---------------|
| `config/skills/coding/` | Architect, Backend Dev, Tester, DBA, Security, DevOps, Frontend, Product Owner | fix-bug, add-feature | hierarchical |
| `config/skills/security/` | Vulnerability Analyst, Auth Reviewer, Injection Checker, Secrets Detector, False Positive Filter | security-scan | structured |
| `config/skills/api-security/` | API Design Auditor, Auth Tester, Vulnerability Analyst, False Positive Filter | api-scan | structured |
| `config/skills/legal/` | Contract Analyst, Compliance Checker, Risk Assessor, Liability Analyst, Clause Negotiator | legal-analysis | discussion |
| `config/skills/mad/` | Philosopher, Dreamer, Realist, Devil's Advocate, Silencer | mad-discussion | discussion |

## Discussion Flow

Discussion pipelines (mad-discussion, legal-analysis) run in rounds. Each round, every selected role emits one or more **typed observations** — `Concern` text, `Confidence` (0–100), `Blocking` flag, `EvidenceMode` (`Potential` or `AnalyzedFromSource`):

```
Round 1
  Architect:  Concern="layering violation — Contracts needs the new interface"
              Confidence=85  Blocking=true   EvidenceMode=AnalyzedFromSource
  DBA:        Concern="schema change is backward compatible — verified"
              Confidence=90  Blocking=false  EvidenceMode=AnalyzedFromSource
  Tester:     Concern="add integration test for the new endpoint"
              Confidence=75  Blocking=false  EvidenceMode=Potential

Round 2
  Architect:  Concern="acknowledged — interface added to plan"
              Confidence=90  Blocking=false  EvidenceMode=AnalyzedFromSource
  Tester:     Concern="integration test included in plan"
              Confidence=85  Blocking=false  EvidenceMode=AnalyzedFromSource

→ ConvergenceCheckHandler: no blocking observations with Confidence ≥ 70.
→ Consolidated plan goes to execution.
```

`ConvergenceCheckHandler` runs against aggregated `SkillObservation`s, not prose. Round count is capped (default 3) to prevent endless debate. Blocking observations with `Confidence < 70` are auto-downgraded to `Blocking=false` with a structured log entry — speculation never breaks the pipeline.

## Project-Specific Skills

Projects can override or extend the default roles. Put a `skills.yaml` in your project's `.agentsmith/` directory to select which roles are active:

```yaml
roles:
  - architect
  - backend-developer
  - tester
```

This activates only those three roles for the project, ignoring DBA, DevOps, etc.

## Custom Roles

Create your own role by adding a YAML file to your skills directory:

```yaml
name: performance-engineer
display_name: "Performance Engineer"
emoji: "⚡"
description: "Evaluates performance impact of proposed changes"

triggers:
  - database
  - api-endpoint
  - caching

rules: |
  You review changes for performance implications.
  Flag N+1 queries, missing indexes, unbounded collections,
  and unnecessary allocations.

convergence_criteria:
  - "No performance concerns above medium severity"
```
