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
├── LoadDomainRulesCommand   → LoadDomainRulesHandler
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

Agent Smith ships with seven presets defined in `PipelinePresets.cs`:

| Preset | Steps | Use case |
|--------|-------|----------|
| `fix-bug` | 13 | Ticket → code → test → PR |
| `add-feature` | 14 | Same + GenerateTests + GenerateDocs |
| `fix-no-test` | 12 | Like fix-bug but skips test step |
| `init-project` | 3 | Bootstrap .agentsmith/ directory |
| `security-scan` | 11 | Multi-role code security review |
| `api-security-scan` | 8 | Nuclei + Spectral + AI panel |
| `mad-discussion` | 7 | Multi-agent design discussion |

## Dynamic Pipeline Expansion

Some commands insert follow-up commands at runtime. For example, `ApiSecurityTriageCommand` determines which specialist roles are needed and inserts `SkillRoundCommand` instances for each:

```
Before triage:
  [5/8] ApiSecurityTriageCommand
  [6/8] ConvergenceCheckCommand

After triage (2 roles selected):
  [5/11] ApiSecurityTriageCommand          ✓
  [6/11] SkillRoundCommand:auth-tester:1       ← inserted
  [7/11] SkillRoundCommand:api-design-auditor:1 ← inserted
  [8/11] ConvergenceCheckCommand               ← inserted
  [9/11] ConvergenceCheckCommand
  ...
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

## Custom Pipelines

You can define custom pipelines by specifying a command sequence in the project configuration. Each command name maps to a registered handler via dependency injection.
