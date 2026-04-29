# Pipelines

Agent Smith ships with **10 pipeline presets** — pre-built sequences of command handlers that cover the most common AI orchestration workflows.

## Pipeline Overview

| Pipeline | CLI Command | Steps | What It Does |
|----------|------------|-------|-------------|
| **fix-bug** | `agent-smith fix` | 14 | Ticket → branch → code → test → PR |
| **fix-no-test** | `agent-smith fix --no-test` | 13 | Like fix-bug, skips the test step |
| **add-feature** | `agent-smith feature` | 16 | fix-bug + generate tests + generate docs |
| **security-scan** | `agent-smith security-scan` | 18 | Multi-role code security review with SARIF output |
| **api-security-scan** | `agent-smith api-scan` | 9 | Nuclei + Spectral + AI specialist panel on live APIs |
| **legal-analysis** | `agent-smith legal` | 7 | Contract review with 5 legal specialist roles |
| **mad-discussion** | `agent-smith mad` | 9 | Multi-agent design discussion with convergence |
| **skill-manager** | `agent-smith skill-manager` | 6 | Discover, evaluate, and install skills |
| **autonomous** | `agent-smith autonomous` | 11 | Observe project, write tickets autonomously |
| **init-project** | `agent-smith init` | 3 | Bootstrap `.agentsmith/` directory in a repo |

All pipeline commands support `--dry-run` to preview the execution plan without running it. Utility commands (`compile-wiki`, `security-trend`) also support `--dry-run`.

## How Pipelines Work

Every pipeline is an ordered list of **commands**. Each command has a matching **handler** that does the actual work. Commands share a `PipelineContext` — a key-value store that flows data between steps.

```
Pipeline: fix-bug
├── FetchTicket          → reads ticket from GitHub/AzDO/Jira/GitLab
├── CheckoutSource       → clones repo, creates branch
├── BootstrapProject     → detects language, framework, project type
├── LoadCodeMap          → generates navigable code map
├── LoadCodingPrinciples      → loads coding standards from repo
├── LoadContext           → loads .agentsmith/context.yaml
├── AnalyzeCode          → scout agent maps relevant files
├── Triage               → selects specialist roles (coding pipeline)
├── GeneratePlan         → AI generates implementation plan
├── Approval             → waits for human OK (or runs headless)
├── AgenticExecute       → AI writes code in agentic loop
├── Test                 → runs test suite
├── WriteRunResult       → writes result.md with cost/token data
└── CommitAndPR          → commits, pushes, opens PR
```

## Dynamic Pipeline Expansion

Some commands insert follow-up steps at runtime. The **Triage** step inspects the problem and inserts `SkillRound` commands for each specialist role it selects. The **ConvergenceCheck** step evaluates whether all roles have met their convergence criteria — if not, it inserts another round.

```
Static pipeline:
  ... → Triage → ConvergenceCheck → CompileDiscussion → ...

After Triage expands:
  ... → Triage → SkillRound(vuln-analyst, r1) → SkillRound(auth-reviewer, r1)
      → ConvergenceCheck → CompileDiscussion → ...

After ConvergenceCheck finds objections:
  ... → ConvergenceCheck → SkillRound(auth-reviewer, r2)
      → ConvergenceCheck → CompileDiscussion → ...
```

## Custom Pipelines

You can define custom pipelines in `agentsmith.yml`:

```yaml
pipelines:
  my-custom-pipeline:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - BootstrapProjectCommand
      - AgenticExecuteCommand
      - CommitAndPRCommand
```

Then reference it in your project config:

```yaml
projects:
  my-project:
    pipeline: my-custom-pipeline
```

## Pipeline Types

Since Phase 64, Agent Smith classifies every pipeline into one of three **orchestration types**. The type determines how skills are selected, how they communicate, and whether convergence rounds apply.

| Type | Triage | Skill Runs | Handoffs | Convergence |
|------|--------|------------|----------|-------------|
| **discussion** | LLM selects skills | Multiple rounds possible | Free-text accumulation | Yes -- rounds until consensus |
| **structured** | Deterministic graph (`SkillGraphBuilder`) | Single call per skill | Typed JSON (`SkillOutputs`) | No -- skipped |
| **hierarchical** | Deterministic graph (`SkillGraphBuilder`) | Lead then contributors then gates | Typed JSON | No -- gate veto instead |

### Discussion pipelines

**mad-discussion**, **legal-analysis**. LLM-based triage selects relevant skills. Skills run in rounds with free-text accumulation. `ConvergenceCheck` evaluates whether all skills agree; if not, objecting skills re-run until consensus or the max round limit. Skills without an orchestration block default to contributor role in discussion mode.

### Structured pipelines

**security-scan**, **api-security-scan**. `SkillGraphBuilder` builds a deterministic execution graph from `runs_after`/`runs_before` declarations in skill metadata. No LLM triage. Skills are topologically sorted into stages: contributors (parallel, category-sliced) run first, then a gate (e.g., false-positive-filter) that can veto findings, then an executor. Each skill runs exactly once with typed JSON handoffs. The gate produces typed `List<Finding>` output that flows directly to `DeliverFindings`, bypassing raw text extraction. This achieves approximately 80% token reduction compared to discussion mode.

### Hierarchical pipelines

**fix-bug**, **add-feature**, **fix-no-test**. A lead skill drives the workflow, delegating to contributor skills and validating through gate skills. The execution graph is deterministic (built by `SkillGraphBuilder`), but the lead has authority to direct contributors. No convergence rounds -- gates provide pass/fail verdicts.

### Context keys

The pipeline type is stored in `PipelineContext` under the `PipelineType` key. Additional context keys introduced in Phase 64:

| Key | Type | Description |
|-----|------|-------------|
| `PipelineType` | `string` | `discussion`, `structured`, or `hierarchical` |
| `SkillGraph` | `ExecutionGraph` | The topologically sorted skill graph |
| `SkillOutputs` | `Dictionary<string, object>` | Typed outputs from each skill, keyed by skill name |

## Next Steps

- [Fix Bug / Add Feature](fix-and-feature.md) — the coding pipelines
- [Security Scan](security-scan.md) — code security review
- [API Scan](api-scan.md) — live API scanning
- [Legal Analysis](legal-analysis.md) — contract review
- [MAD Discussion](mad-discussion.md) — multi-agent design debate
- [Skill Manager](skill-manager.md) — autonomous skill discovery and installation
- [Autonomous](autonomous.md) — agent-driven project improvement tickets
