# Phase 4 - Pipeline Execution

## Goal
Make the system work end-to-end: User enters `"fix #123 in todo-list"` →
Intent is recognized → Config loaded → Pipeline built → Commands executed sequentially.

---

## Components

| Step | File | Description |
|------|------|-------------|
| 1 | `phase4-intent-parser.md` | IntentParser: Regex-based, User Input → TicketId + ProjectName |
| 2 | `phase4-pipeline-executor.md` | PipelineExecutor: Command names → Build Contexts → Execute Handlers |
| 3 | `phase4-use-case.md` | ProcessTicketUseCase: Orchestrates the entire flow |
| 4 | `phase4-di-wiring.md` | DI Registration in Infrastructure + Host Program.cs |
| 5 | Tests | IntentParser, PipelineExecutor, UseCase (with mocks) |

---

## Dependencies

- **Phase 1-3** must be complete (they are)
- IntentParser is intentionally kept simple (Regex instead of LLM call)
  - Rationale: Simplicity, no API costs, deterministic
  - Can be extended later to Claude-based parsing
- PipelineExecutor uses the existing CommandExecutor
- ProcessTicketUseCase is the central entry point for Host/CLI

---

## Design Decisions

### IntentParser: Regex instead of LLM
According to architecture.md, a Claude call is planned. For Phase 4 we implement
a Regex-based variant that recognizes the most common patterns:
- `"fix #123 in todo-list"` → TicketId(123), ProjectName(todo-list)
- `"#34237 todo-list"` → TicketId(34237), ProjectName(todo-list)
- `"todo-list #123"` → TicketId(123), ProjectName(todo-list)

An LLM-based parser can be registered as an alternative later.

### PipelineExecutor: Context-Building
The central challenge: Build the appropriate ICommandContext from a command name
(string from YAML config). This requires a mapping:
- `"FetchTicketCommand"` → `FetchTicketContext` with data from Config + PipelineContext
- Each command needs different inputs
- Solution: A `CommandContextFactory` that handles the mapping

### ProcessTicketUseCase
Orchestrates the entire flow:
1. Load config
2. Parse intent
3. Find project config
4. Find pipeline config
5. Start PipelineExecutor


---

# Phase 4 - Step 4: DI Wiring

## Goal
Register all Phase 4 components in DI.
Update Infrastructure ServiceCollectionExtensions + Host Program.cs.

---

## Infrastructure Registration

```
File: src/AgentSmith.Infrastructure/ServiceCollectionExtensions.cs
```

New extension method: `AddAgentSmithInfrastructure()`

Registers:
- `SecretsProvider` → Singleton
- `ITicketProviderFactory` → `TicketProviderFactory` → Singleton
- `ISourceProviderFactory` → `SourceProviderFactory` → Singleton
- `IAgentProviderFactory` → `AgentProviderFactory` → Singleton
- `IConfigurationLoader` → `YamlConfigurationLoader` → Singleton

---

## Application Registration Update

```
File: src/AgentSmith.Application/ServiceCollectionExtensions.cs
```

Extend `AddAgentSmithCommands()` with:
- `IIntentParser` → `RegexIntentParser` → Transient
- `ICommandContextFactory` → `CommandContextFactory` → Transient
- `IPipelineExecutor` → `PipelineExecutor` → Transient
- `ProcessTicketUseCase` → Transient

---

## Host Program.cs

```
File: src/AgentSmith.Cli/Program.cs
```

Minimal CLI without CommandLineParser (that comes in Phase 5):

```csharp
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddAgentSmithInfrastructure();
services.AddAgentSmithCommands();

var provider = services.BuildServiceProvider();

var configPath = args.Length > 1 ? args[1] : "config/agentsmith.yml";
var userInput = args.Length > 0 ? args[0] : throw new Exception("Usage: agentsmith <input> [config]");

var useCase = provider.GetRequiredService<ProcessTicketUseCase>();
var result = await useCase.ExecuteAsync(userInput, configPath);

Console.WriteLine(result.Success ? $"Success: {result.Message}" : $"Failed: {result.Message}");
return result.Success ? 0 : 1;
```

---

## Contract Change

