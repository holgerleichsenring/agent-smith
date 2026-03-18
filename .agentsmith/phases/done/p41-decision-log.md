# Phase 41: Decision Log — Why, not What

## Goal

Every architectural decision, tooling choice, and trade-off the agent makes is
automatically recorded in `.agentsmith/decisions.md` — one sentence, at the moment
of the decision, in human-readable language.

Not: "DuckDB is used."
But: "DuckDB over direct OneLake access: RBAC setup too complex for first run,
DuckDB reads Parquet natively."

This is not documentation. This is **Institutional Memory**.

---

## The Problem

AI-generated code is a black box. When the system fails, the human does not know:
- Why this architecture?
- Why this tool?
- What was the trade-off?

Run History (`runs/`) captures *what* happened. `decisions.md` captures *why*
decisions were made the way they were.

A developer who wrote their own code knows all strengths and weaknesses. With
AI-generated code, exactly this knowledge is missing — `decisions.md` closes
that gap.

---

## Format: `decisions.md`

```markdown
# Decision Log

## Architecture
- **LinkedList for Pipeline**: Runtime insertion of Commands required —
  append-only List would not support Cascading Commands.
- **Redis Streams over Queue**: Fan-out to multiple consumers needed (Dispatcher +
  Monitoring) — simple queue can only serve one consumer.

## Tooling
- **DuckDB over direct OneLake**: RBAC setup via abfss:// too complex for first
  run — DuckDB reads Parquet natively without Azure authentication.
- **YamlDotNet over JSON**: Human-readable config is a design principle — YAML
  allows comments, JSON does not.

## Implementation
- **Sealed classes as default**: Prevents accidental inheritance — explicit
  opt-in instead of accidental extension points.
- **Records for DTOs**: Immutability by default for Command Contexts — side
  effects in the pipeline excluded.

## TradeOff
- **No local Dynamics**: No Dataverse instance locally available — development
  always against real dev environment, increases feedback latency.
- **FileSystemWatcher for Legal-Inbox**: Polling would be more stable, but FSW
  is sufficient for first run — conscious choice for simplicity over robustness.
```

---

## When Decisions Are Written

The agent writes to `decisions.md` **at the moment of the decision** — not
afterwards, not as post-processing. Three triggers:

### 1. GeneratePlan Step
When the agent creates the plan and makes tool, architecture, or implementation
decisions, each decision is captured. The plan JSON response includes a `decisions`
array alongside `summary` and `steps`. The handler writes them via `IDecisionLogger`
after parsing.

### 2. AgenticExecute Step
When the agent deviates from the original plan during execution (e.g. because a
library call does not work as expected), it calls the `log_decision` tool immediately
to document why.

### 3. Init Project (Bootstrap)
When bootstrapping a new project: why a specific framework was detected, why certain
skills were activated, why specific coding principles were derived. The LLM generates
these decisions; the handler writes them via `IDecisionLogger`.

---

## Implementation

### 1. Interface in `AgentSmith.Contracts`

```csharp
// src/AgentSmith.Contracts/Decisions/IDecisionLogger.cs
namespace AgentSmith.Contracts.Decisions;

public interface IDecisionLogger
{
    Task LogAsync(string? repoPath, DecisionCategory category, string decision,
                  CancellationToken cancellationToken = default);
}

public enum DecisionCategory
{
    Architecture,
    Tooling,
    Implementation,
    TradeOff
}
```

**Tell-don't-ask**: `repoPath` is nullable. Handlers always call `LogAsync` —
they never check whether a repo exists. The implementation decides what to do:
- `FileDecisionLogger`: writes to `decisions.md` if repoPath is provided, skips if null
- `InMemoryDecisionLogger`: no-op (for pipelines without a repo like legal-analysis, MAD)

Two implementations, DI registration determines which one is used.

### 2. Infrastructure — Two Implementations (Tell-don't-ask)

**`FileDecisionLogger`** — for coding pipelines (have a repo):

