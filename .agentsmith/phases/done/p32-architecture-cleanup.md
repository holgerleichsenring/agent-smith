# Phase 32: Architecture Cleanup — ILlmClient, Generator Simplification, Command Constants

## Context

The three bootstrap generators (ContextGenerator, CodeMapGenerator, CodingPrinciplesGenerator) have
accumulated structural debt:
- **Direct Anthropic SDK coupling** — each creates its own `AnthropicClient` + `ResilientHttpClientFactory`,
  making it impossible to switch LLM providers without rewriting all three.
- **Language-specific code in CodeMapGenerator** — 300+ lines of `CollectDotNetInput`, `CollectTypeScriptInput`,
  `CollectPythonInput` that duplicate what the LLM should infer from raw repo data.
- **Duplicated utilities** — `GenerateTree`, `BuildTreeLines`, `ExcludedDirs`, `StripCodeFences` are
  copy-pasted across generators.
- **Magic strings** — command names like `"FetchTicketCommand"` appear in YAML config, `CommandContextFactory`,
  `PipelineExecutor.StepLabels`, and tests without a single source of truth.
- **Pipeline YAML illusion** — pipelines are defined in YAML as if configurable, but nobody reconfigures them.
  They should be code-defined presets with optional YAML override.

**Goal**: Clean architecture where generators are thin prompt-builders backed by an `ILlmClient` abstraction,
command names are constants, and pipelines are code-defined.

---

## Step 1: ILlmClient abstraction + shared utilities

### 1a: Create `ILlmClient` interface

**NEW: [ILlmClient.cs](src/AgentSmith.Contracts/Services/ILlmClient.cs)**
```csharp
public interface ILlmClient
{
    Task<string> CompleteAsync(
        string systemPrompt,
        string userPrompt,
        TaskType taskType,
        CancellationToken cancellationToken);
}
```

### 1b: Create `AnthropicLlmClient` implementation

**NEW: [AnthropicLlmClient.cs](src/AgentSmith.Infrastructure/Services/AnthropicLlmClient.cs)**
- Takes `string apiKey, RetryConfig, IModelRegistry, ILogger`
- Creates one `HttpClient` via `ResilientHttpClientFactory` in constructor (reused across calls)
- `CompleteAsync`: resolves `ModelAssignment` from `IModelRegistry.GetModel(taskType)`,
  creates `AnthropicClient(apiKey, httpClient)`, calls `GetClaudeMessageAsync`,
  returns `TextContent.Text.Trim()`

### 1c: Create `LlmResponseHelper` static utility

**NEW: [LlmResponseHelper.cs](src/AgentSmith.Infrastructure/Services/LlmResponseHelper.cs)**
- Move `StripCodeFences(string)` here (currently duplicated in ContextGenerator + CodeMapGenerator)
- Move `IsValidYaml(string)` here (currently in CodeMapGenerator, uses YamlDotNet)

### 1d: Add `DirectoryTree` to `RepoSnapshot`

**[RepoSnapshot.cs](src/AgentSmith.Contracts/Models/RepoSnapshot.cs)**
- Add `string DirectoryTree` property to the record

### 1e: Enhance `RepoSnapshotCollector`

**[RepoSnapshotCollector.cs](src/AgentSmith.Infrastructure/Services/RepoSnapshotCollector.cs)**
- Add tree generation (move `GenerateTree` + `BuildTreeLines` from ContextGenerator/CodeMapGenerator)
- Centralize `ExcludedDirs` as the single source (remove duplicates from generators)
- `Collect()` returns `RepoSnapshot` including the directory tree
- Make `MaxTreeDepth` = 4, `MaxTreeLines` = 200

### 1f: Register `ILlmClient` in DI

