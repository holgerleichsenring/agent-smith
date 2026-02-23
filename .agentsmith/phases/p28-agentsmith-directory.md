# Phase 28: `.agentsmith/` Directory Structure

## Goal

Introduce a unified `.agentsmith/` directory as the single home for all agent-generated
and agent-consumed meta-files. Replace the `prompts/` directory. Add pipeline steps
for context loading (`LoadContextCommand`) and run result tracking (`WriteRunResultCommand`).

Every successful agent run records itself in `.agentsmith/runs/` and appends an entry
to `state.done` in `context.yaml` — creating an auditable decision-level changelog.

## Directory Structure

```
.agentsmith/
  context.yaml              # Project metadata (was .context.yaml in repo root)
  code-map.yaml             # Architecture map (was code-map.yaml in repo root)
  coding-principles.md      # Coding conventions (was config/coding-principles.md)
  template.context.yaml     # CCS template for context generation
  phases/                   # Phase planning docs (human + AI co-created)
    p23-multi-repo.md
    p25-pr-review.md
    p28-agentsmith-directory.md
  runs/                     # Per-ticket execution logs (agent-generated)
    r01-fix-null-check/
      plan.md               # GeneratePlan output, human-readable
      result.md             # Summary, changed files, tokens, duration
```

## Run Tracking Format

After each successful run, the agent:
1. Writes `plan.md` + `result.md` to `.agentsmith/runs/r{NN}-{slug}/`
2. Appends to `state.done` in `context.yaml`:

```yaml
state:
  done:
    p01: "Solution structure, domain entities, contracts"
    p27: "Structured command UI: Slack modals"
    r01: "fix #42: Null check in parser service"
    r02: "feat #87: Retry logic for API calls"
```

Run number `r{NN}` is determined by reading the last `r`-prefixed key in `state.done`
and incrementing. Failed runs are recorded only in `.agentsmith/runs/` (not in `state.done`).

### result.md Format

```markdown
# r{NN}: {ticket title}

- **Ticket**: #{id} — {title}
- **Date**: {ISO date}
- **Result**: success | failed
- **Duration**: {seconds}s
- **Tokens**: {input} in / {output} out (${cost})
- **Model**: {model id}

## Changed Files
- [Create] src/Foo/Bar.cs
- [Modify] src/Foo/Baz.cs

## Summary
{Agent-generated one-paragraph summary of what was done and why}
```

## Updated Pipeline

```
1.  FetchTicketCommand
2.  CheckoutSourceCommand
3.  BootstrapProjectCommand       # generates .agentsmith/ files if missing
4.  LoadCodeMapCommand            # reads .agentsmith/code-map.yaml
5.  LoadCodingPrinciplesCommand   # reads .agentsmith/coding-principles.md
6.  LoadContextCommand            # reads .agentsmith/context.yaml        ← NEW
7.  AnalyzeCodeCommand
8.  GeneratePlanCommand
9.  ApprovalCommand
10. AgenticExecuteCommand
11. TestCommand
12. WriteRunResultCommand         # writes runs/ + updates context.yaml   ← NEW
13. CommitAndPRCommand            # commits everything incl. .agentsmith/
```

## Implementation Steps

### Step 1: Migrate Agent Smith's own files

Move files (git mv where possible):
- `prompts/.context.yaml` → `.agentsmith/context.yaml`
- `prompts/coding-principles.md` → `.agentsmith/coding-principles.md`
- `prompts/template.context.yaml` → `.agentsmith/template.context.yaml`
- `prompts/phase-23-multi-repo.md` → `.agentsmith/phases/p23-multi-repo.md`
- `prompts/phase-25-pr-review-iteration.md` → `.agentsmith/phases/p25-pr-review.md`

Create: `.agentsmith/runs/.gitkeep`

Delete: `prompts/` directory

Update `.agentsmith/context.yaml`:
- Change `state.planned` references from `prompts/phase-*` → `.agentsmith/phases/p*`
- Add `p28` to `state.done`

### Step 2: Update BootstrapProjectHandler paths

**Modify:** `src/AgentSmith.Application/Services/Handlers/BootstrapProjectHandler.cs`