```csharp
// src/AgentSmith.Infrastructure.Core/Services/FileDecisionLogger.cs
public sealed class FileDecisionLogger(ILogger<FileDecisionLogger> logger) : IDecisionLogger
{
    public async Task LogAsync(string? repoPath, DecisionCategory category,
                               string decision, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(repoPath))
        {
            logger.LogDebug("No repo path provided, skipping file write");
            return;
        }
        // ... write to .agentsmith/decisions.md (SemaphoreSlim protected)
    }
}
```

**`InMemoryDecisionLogger`** — for non-repo pipelines (legal-analysis, MAD):

```csharp
// src/AgentSmith.Infrastructure.Core/Services/InMemoryDecisionLogger.cs
public sealed class InMemoryDecisionLogger(ILogger<InMemoryDecisionLogger> logger) : IDecisionLogger
{
    public Task LogAsync(string? repoPath, DecisionCategory category,
                         string decision, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Decision logged in-memory [{Category}]: {Decision}", category, decision);
        return Task.CompletedTask;
    }
}
```

Key design decisions:
- **Tell-don't-ask**: Handlers always call `LogAsync` — never check for repo existence
- **DI determines behavior**: `FileDecisionLogger` (default) or `InMemoryDecisionLogger`
- **`SemaphoreSlim(1,1)`** for thread safety on concurrent pipeline runs
- **`repoPath` nullable** — `FileDecisionLogger` skips file write if null
- **Agent delivers formatted markdown** (`**Name**: reason`) — no parsing needed
- **`InsertUnderSection`** appends to existing section, no overwrites

### 3. Plan Response Extension

The JSON response format for plan generation adds a `decisions` array:

```json
{
  "summary": "...",
  "steps": [...],
  "decisions": [
    { "category": "Architecture", "decision": "**Redis Streams over RabbitMQ**: fan-out to multiple consumers required" },
    { "category": "Tooling", "decision": "**YamlDotNet over System.Text.Json**: human-readable config with comments" }
  ]
}
```

**Plan entity** gets a new property:

```csharp
// src/AgentSmith.Domain/Entities/Plan.cs — add property
public IReadOnlyList<PlanDecision> Decisions { get; }
```

```csharp
// src/AgentSmith.Domain/Entities/PlanDecision.cs — new record
public sealed record PlanDecision(string Category, string Decision);
```

**PlanParser** parses `decisions` (optional, defaults to empty list):

```csharp
var decisions = root.TryGetProperty("decisions", out var dArr)
    ? dArr.EnumerateArray().Select(ParseDecision).ToList()
    : new List<PlanDecision>();
```

**GeneratePlanHandler** writes decisions after parsing (tell-don't-ask — always
calls logger, passes nullable repoPath, stores in pipeline for `result.md`):

```csharp
if (plan.Decisions.Count > 0)
{
    context.Pipeline.TryGet<Repository>(ContextKeys.Repository, out var repo);
    await WriteDecisionsAsync(repo?.LocalPath, plan.Decisions, cancellationToken);
    StoreDecisionsInPipeline(context.Pipeline, plan.Decisions);
}
```

### 4. System Prompt Changes

**`AgentPromptBuilder.BuildPlanSystemPrompt()`** — add to JSON format spec:

```
## Respond in JSON format:
{
  "summary": "Brief summary of what needs to be done",
  "steps": [
    { "order": 1, "description": "...", "target_file": "...", "change_type": "Create|Modify|Delete" }
  ],
  "decisions": [
    { "category": "Architecture|Tooling|Implementation|TradeOff", "decision": "**DecisionName**: reason in one sentence why, not what" }
  ]
}

For every architectural, tooling, or implementation decision in your plan, add an
entry to the decisions array. Format: "**Decision name**: reason why, not what."
If the plan is straightforward with no significant decisions, the array may be empty.
```

**`AgentPromptBuilder.BuildExecutionSystemPrompt()`** — add to Instructions:

```
- When you deviate from the plan or make a non-trivial implementation decision,
  call the log_decision tool immediately. One sentence. Why, not what.
  Format: "**Decision name**: reason in one sentence"
```

