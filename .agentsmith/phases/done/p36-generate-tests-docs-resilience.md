# Phase 36: GenerateTests & GenerateDocs Commands + Pipeline Resilience

## Context

The `add-feature` pipeline includes `GenerateTestsCommand` and `GenerateDocsCommand` but neither
has any implementation — no context model, no handler, no factory case. When the pipeline reaches
these commands, `CommandContextFactory` throws `ConfigurationException("Unknown command")`, which
crashes the container. Because the host process has no top-level exception handling and the
`PipelineExecutor` doesn't catch per-command exceptions, the crash occurs silently — no error is
sent to Redis, so the Slack UI stays stuck indefinitely.

## Goals

1. **Implement GenerateTests**: LLM-based test generation for code changes, runs before `TestCommand`
2. **Implement GenerateDocs**: LLM-based documentation generation, runs after `TestCommand`
3. **Pipeline resilience**: No single command exception should crash the entire process

## Changes

### 1. GenerateTestsContext + GenerateTestsHandler

**Context** (`Application/Models/GenerateTestsContext.cs`):
```
sealed record GenerateTestsContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    string? CodeMap = null,
    string? ProjectContext = null) : ICommandContext;
```

**Handler** (`Application/Services/Handlers/GenerateTestsHandler.cs`):
- Creates a synthetic `Plan` with a single step: generate unit tests for changed files
- Calls `IAgentProvider.ExecutePlanAsync()` — reuses the full agentic loop (read files, write tests, iterate)
- Merges resulting `CodeChange` list with existing `ContextKeys.CodeChanges`
- The prompt instructs the LLM to:
  - Analyze the changed files and existing test patterns in the repo
  - Generate tests following the detected test framework and naming conventions
  - Only generate tests for new/modified public API — not internals

### 2. GenerateDocsContext + GenerateDocsHandler

**Context** (`Application/Models/GenerateDocsContext.cs`):
```
sealed record GenerateDocsContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline,
    string? CodeMap = null,
    string? ProjectContext = null) : ICommandContext;
```

**Handler** (`Application/Services/Handlers/GenerateDocsHandler.cs`):
- Creates a synthetic `Plan` with a single step: update/generate documentation for changes
- Calls `IAgentProvider.ExecutePlanAsync()` — same agentic loop
- Merges resulting `CodeChange` list with existing `ContextKeys.CodeChanges`
- The prompt instructs the LLM to:
  - Update README if new features/endpoints were added
  - Add/update inline XML docs for new public types
  - Update API docs or changelog if they exist in the repo

### 3. CommandContextFactory — two new cases

```csharp
CommandNames.GenerateTests => CreateGenerateTests(project, pipeline),
CommandNames.GenerateDocs  => CreateGenerateDocs(project, pipeline),
```

Both pull: `Repository`, `CodeChanges`, `CodingPrinciples`, `AgentConfig`, `CodeMap?`, `ProjectContext?`

### 4. PipelineExecutor — two new context dispatch cases

```csharp
GenerateTestsContext c => commandExecutor.ExecuteAsync(c, ct),
GenerateDocsContext c  => commandExecutor.ExecuteAsync(c, ct),
```

### 5. ServiceCollectionExtensions — handler registration

```csharp
services.AddTransient<ICommandHandler<GenerateTestsContext>, GenerateTestsHandler>();
services.AddTransient<ICommandHandler<GenerateDocsContext>, GenerateDocsHandler>();
```

### 6. Pipeline Resilience (already implemented)

**PipelineExecutor** — per-command try/catch around `contextFactory.Create()` + `ExecuteCommandAsync()`.
Any exception becomes `CommandResult.Fail(...)` instead of crashing the process. `OperationCanceledException`
is re-thrown to preserve cancellation semantics.

**Program.cs** — top-level try/catch around `useCase.ExecuteAsync()`. Ensures `ReportErrorAsync` is
always called in K8s job mode, even on unhandled exceptions.

**OrphanJobDetector** — replaced time-based orphan detection with container liveness checking:
- Added `IJobSpawner.IsAliveAsync(jobId)` to check whether the Docker container / K8s pod is still running
- `DockerJobSpawner.IsAliveAsync`: inspects container by name, returns `State.Running`; returns `false` if `DockerContainerNotFoundException` (AutoRemove cleaned it up)
- `KubernetesJobSpawner.IsAliveAsync`: reads K8s job status, returns `Status.Active > 0`; returns `false` on 404
- `OrphanJobDetector.IsOrphanedAsync` logic: (1) skip if runtime < 1 min (startup grace), (2) orphaned if runtime > 120 min (absolute limit), (3) check `IsAliveAsync` — alive = not orphaned, dead = orphaned, error = skip (fail-safe)
- This eliminates false positives from slow operations (e.g., `dotnet build` taking >30s without Redis messages)

## Files Modified

1. `src/AgentSmith.Application/Models/GenerateTestsContext.cs` (new)
2. `src/AgentSmith.Application/Models/GenerateDocsContext.cs` (new)
3. `src/AgentSmith.Application/Services/Handlers/GenerateTestsHandler.cs` (new)
4. `src/AgentSmith.Application/Services/Handlers/GenerateDocsHandler.cs` (new)
5. `src/AgentSmith.Application/Services/CommandContextFactory.cs` (two new cases)
6. `src/AgentSmith.Application/Services/PipelineExecutor.cs` (two new dispatch cases + exception handling)
7. `src/AgentSmith.Application/Extensions/ServiceCollectionExtensions.cs` (two handler registrations)
8. `src/AgentSmith.Host/Program.cs` (top-level try/catch)
9. `src/AgentSmith.Dispatcher/Contracts/IJobSpawner.cs` (added IsAliveAsync)
10. `src/AgentSmith.Dispatcher/Services/DockerJobSpawner.cs` (IsAliveAsync implementation)
11. `src/AgentSmith.Dispatcher/Services/KubernetesJobSpawner.cs` (IsAliveAsync implementation)
12. `src/AgentSmith.Dispatcher/Services/OrphanJobDetector.cs` (liveness-based orphan detection)
13. `tests/AgentSmith.Tests/Commands/GenerateTestsHandlerTests.cs` (new, 4 tests)
14. `tests/AgentSmith.Tests/Commands/GenerateDocsHandlerTests.cs` (new, 4 tests)
15. `tests/AgentSmith.Tests/Commands/PipelineExecutorTests.cs` (2 new exception handling tests)
16. `tests/AgentSmith.Tests/Dispatcher/OrphanJobDetectorTests.cs` (4 new liveness tests)

## Pipeline Flow (add-feature)

```
... → AgenticExecute → GenerateTests → Test → GenerateDocs → WriteRunResult → CommitAndPR
         ↓                  ↓            ↓          ↓
    writes code      generates tests  runs all   generates docs
    CodeChanges[0]   CodeChanges[+]   validates  CodeChanges[+]
```

## Test Plan

- GenerateTestsHandler: mock IAgentProviderFactory, verify synthetic plan, verify CodeChanges merge
- GenerateDocsHandler: same pattern
- PipelineExecutor: verify exception from factory/handler yields Fail result (no crash)
- PipelinePresets: verify AddFeature still contains GenerateTests + GenerateDocs