Change generated file paths from repo root to `.agentsmith/`:
```csharp
// Before
private const string ContextFileName = ".context.yaml";
private const string CodeMapFileName = "code-map.yaml";
// ...
var contextFilePath = Path.Combine(repoPath, ContextFileName);

// After
private const string AgentSmithDir = ".agentsmith";
private const string ContextFileName = "context.yaml";
private const string CodeMapFileName = "code-map.yaml";
// ...
var agentDir = Path.Combine(repoPath, AgentSmithDir);
Directory.CreateDirectory(agentDir);
var contextFilePath = Path.Combine(agentDir, ContextFileName);
```

Same for `TryGenerateCodeMapAsync` — write to `.agentsmith/code-map.yaml`.

### Step 3: Update LoadCodeMapHandler path

**Modify:** `src/AgentSmith.Application/Services/Handlers/LoadCodeMapHandler.cs`

```csharp
// Before
var codeMapPath = Path.Combine(context.Repository.LocalPath, "code-map.yaml");

// After
var codeMapPath = Path.Combine(context.Repository.LocalPath, ".agentsmith", "code-map.yaml");
```

### Step 4: Update LoadCodingPrinciplesHandler + default path

**Modify:** `src/AgentSmith.Application/Services/CommandContextFactory.cs`

```csharp
// Before
var path = project.CodingPrinciplesPath ?? "config/coding-principles.md";

// After
var path = project.CodingPrinciplesPath ?? ".agentsmith/coding-principles.md";
```

### Step 5: Add LoadContextCommand

**New:** `src/AgentSmith.Application/Models/LoadContextContext.cs`
```csharp
public sealed record LoadContextContext(
    Repository Repository, PipelineContext Pipeline) : ICommandContext;
```

**New:** `src/AgentSmith.Application/Services/Handlers/LoadContextHandler.cs`
```csharp
public sealed class LoadContextHandler(ILogger<LoadContextHandler> logger)
    : ICommandHandler<LoadContextContext>
{
    public async Task<CommandResult> ExecuteAsync(
        LoadContextContext context, CancellationToken ct = default)
    {
        var path = Path.Combine(context.Repository.LocalPath, ".agentsmith", "context.yaml");
        if (!File.Exists(path))
        {
            logger.LogInformation("No .agentsmith/context.yaml found, continuing without");
            return CommandResult.Ok("No context file found, continuing without");
        }

        var content = await File.ReadAllTextAsync(path, ct);
        context.Pipeline.Set(ContextKeys.ProjectContext, content);
        logger.LogInformation("Loaded context.yaml ({Chars} chars)", content.Length);
        return CommandResult.Ok($"Loaded context ({content.Length} chars)");
    }
}
```

**Modify:** `src/AgentSmith.Contracts/Commands/ContextKeys.cs`
- Add: `public const string ProjectContext = "ProjectContext";`

### Step 6: Add WriteRunResultCommand

**New:** `src/AgentSmith.Application/Models/WriteRunResultContext.cs`
```csharp
public sealed record WriteRunResultContext(
    Repository Repository,
    Plan Plan,
    Ticket Ticket,
    IReadOnlyList<CodeChange> Changes,
    PipelineContext Pipeline) : ICommandContext;
```

**New:** `src/AgentSmith.Application/Services/Handlers/WriteRunResultHandler.cs`

Responsibilities:
1. Read `.agentsmith/context.yaml`, parse `state.done`, find last `r{NN}` key
2. Increment to `r{NN+1}`, generate slug from ticket title
3. Create `.agentsmith/runs/r{NN}-{slug}/` directory
4. Write `plan.md` from `Plan` object
5. Write `result.md` with summary, changed files, token usage, duration
6. Append `r{NN}: "{type} #{id}: {title}"` to `state.done` in context.yaml
7. Store run directory path in pipeline context for CommitAndPR

Uses `YamlDotNet` for safe context.yaml modification (already a dependency).

Token usage and duration: read from `PipelineContext` (already tracked by `RunCostSummary`).

**Modify:** `src/AgentSmith.Contracts/Commands/ContextKeys.cs`
- Add: `public const string RunResult = "RunResult";`

### Step 7: Wire into CommandContextFactory + PipelineExecutor

**Modify:** `src/AgentSmith.Application/Services/CommandContextFactory.cs`
- Add `"LoadContextCommand"` case → `CreateLoadContext(pipeline)`
- Add `"WriteRunResultCommand"` case → `CreateWriteRunResult(pipeline)`

**Modify:** `src/AgentSmith.Application/Services/PipelineExecutor.cs`
- Add `LoadContextContext` and `WriteRunResultContext` to `ExecuteCommandAsync` switch
- Add step labels: `"LoadContextCommand" => "Loading project context"`
  and `"WriteRunResultCommand" => "Writing run result"`