### 5. Tool Definitions — All Three Providers

**`ToolDefinitions.cs` (Anthropic)**:

```csharp
public static Tool LogDecision => CreateTool(
    "log_decision",
    "Log an architectural, tooling, or implementation decision with its reason. " +
    "Call this when deviating from the plan or making a non-trivial decision during execution.",
    new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["category"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = new JsonArray("Architecture", "Tooling", "Implementation", "TradeOff"),
                ["description"] = "Decision category."
            },
            ["decision"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "Format: '**DecisionName**: reason why, not what'"
            }
        },
        ["required"] = new JsonArray("category", "decision")
    });

public static IList<Tool> All => new List<Tool>
{
    ReadFile, WriteFile, ListFiles, RunCommand, LogDecision
};
```

**`OpenAiToolDefinitions.cs`** — same tool in OpenAI ChatTool format.
**`GeminiToolDefinitions.cs`** — same tool in Gemini FunctionDeclaration format.

Scout tools remain unchanged (read-only).

### 6. ToolExecutor — New Case

```csharp
// In ToolExecutor.ExecuteAsync switch
"log_decision" => LogDecision(input),
```

```csharp
private string LogDecision(JsonNode? input)
{
    var category = GetStringParam(input, "category");
    var decision = GetStringParam(input, "decision");

    // Store for later batch-write by the handler
    _decisions.Add(new PlanDecision(category, decision));
    logger.LogDebug("Decision logged [{Category}]: {Decision}", category, decision);
    return $"Decision logged: [{category}] {decision}";
}
```

`ToolExecutor` gets a new `_decisions` list and `GetDecisions()` method,
mirroring `_changes`/`GetChanges()`. The AgenticExecuteHandler (or provider)
calls `IDecisionLogger` after the loop completes.

This avoids injecting `IDecisionLogger` into `ToolExecutor` (which has no DI).

### 7. Provider Changes — Writing Decisions After Execution

**`ClaudeAgentProvider.ExecutePlanAsync()`** returns decisions:

```csharp
// After loop.RunAsync():
var decisions = toolExecutor.GetDecisions();
return new AgentExecutionResult(changes, costSummary, (int)sw.Elapsed.TotalSeconds, decisions);
```

**`AgentExecutionResult`** gets a new property:

```csharp
public IReadOnlyList<PlanDecision> Decisions { get; }
```

**`AgenticExecuteHandler`** writes decisions after execution + stores in pipeline:

```csharp
if (result.Decisions is { Count: > 0 })
{
    foreach (var d in result.Decisions)
    {
        if (Enum.TryParse<DecisionCategory>(d.Category, true, out var cat))
            await decisionLogger.LogAsync(context.Repository.LocalPath, cat, d.Decision, ct);
    }
    StoreDecisionsInPipeline(context.Pipeline, result.Decisions);
}
```

Same pattern for `OpenAiAgentProvider` and `GeminiAgentProvider`.

### 7a. Decisions in `result.md` (Pipeline Flow)

Decisions flow through `PipelineContext` via `ContextKeys.Decisions` to `result.md`:

1. `GeneratePlanHandler` + `AgenticExecuteHandler` store decisions via
   `StoreDecisionsInPipeline()` (append to existing list — both handlers may contribute)
2. `WriteRunResultHandler` reads `List<PlanDecision>` from `ContextKeys.Decisions`
3. `RunResultFormatter.FormatResult` renders a `## Decisions` section, grouped by category

```csharp
// RunResultFormatter — new internal method
internal static void AppendDecisions(StringBuilder sb, IReadOnlyList<PlanDecision>? decisions)
{
    if (decisions is null || decisions.Count == 0) return;
    sb.AppendLine();
    sb.AppendLine("## Decisions");
    var grouped = decisions.GroupBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
                           .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
    foreach (var group in grouped)
    {
        sb.AppendLine();
        sb.AppendLine($"### {group.Key}");
        foreach (var d in group) sb.AppendLine($"- {d.Decision}");
    }
}
```