`IPipelineExecutor` interface must be adjusted (ProjectConfig parameter):
```csharp
Task<CommandResult> ExecuteAsync(
    IReadOnlyList<string> commandNames,
    ProjectConfig projectConfig,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

Reflect this change in `architecture.md`!

---

## Tests

DI integration test:
- `ServiceRegistration_AllServicesResolvable` - Builds ServiceProvider, resolves all types


---

# Phase 4 - Step 1: IntentParser

## Goal
Convert user input like `"fix #123 in todo-list"` into a structured `ParsedIntent`.
Project: `AgentSmith.Application/Services/`

---

## RegexIntentParser

```
File: src/AgentSmith.Application/Services/RegexIntentParser.cs
```

Implements `IIntentParser` from Contracts.

**Supported Patterns:**
```
"fix #123 in todo-list"      → #123, todo-list
"#34237 todo-list"            → #34237, todo-list
"todo-list #123"              → #123, todo-list
"fix 123 in todo-list"        → 123, todo-list
"resolve ticket #42 in api" → #42, api
```

**Regex Strategy:**
1. Extract TicketId: `#?(\d+)` - Number with optional `#`
2. Extract ProjectName: Remove known noise words (`fix`, `resolve`, `in`, `ticket`, etc.)
   → remaining word = ProjectName

**Alternative Approach (simpler):**
- Regex 1: `#?(\d+)\s+(?:in\s+)?(\w+)` → Ticket first
- Regex 2: `(\w+)\s+#?(\d+)` → Project first
- Both are tried, first match wins

**Validation:**
- No match → `ConfigurationException("Could not parse intent from input: ...")`
- TicketId must be numeric
- ProjectName must be non-empty

**Constructor:**
- `ILogger<RegexIntentParser> logger`

---

## Extensibility

Later a `ClaudeIntentParser` can be registered as an alternative:
```csharp
// DI: Swap via config or feature flag
services.AddTransient<IIntentParser, RegexIntentParser>();
// or:
services.AddTransient<IIntentParser, ClaudeIntentParser>();
```

---

## Tests

**RegexIntentParserTests:**
- `ParseAsync_FixHashInProject_ReturnsCorrectIntent`
- `ParseAsync_ProjectFirst_ReturnsCorrectIntent`
- `ParseAsync_NoHash_ReturnsCorrectIntent`
- `ParseAsync_InvalidInput_ThrowsConfigurationException`
- `ParseAsync_OnlyNumber_ThrowsConfigurationException`


---

# Phase 4 - Step 2: PipelineExecutor + CommandContextFactory

## Goal
Build the appropriate Contexts from a list of command names (from YAML) and
execute them sequentially via CommandExecutor.
Project: `AgentSmith.Application/Services/`

---

## CommandContextFactory

```
File: src/AgentSmith.Application/Services/CommandContextFactory.cs
```

Builds the appropriate `ICommandContext` from a command name + Config + PipelineContext.

**Interface:**
```csharp
public interface ICommandContextFactory
{
    ICommandContext Create(string commandName, ProjectConfig project, PipelineContext pipeline);
}
```

**Mapping (switch expression):**
```
"FetchTicketCommand"           → FetchTicketContext(pipeline.Get<TicketId>("TicketId"), project.Tickets, pipeline)
"CheckoutSourceCommand"        → CheckoutSourceContext(project.Source, BranchName.FromTicket(ticketId), pipeline)
"LoadCodingPrinciplesCommand"  → LoadCodingPrinciplesContext(project.CodingPrinciplesPath, pipeline)
"AnalyzeCodeCommand"           → AnalyzeCodeContext(pipeline.Get<Repository>(...), pipeline)
"GeneratePlanCommand"          → GeneratePlanContext(ticket, analysis, principles, project.Agent, pipeline)
"ApprovalCommand"              → ApprovalContext(plan, pipeline)
"AgenticExecuteCommand"        → AgenticExecuteContext(plan, repo, principles, project.Agent, pipeline)
"TestCommand"                  → TestContext(repo, changes, pipeline)
"CommitAndPRCommand"           → CommitAndPRContext(repo, changes, ticket, project.Source, pipeline)
```

**Notes:**
- Early commands (FetchTicket, Checkout) pull data from Config
- Later commands (GeneratePlan, Agentic) pull data from PipelineContext (previous steps)
- TicketId is set in the PipelineContext before the pipeline starts
- Unknown command name → `ConfigurationException`