**[ServiceCollectionExtensions.cs](src/AgentSmith.Infrastructure/Extensions/ServiceCollectionExtensions.cs)**
- Register `ILlmClient` → `AnthropicLlmClient` as singleton
- Remove the 3 individual generator factory registrations (they'll be simple `AddSingleton<IFoo, Foo>`)

### 1g: Tests

- Move `StripCodeFences` tests from `ContextGeneratorTests` + `CodeMapGeneratorTests` → new `LlmResponseHelperTests`
- Move `IsValidYaml` tests → `LlmResponseHelperTests`
- Move `GenerateTree` tests → `RepoSnapshotCollectorTests`
- Add `AnthropicLlmClient` unit test (mock HttpClient, verify model routing)

**BUILD + TEST checkpoint**

---

## Step 2: Simplify all 3 generators

### 2a: Simplify `CodingPrinciplesGenerator` (simplest — do first)

**[CodingPrinciplesGenerator.cs](src/AgentSmith.Infrastructure/Services/CodingPrinciplesGenerator.cs)**
- Change constructor: `(ILlmClient llmClient, ILogger)` — remove `apiKey`, `RetryConfig`, `ModelAssignment`
- `GenerateAsync`: call `llmClient.CompleteAsync(SystemPrompt, userPrompt, TaskType.ContextGeneration, ct)`
- Remove `CreateClient()` method
- Keep `BuildUserPrompt` (already clean, no language-specific code)
- ~120 → ~50 lines

### 2b: Simplify `CodeMapGenerator` (biggest change)

**[ICodeMapGenerator.cs](src/AgentSmith.Contracts/Services/ICodeMapGenerator.cs)**
- Change signature: add `RepoSnapshot snapshot` parameter (align with other generators)

**[CodeMapGenerator.cs](src/AgentSmith.Infrastructure/Services/CodeMapGenerator.cs)**
- Change constructor: `(ILlmClient llmClient, ILogger)` — remove `apiKey`, `RetryConfig`, `ModelAssignment`
- **DELETE** `CollectArchitectureInput`, `CollectDotNetInput`, `CollectTypeScriptInput`,
  `CollectPythonInput`, `CollectGenericInput` (~250 lines)
- **DELETE** `GenerateTree`, `BuildTreeLines`, `ExcludedDirs`, `TryReadFileTruncated`,
  `IsExcludedPath`, `IsInterfaceDirectory`, `SafeGetFiles` (~100 lines)
- **DELETE** `CreateClient`, `ExtractYaml`, `StripCodeFences` (use `LlmResponseHelper`)
- `GenerateAsync`: build prompt from `DetectedProject` + `snapshot.DirectoryTree` +
  `snapshot.CodeSamples` + `snapshot.ConfigFileContents`, call `llmClient.CompleteAsync`,
  validate YAML via `LlmResponseHelper.IsValidYaml`
- Update `BuildUserPrompt` to use snapshot data instead of language-specific input
- ~460 → ~80 lines

**[BootstrapProjectHandler.cs](src/AgentSmith.Application/Services/Handlers/BootstrapProjectHandler.cs)**
- `TryGenerateCodeMapAsync`: pass `snapshot` (already available from earlier in `ExecuteAsync`)
  to `codeMapGenerator.GenerateAsync(detected, repoPath, snapshot, ct)`

### 2c: Simplify `ContextGenerator`

**[ContextGenerator.cs](src/AgentSmith.Infrastructure/Services/ContextGenerator.cs)**
- Change constructor: `(ILlmClient llmClient, ILogger)` — remove `apiKey`, `RetryConfig`, `ModelAssignment`
- **DELETE** `GenerateTree`, `BuildTreeLines`, `ExcludedDirs` (use `snapshot.DirectoryTree`)
- **DELETE** `CreateClient`, `ExtractYaml`, `StripCodeFences` (use `LlmResponseHelper`)
- `GenerateAsync`: use `snapshot.DirectoryTree` instead of generating tree
- `RetryWithErrorsAsync`: call `llmClient.CompleteAsync` with retry prompt
- Update `BuildUserPrompt` signature: remove `directoryTree` param, get it from snapshot
- Keep `ReadKeyFiles`, `BuildSnapshotSection` (still useful, not duplicated)
- Keep prompt templates (SystemPrompt, QualityTemplateBasic/Extended)
- ~340 → ~150 lines

### 2d: Update DI registrations

**[ServiceCollectionExtensions.cs](src/AgentSmith.Infrastructure/Extensions/ServiceCollectionExtensions.cs)**
- Generators become simple: `services.AddSingleton<IContextGenerator, ContextGenerator>()`
- No more factory lambdas with apiKey/retryConfig/modelAssignment

### 2e: Update tests

**[CodeMapGeneratorTests.cs](tests/AgentSmith.Tests/Services/CodeMapGeneratorTests.cs)**
- **DELETE** `CollectDotNetInput_*`, `CollectTypeScriptInput_*`, `CollectPythonInput_*`,
  `CollectArchitectureInput_*` tests (~7 tests removed)
- **DELETE** `GenerateTree_*`, `TryReadFileTruncated_*`, `StripCodeFences_*` tests (moved to Step 1g)
- **KEEP** `BuildUserPrompt_*` tests (update for new signature using snapshot)
- **ADD** test: `GenerateAsync_CallsLlmClientWithCorrectTaskType` (mock ILlmClient)

**[ContextGeneratorTests.cs](tests/AgentSmith.Tests/Services/ContextGeneratorTests.cs)**
- **DELETE** `GenerateTree_*`, `StripCodeFences_*` tests (moved to Step 1g)
- **UPDATE** `BuildUserPrompt_*` tests for new signature (snapshot instead of separate directoryTree)
- **KEEP** `ReadKeyFiles_*`, `BuildSnapshotSection_*` tests

**BUILD + TEST checkpoint**

---

## Step 3: CommandNames constants + Pipeline presets

### 3a: Create `CommandNames` static class

**NEW: [CommandNames.cs](src/AgentSmith.Contracts/Commands/CommandNames.cs)**
```csharp
public static class CommandNames
{
    public const string FetchTicket = "FetchTicketCommand";
    public const string CheckoutSource = "CheckoutSourceCommand";
    public const string BootstrapProject = "BootstrapProjectCommand";
    public const string LoadCodeMap = "LoadCodeMapCommand";
    public const string LoadCodingPrinciples = "LoadCodingPrinciplesCommand";
    public const string LoadContext = "LoadContextCommand";
    public const string AnalyzeCode = "AnalyzeCodeCommand";
    public const string GeneratePlan = "GeneratePlanCommand";
    public const string Approval = "ApprovalCommand";
    public const string AgenticExecute = "AgenticExecuteCommand";
    public const string Test = "TestCommand";
    public const string WriteRunResult = "WriteRunResultCommand";
    public const string CommitAndPR = "CommitAndPRCommand";
    public const string InitCommit = "InitCommitCommand";
    public const string GenerateTests = "GenerateTestsCommand";
    public const string GenerateDocs = "GenerateDocsCommand";

    public static string GetLabel(string commandName) =>
        Labels.GetValueOrDefault(commandName, commandName);

    private static readonly Dictionary<string, string> Labels = ...;
}
```

### 3b: Create `PipelinePresets` static class

**NEW: [PipelinePresets.cs](src/AgentSmith.Contracts/Commands/PipelinePresets.cs)**
- Four presets: `FixBug`, `FixNoTest`, `InitProject`, `AddFeature`
- `TryResolve(string name)` → `IReadOnlyList<string>?`

### 3c: Update consumers

- `CommandContextFactory`: string literals → `CommandNames.*`
- `PipelineExecutor`: remove `StepLabels` → use `CommandNames.GetLabel()`
- `ProcessTicketUseCase`: resolve from `PipelinePresets` first, then YAML fallback

### 3d: Simplify YAML config

- Remove `pipelines` section from `agentsmith.yml`
- Keep `pipeline: fix-bug` in project config (selects which preset)

### 3e: Tests

- `CommandNamesTests`, `PipelinePresetsTests`
- Update `PipelineExecutorTests`

**BUILD + TEST checkpoint**

---

---

## Step 4: ILlmClientFactory — per-project LLM client creation

### Context

Steps 1-2 introduced `ILlmClient` as a global singleton. This is wrong: each project has its own
`AgentConfig` with `type` (Claude/OpenAI/Gemini), `retry` policy, and `models` mapping.
A global singleton ignores all of this — not deterministic across projects.

The `IAgentProviderFactory` already solves this correctly for the agentic loop.
Mirror the same pattern for `ILlmClient`.

### 4a: Create `ILlmClientFactory` interface

**NEW: [ILlmClientFactory.cs](src/AgentSmith.Contracts/Services/ILlmClientFactory.cs)**
```csharp
public interface ILlmClientFactory
{
    ILlmClient Create(AgentConfig config);
}
```

### 4b: Create `LlmClientFactory` implementation

**NEW: [LlmClientFactory.cs](src/AgentSmith.Infrastructure/Services/Factories/LlmClientFactory.cs)**
- Takes `SecretsProvider, ILoggerFactory` (mirrors `AgentProviderFactory`)
- `Create(AgentConfig config)`: resolves API key by `config.Type`, creates `IModelRegistry`
  from `config.Models`, returns `AnthropicLlmClient(apiKey, config.Retry, registry, logger)`
- Other providers throw `NotSupportedException` until `OpenAiLlmClient` / `GeminiLlmClient` exist

### 4c: Remove global singleton from DI

**EDIT: [ServiceCollectionExtensions.cs](src/AgentSmith.Infrastructure/Extensions/ServiceCollectionExtensions.cs)**
- Remove singleton `ILlmClient` and `IModelRegistry` registrations
- Add: `services.AddSingleton<ILlmClientFactory, LlmClientFactory>();`

### 4d: Pass `AgentConfig` to `BootstrapProjectContext`

**EDIT: [BootstrapProjectContext.cs](src/AgentSmith.Application/Models/BootstrapProjectContext.cs)**
- Add `AgentConfig Agent` parameter to record

**EDIT: [CommandContextFactory.cs](src/AgentSmith.Application/Services/CommandContextFactory.cs)**
- `CreateBootstrapProject` now takes `ProjectConfig project` and passes `project.Agent`

### 4e: Generators accept `ILlmClient` as method parameter (not constructor)

**EDIT: [IContextGenerator.cs](src/AgentSmith.Contracts/Services/IContextGenerator.cs)**,
[ICodeMapGenerator.cs](src/AgentSmith.Contracts/Services/ICodeMapGenerator.cs),
[ICodingPrinciplesGenerator.cs](src/AgentSmith.Contracts/Services/ICodingPrinciplesGenerator.cs)
- Add `ILlmClient llmClient` parameter to `GenerateAsync` (and `RetryWithErrorsAsync`)

**EDIT: Generators** — remove `ILlmClient` from constructor, accept as parameter instead.
Generators become truly stateless (only `ILogger` in constructor).

### 4f: `BootstrapProjectHandler` creates per-project client

**EDIT: [BootstrapProjectHandler.cs](src/AgentSmith.Application/Services/Handlers/BootstrapProjectHandler.cs)**
- Inject `ILlmClientFactory` in constructor
- `ExecuteAsync`: `var llmClient = llmClientFactory.Create(context.Agent);`
- Pass `llmClient` to all generator calls

### 4g: Dispatcher uses default `ILlmClient` for intent parsing

**EDIT: [ServiceCollectionExtensions.cs (Dispatcher)](src/AgentSmith.Dispatcher/Extensions/ServiceCollectionExtensions.cs)**
- `AddIntentEngine`: create default `ILlmClient` via `ILlmClientFactory.Create(new AgentConfig { Type = "claude" })`
- Intent parsing is a global operation (pre-project), so defaults are correct here

### 4h: Rename `IHaikuIntentParser` → `ILlmIntentParser`

**NEW: [ILlmIntentParser.cs](src/AgentSmith.Dispatcher/Contracts/ILlmIntentParser.cs)** — provider-agnostic name
**NEW: [LlmIntentParser.cs](src/AgentSmith.Dispatcher/Services/LlmIntentParser.cs)** — uses `ILlmClient` with `TaskType.Scout`
**DELETE**: `IHaikuIntentParser.cs`, `HaikuIntentParser.cs` (direct Anthropic SDK coupling removed)
**EDIT: [IntentEngine.cs](src/AgentSmith.Dispatcher/Services/IntentEngine.cs)** — `IHaikuIntentParser` → `ILlmIntentParser`

### 4i: Blocked command patterns in ToolExecutor

**EDIT: [ToolExecutor.cs](src/AgentSmith.Infrastructure/Services/Providers/Agent/ToolExecutor.cs)**
- Add hard blocklist for long-running server commands (`dotnet run`, `npm start`, `docker run`, etc.)
- `IsBlockedCommand()` checks before execution, returns immediate error instead of 60s timeout
- 21 blocked patterns, multi-command and multi-line detection

### 4j: Tests

- `BootstrapProjectHandlerTests` — add `ILlmClientFactory` mock, `AgentConfig` in context
- Generator tests — `ILlmClient` passed as parameter, not constructor
- `ToolExecutorTests` — 37 new tests for blocked command patterns
- 339 tests total, all passing

**BUILD + TEST checkpoint**

---

## Unchanged Components

- **ProjectDetector** — stays as-is (deterministic, fast, no LLM tokens)
- **RepoSnapshotCollector language hint** — keeps `GetSourceExtensions(language)` for efficient file selection
- **IAgentProvider** — separate concern (agentic loop), not affected

## File Summary

| Action | Count | Files |
|--------|-------|-------|
| New | 8 | ILlmClient.cs, AnthropicLlmClient.cs, LlmResponseHelper.cs, CommandNames.cs, PipelinePresets.cs, ILlmClientFactory.cs, LlmClientFactory.cs, ILlmIntentParser.cs + LlmIntentParser.cs |
| New tests | 4 | LlmResponseHelperTests.cs, CommandNamesTests.cs, PipelinePresetsTests.cs, LlmClientFactoryTests.cs |
| Delete | 2 | IHaikuIntentParser.cs, HaikuIntentParser.cs |
| Major rewrite | 3 | CodeMapGenerator.cs (~460→80), ContextGenerator.cs (~340→150), CodingPrinciplesGenerator.cs (~120→50) |
| Modify | 12 | RepoSnapshot.cs, RepoSnapshotCollector.cs, ICodeMapGenerator.cs, IContextGenerator.cs, ICodingPrinciplesGenerator.cs, BootstrapProjectHandler.cs, BootstrapProjectContext.cs, ServiceCollectionExtensions.cs (×2), CommandContextFactory.cs, PipelineExecutor.cs, ProcessTicketUseCase.cs, ToolExecutor.cs, IntentEngine.cs |
| Modify tests | 3 | CodeMapGeneratorTests.cs, ContextGeneratorTests.cs, BootstrapProjectHandlerTests.cs, ToolExecutorTests.cs |
| Simplify config | 1 | agentsmith.yml (remove pipelines section) |