This ensures decisions are **always** part of `result.md` — regardless of whether
a `decisions.md` file was written. Non-repo pipelines (legal, MAD) get decisions
in their run results even without a target repository.

### 8. Bootstrap — Empty Template + LLM Decisions

**`MetaFileBootstrapper`** creates empty `decisions.md`:

```csharp
private const string DecisionsFileName = "decisions.md";

// In BootstrapAsync, after other files:
TryCreateDecisionsTemplate(agentDir);
```

```csharp
private void TryCreateDecisionsTemplate(string agentDir)
{
    var path = Path.Combine(agentDir, DecisionsFileName);
    if (File.Exists(path)) return;

    File.WriteAllText(path, "# Decision Log\n");
    logger.LogInformation("Created empty {File}", DecisionsFileName);
}
```

The LLM already generates context.yaml, code-map.yaml, coding-principles.md.
For bootstrap decisions, the existing generators (`ContextGenerator`,
`CodingPrinciplesGenerator`, `CodeMapGenerator`) do not currently expose their
reasoning. Adding decision output to bootstrap is **deferred to a follow-up** —
the empty template is created now, actual bootstrap decisions can be added when
the generators are extended to return reasoning alongside their output.

### 9. DI Registration

```csharp
// In AgentSmith.Infrastructure.Core/ServiceCollectionExtensions.cs
// Default: FileDecisionLogger for coding pipelines
services.AddSingleton<IDecisionLogger, FileDecisionLogger>();
// Alternative: InMemoryDecisionLogger for non-repo pipelines (legal, MAD)
// services.AddSingleton<IDecisionLogger, InMemoryDecisionLogger>();
```

`IDecisionLogger` is injected into:
- `GeneratePlanHandler` — writes plan decisions after parsing
- `AgenticExecuteHandler` — writes execution decisions after loop completes

Decisions also flow through `PipelineContext` (`ContextKeys.Decisions`) to:
- `WriteRunResultHandler` — reads decisions, passes to `RunResultFormatter`

---

## `.agentsmith/` Directory Update

```
.agentsmith/
  context.yaml
  code-map.yaml
  coding-principles.md
  decisions.md          <- NEW: why-documentation, agent-generated
  runs/
    r01-fix-null-check/
      plan.md
      result.md
```

`decisions.md` is **not** recreated per run — it is a growing document that
accumulates over the entire project lifetime. Decisions are never overwritten,
only appended.

---

## Files to Create

- `src/AgentSmith.Contracts/Decisions/IDecisionLogger.cs` — interface + enum
- `src/AgentSmith.Domain/Entities/PlanDecision.cs` — decision record
- `src/AgentSmith.Infrastructure.Core/Services/FileDecisionLogger.cs` — file-based implementation
- `src/AgentSmith.Infrastructure.Core/Services/InMemoryDecisionLogger.cs` — no-op implementation for non-repo pipelines
- `tests/AgentSmith.Tests/Services/FileDecisionLoggerTests.cs` — unit tests
- `tests/AgentSmith.Tests/Services/InMemoryDecisionLoggerTests.cs` — unit tests

## Files to Modify