---

## PipelineExecutor

```
File: src/AgentSmith.Application/Services/PipelineExecutor.cs
```

Implements `IPipelineExecutor` from Contracts.

**Constructor:**
- `ICommandExecutor commandExecutor`
- `ICommandContextFactory contextFactory`
- `ProjectConfig projectConfig` (via Factory/DI or passed directly)
- `ILogger<PipelineExecutor> logger`

**Problem:** PipelineExecutor needs `ProjectConfig`, but it differs per invocation.
**Solution:** ProjectConfig is not injected via DI, but passed as a parameter.

Interface adjustment:
```csharp
public interface IPipelineExecutor
{
    Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        ProjectConfig projectConfig,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}
```

**ExecuteAsync:**
1. For each command name:
   a. `contextFactory.Create(name, projectConfig, pipeline)` → ICommandContext
   b. `commandExecutor.ExecuteAsync(context, ct)` → CommandResult
   c. Log: Command name + Success/Fail
   d. On Fail → immediately return with Fail result
2. All successful → `CommandResult.Ok("Pipeline completed")`

**Challenge:** `ExecuteAsync<TContext>` is generic, but the compile-time type
is `ICommandContext`. Solution: Reflection or Dictionary with Delegates.

Pragmatic approach: `ExecuteCommandAsync(ICommandContext context)` method
that uses pattern matching to make the correct generic call:
```csharp
private Task<CommandResult> ExecuteCommandAsync(ICommandContext context, CancellationToken ct)
{
    return context switch
    {
        FetchTicketContext c => commandExecutor.ExecuteAsync(c, ct),
        CheckoutSourceContext c => commandExecutor.ExecuteAsync(c, ct),
        // ... all 9 commands
        _ => throw new ConfigurationException($"Unknown context type: {context.GetType().Name}")
    };
}
```

---

## Tests

**CommandContextFactoryTests:**
- `Create_FetchTicketCommand_ReturnsFetchTicketContext`
- `Create_UnknownCommand_ThrowsConfigurationException`
- `Create_GeneratePlanCommand_PullsFromPipeline`

**PipelineExecutorTests:**
- `ExecuteAsync_AllCommandsSucceed_ReturnsOk`
- `ExecuteAsync_SecondCommandFails_StopsAndReturnsFail`
- `ExecuteAsync_EmptyPipeline_ReturnsOk`


---

# Phase 4 - Step 3: ProcessTicketUseCase

## Goal
Central entry point: User Input → Config → Intent → Pipeline → Result.
Project: `AgentSmith.Application/UseCases/`

---

## ProcessTicketUseCase

```
File: src/AgentSmith.Application/UseCases/ProcessTicketUseCase.cs
```

**Constructor:**
- `IConfigurationLoader configLoader`
- `IIntentParser intentParser`
- `IPipelineExecutor pipelineExecutor`
- `ILogger<ProcessTicketUseCase> logger`

**ExecuteAsync(string userInput, string configPath, CancellationToken):**

1. Load config: `configLoader.LoadAsync(configPath, ct)`
2. Parse intent: `intentParser.ParseAsync(userInput, ct)`
3. Find project: `config.Projects[intent.ProjectName.Value]`
   - Not found → `ConfigurationException("Project '{name}' not found")`
4. Find pipeline: `config.Pipelines[projectConfig.Pipeline]`
   - Not found → `ConfigurationException("Pipeline '{name}' not found")`
5. Create PipelineContext, set TicketId
6. Execute pipeline: `pipelineExecutor.ExecuteAsync(commands, projectConfig, pipeline, ct)`
7. Log result and return

**Return:** `CommandResult` (Ok with PR URL or Fail with error details)

---

## Notes

- UseCase is thin, only orchestrates
- No business logic, just wiring
- Error handling: Exceptions from providers are propagated to the caller
- Log level: Information for start/end, Warning for errors

---

## Tests

**ProcessTicketUseCaseTests:**
- `ExecuteAsync_ValidInput_RunsPipeline` (all dependencies mocked)
- `ExecuteAsync_UnknownProject_ThrowsConfigurationException`
- `ExecuteAsync_UnknownPipeline_ThrowsConfigurationException`
