# Pipeline System

Everything Agent Smith does is a pipeline — an ordered list of commands executed in sequence.

## Commands and Handlers

Each pipeline step is a **command** with a matching **handler**. The command defines what needs to happen. The handler does it.

```
Pipeline: fix-bug
├── FetchTicketCommand       → FetchTicketHandler
├── CheckoutSourceCommand    → CheckoutSourceHandler
├── BootstrapProjectCommand  → BootstrapProjectHandler
├── LoadCodeMapCommand       → LoadCodeMapHandler
├── LoadCodingPrinciplesCommand   → LoadCodingPrinciplesHandler
├── LoadContextCommand       → LoadContextHandler
├── AnalyzeCodeCommand       → AnalyzeCodeHandler
├── GeneratePlanCommand      → GeneratePlanHandler
├── ApprovalCommand          → ApprovalHandler
├── AgenticExecuteCommand    → AgenticExecuteHandler
├── TestCommand              → TestHandler
├── WriteRunResultCommand    → WriteRunResultHandler
└── CommitAndPRCommand       → CommitAndPRHandler
```

Handlers receive a typed context object and return a `CommandResult` (success or failure with message).

## Pipeline Presets

Agent Smith ships with nine presets defined in `PipelinePresets.*.cs`. Step counts are total commands in the preset; many are pipeline-plumbing (PipelineNameInitializer, BootstrapCheck, BootstrapGate, LoadSkills, EmptyPlanCheck) and aren't user-visible.

| Preset | Steps | Use case |
|--------|-------|----------|
| `fix-bug` | 22 | Ticket → code → review → verify → test → PR (multi-repo cross-linked) |
| `add-feature` | 24 | Same flow + GenerateTests + GenerateDocs |
| `fix-no-test` | 21 | Like fix-bug but skips Test |
| `init-project` | 6 | Bootstrap `.agentsmith/` per repo (cross-linked via PrCrossLink) |
| `security-scan` | 14 | Static patterns + git history + dependency audit + AI panel (read-only) |
| `api-security-scan` | 9 | Nuclei + Spectral + AI panel against a live API |
| `legal-analysis` | 8 | Contract review with five legal specialists |
| `mad-discussion` | 9 | Multi-agent design discussion |
| `autonomous` | 3 | Open-ended operator-driven loop |

Authoritative source-of-truth is each `src/AgentSmith.Contracts/Commands/PipelinePresets.{Name}.cs` file — counts here track but may lag.

## Pipeline Types and Triage

Since Phase 64, every pipeline has a **type** that determines how skills are selected and orchestrated. The type is stored in `PipelineContext` under the `PipelineType` key.

| Type | Triage Method | Convergence | Handoffs |
|------|--------------|-------------|----------|
| **discussion** | LLM selects skills | Yes -- rounds until consensus | Free-text accumulation |
| **structured** | `SkillGraphBuilder` (deterministic) | No -- skipped | Typed JSON via `SkillOutputs` |
| **hierarchical** | `SkillGraphBuilder` (deterministic) | No -- gate veto | Typed JSON via `SkillOutputs` |

### SkillGraphBuilder

For **structured** and **hierarchical** pipelines, `SkillGraphBuilder` constructs a deterministic execution graph from skill metadata. Each skill declares `runs_after` and/or `runs_before` in its YAML definition. The builder performs a topological sort to produce an ordered list of `ExecutionStage` objects. Skills within the same stage run in parallel.

```
SkillGraphBuilder reads skill metadata:
  vuln-analyst:       role: executor,    runs_after: [false-positive-filter]
  auth-reviewer:      role: contributor
  injection-checker:  role: contributor
  secrets-detector:   role: contributor
  false-positive-filter: role: gate,     runs_after: [auth-reviewer, injection-checker, secrets-detector]

Topological sort produces:
  Stage 1 (contributors): auth-reviewer, injection-checker, secrets-detector  [parallel]
  Stage 2 (gate):         false-positive-filter                               [single]
  Stage 3 (executor):     vuln-analyst                                        [single]
```

A gate skill with `output: list` writes typed `List<Finding>` directly to `ExtractedFindings` in the pipeline context, bypassing raw text extraction.

### Discussion pipelines (LLM triage)

For **discussion** pipelines (mad-discussion, legal-analysis), triage still uses an LLM call to select relevant skills. Dynamic expansion and convergence checking work as before:

```
After LLM triage (3 skills selected):
  → SkillRoundCommand:analyst:1        ← inserted
  → SkillRoundCommand:critic:1         ← inserted
  → SkillRoundCommand:synthesizer:1    ← inserted
  → ConvergenceCheckCommand            ← inserted
  → (additional rounds if objections)
```

Skills without an `orchestration` block in their metadata default to the contributor role in discussion mode.

### Structured pipelines (deterministic graph)

For **structured** pipelines (security-scan, api-security-scan), the triage handler detects the pipeline type and delegates to `SkillGraphBuilder` instead of calling the LLM. Each skill runs exactly once. `ConvergenceCheck` is skipped entirely.

```
security-scan (structured):
  SecurityTriageCommand → SkillGraphBuilder
    Stage 1: SkillRoundCommand:auth-reviewer      ← parallel
             SkillRoundCommand:injection-checker   ← parallel
             SkillRoundCommand:secrets-detector    ← parallel
    Stage 2: SkillRoundCommand:false-positive-filter (gate → typed List<Finding>)
    Stage 3: SkillRoundCommand:vuln-analyst (executor)
  DeliverFindingsCommand ← reads typed findings directly
```

## PipelineContext

All commands share a `PipelineContext` — a key-value store that flows through the pipeline. Commands read from and write to it:

```csharp
// Write
pipeline.Set(ContextKeys.CodeMap, codeMap);

// Read
pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var plan);
```

This is how data flows between steps without tight coupling.

Since Phase 64, the following context keys support typed orchestration:

| Key | Type | Description |
|-----|------|-------------|
| `PipelineType` | `string` | `discussion`, `structured`, or `hierarchical` |
| `SkillGraph` | `ExecutionGraph` | Topologically sorted skill graph from `SkillGraphBuilder` |
| `SkillOutputs` | `Dictionary<string, object>` | Typed outputs from each completed skill |

## Custom Pipelines

You can define custom pipelines by specifying a command sequence in the project configuration. Each command name maps to a registered handler via dependency injection.