- `src/AgentSmith.Domain/Entities/Plan.cs` — add Decisions property
- `src/AgentSmith.Contracts/Commands/ContextKeys.cs` — add `Decisions` key
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/PlanParser.cs` — parse decisions array
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/AgentPromptBuilder.cs` — prompt changes
- `src/AgentSmith.Infrastructure/Models/ToolDefinitions.cs` — add LogDecision tool
- `src/AgentSmith.Infrastructure/Models/OpenAiToolDefinitions.cs` — add LogDecision tool
- `src/AgentSmith.Infrastructure/Models/GeminiToolDefinitions.cs` — add LogDecision tool
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/ToolExecutor.cs` — add log_decision case + decisions list
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/ClaudeAgentProvider.cs` — pass decisions through
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/OpenAiAgentProvider.cs` — pass decisions through
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/GeminiAgentProvider.cs` — pass decisions through
- `src/AgentSmith.Contracts/Models/AgentExecutionResult.cs` — add Decisions property
- `src/AgentSmith.Application/Services/Handlers/GeneratePlanHandler.cs` — tell-don't-ask logging + pipeline storage
- `src/AgentSmith.Application/Services/Handlers/AgenticExecuteHandler.cs` — tell-don't-ask logging + pipeline storage
- `src/AgentSmith.Application/Services/Handlers/RunResultFormatter.cs` — `AppendDecisions` section grouped by category
- `src/AgentSmith.Application/Services/Handlers/WriteRunResultHandler.cs` — read decisions from pipeline, pass to formatter
- `src/AgentSmith.Application/Services/Handlers/MetaFileBootstrapper.cs` — create empty decisions.md
- `src/AgentSmith.Infrastructure.Core/ServiceCollectionExtensions.cs` — register FileDecisionLogger

## Tests to Create/Modify

- `FileDecisionLoggerTests` — writes to new file, appends to existing section, creates new section, thread safety, null/empty repoPath
- `InMemoryDecisionLoggerTests` — no-op completes without error
- `PlanParserTests` — new: parses decisions array, handles missing decisions gracefully
- `ToolExecutorTests` — new: log_decision tool stores decision and returns confirmation
- `GeneratePlanHandlerTests` — verify decisions via IDecisionLogger + pipeline storage, null repoPath
- `AgenticExecuteHandlerTests` — verify decisions via IDecisionLogger + pipeline storage
- `WriteRunResultHandlerTests` — verify decisions section in result.md, omitted when empty
- `BootstrapProjectHandlerTests` — verify decisions.md template is created

---

## Definition of Done

- [x] `IDecisionLogger` interface (nullable `repoPath`) + `DecisionCategory` enum in `AgentSmith.Contracts`
- [x] `PlanDecision` record in `AgentSmith.Domain`
- [x] `FileDecisionLogger` implementation with `SemaphoreSlim` thread safety + null repoPath handling
- [x] `InMemoryDecisionLogger` no-op implementation for non-repo pipelines
- [x] `ContextKeys.Decisions` for pipeline flow
- [x] `Plan` entity extended with `Decisions` property
- [x] `PlanParser` parses optional `decisions` array from JSON
- [x] `AgentPromptBuilder` — plan prompt requests decisions in JSON response
- [x] `AgentPromptBuilder` — execution prompt instructs to use log_decision tool
- [x] `log_decision` tool in `ToolDefinitions` (Anthropic)
- [x] `log_decision` tool in `OpenAiToolDefinitions`
- [x] `log_decision` tool in `GeminiToolDefinitions`
- [x] `ToolExecutor` handles `log_decision` — stores in list, returns confirmation
- [x] `AgentExecutionResult` extended with `Decisions` property
- [x] All three providers (Claude, OpenAI, Gemini) pass decisions through
- [x] `GeneratePlanHandler` — tell-don't-ask logging + pipeline storage
- [x] `AgenticExecuteHandler` — tell-don't-ask logging + pipeline storage
- [x] `RunResultFormatter.AppendDecisions` — grouped by category in `result.md`
- [x] `WriteRunResultHandler` reads decisions from pipeline, passes to formatter
- [x] `MetaFileBootstrapper` creates empty `decisions.md` template
- [x] DI registration in `Infrastructure.Core`
- [x] Unit tests for FileDecisionLogger (write, append, new section, thread safety, null repoPath)
- [x] Unit tests for InMemoryDecisionLogger
- [x] Unit tests for PlanParser (decisions parsing)
- [x] Unit tests for ToolExecutor (log_decision case)
- [x] Updated handler tests (GeneratePlan — null repoPath + pipeline storage, AgenticExecute — pipeline storage)
- [x] WriteRunResultHandler tests (decisions in result.md, omitted when empty)
- [x] RunResultFormatter tests (AppendDecisions grouping, null/empty)
- [x] All 466 tests green