### Step 8: Register DI

**Modify:** `src/AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs`
- `services.AddTransient<ICommandHandler<LoadContextContext>, LoadContextHandler>();`
- `services.AddTransient<ICommandHandler<WriteRunResultContext>, WriteRunResultHandler>();`

### Step 9: Update pipeline configs

**Modify:** `config/agentsmith.yml`

Add `LoadContextCommand` after `LoadCodingPrinciplesCommand` and
`WriteRunResultCommand` before `CommitAndPRCommand` in all pipelines.

Update `coding_principles_path` to `.agentsmith/coding-principles.md`.

**Modify:** `config/agentsmith.example.yml` — same changes.

### Step 10: Inject context into prompts

**Modify:** `src/AgentSmith.Infrastructure/Services/Providers/Agent/AgentPromptBuilder.cs`

Add optional `projectContext` parameter to `BuildExecutionSystemPrompt`
and `BuildPlanSystemPrompt`:

```csharp
public static string BuildPlanSystemPrompt(
    string codingPrinciples, string? codeMap = null, string? projectContext = null)
```

When present, prepend a `## Project Context` section with the context.yaml content.
This gives the agent immediate awareness of the project's stack, architecture,
integration points, and recent change history.

**Modify:** `src/AgentSmith.Infrastructure/Services/Providers/Agent/ClaudeAgentProvider.cs`
(and OpenAI/Gemini providers) — pass `projectContext` from pipeline to prompt builder.

**Modify:** `src/AgentSmith.Application/Services/CommandContextFactory.cs`
- `CreateGeneratePlan` and `CreateAgenticExecute`: add `TryGet<string>(ContextKeys.ProjectContext, ...)`

### Step 11: Tests

**New:** `tests/AgentSmith.Tests/Services/LoadContextHandlerTests.cs`
- `ExecuteAsync_FileExists_LoadsContent`
- `ExecuteAsync_FileNotFound_ReturnsOk`

**New:** `tests/AgentSmith.Tests/Services/WriteRunResultHandlerTests.cs`
- `ExecuteAsync_FirstRun_CreatesR01`
- `ExecuteAsync_ExistingRuns_IncrementsNumber`
- `ExecuteAsync_WritesPlanAndResultFiles`
- `ExecuteAsync_UpdatesContextYaml`

**Modify:** `tests/AgentSmith.Tests/Services/BootstrapProjectHandlerTests.cs`
- Update expected paths to `.agentsmith/`

**Modify:** `tests/AgentSmith.Tests/Services/LoadCodeMapHandlerTests.cs`
- Update expected path to `.agentsmith/code-map.yaml`

## File Summary

| Action | Files |
|--------|-------|
| Move (5) | context.yaml, coding-principles.md, template.context.yaml, p23, p25 |
| Delete (1) | `prompts/` directory |
| New (4) | LoadContextContext, LoadContextHandler, WriteRunResultContext, WriteRunResultHandler |
| New test (2) | LoadContextHandlerTests, WriteRunResultHandlerTests |
| Modify (11) | BootstrapProjectHandler, LoadCodeMapHandler, CommandContextFactory, PipelineExecutor, ContextKeys, AgentPromptBuilder, ClaudeAgentProvider, OpenAIAgentProvider, GeminiAgentProvider, ServiceCollectionExtensions (App), agentsmith.yml |
| Modify test (2) | BootstrapProjectHandlerTests, LoadCodeMapHandlerTests |

## Key Design Decisions

1. **`.agentsmith/` not `.agent/`** — clearly branded, no conflict with other tools
2. **Run counter `r{NN}` not dates** — unique, simple, never collides, agent reads & increments
3. **Failed runs not in `state.done`** — they did not change project state, detail in `runs/` only
4. **`context.yaml` injected into prompts** — the agent now knows the project's full identity
5. **`WriteRunResult` before `CommitAndPR`** — run artifacts are committed with the code changes

## Verification

1. `dotnet build` after each step
2. `dotnet test` — all existing + ~6 new tests pass
3. Pipeline smoke test: `dotnet run -- --ticket 1 --project agent-smith-test`
4. Verify `.agentsmith/runs/r01-*/plan.md` + `result.md` exist in PR
5. Verify `state.done` in `context.yaml` contains `r01` entry
