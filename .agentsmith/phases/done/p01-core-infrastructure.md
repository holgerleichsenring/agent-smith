# Phase 1: Core Infrastructure - Implementation Plan

## Goal
Foundation of the project: Solution Structure, Domain Entities, all Contracts (interfaces only), Config Loader.
After Phase 1 the solution compiles but has no functional logic.

---

## Prerequisite
- .NET 8 SDK installed
- This repo cloned

## Steps

### Step 1: Solution & Projects Setup
See: `prompts/phase1-solution-structure.md`

Create the .NET Solution with all projects and correct references.
Result: `dotnet build` succeeds (empty but error-free).

### Step 2: Domain Entities & Value Objects
See: `prompts/phase1-domain.md`

The core types of the system. Pure data models without infrastructure dependencies.
- Entities: `Ticket`, `Repository`, `Plan`, `CodeChange`, `CodeAnalysis`
- Value Objects: `TicketId`, `ProjectName`, `BranchName`, `FilePath`, `CommandResult`
- Exceptions: `AgentSmithException`, `TicketNotFoundException`, `ConfigurationException`

### Step 3: Contracts (Interfaces)
See: `prompts/phase1-contracts.md`

All interfaces that define the system. No implementation.
- Command Pattern (MediatR-Style): `ICommandContext`, `ICommandHandler<TContext>`, `ICommandExecutor`
- Shared State: `PipelineContext`, `ContextKeys`
- Providers: `ITicketProvider`, `ISourceProvider`, `IAgentProvider`
- Services: `IPipelineExecutor`, `IIntentParser`, `IConfigurationLoader`
- Factories: `ITicketProviderFactory`, `ISourceProviderFactory`, `IAgentProviderFactory`

### Step 4: Configuration
See: `prompts/phase1-config.md`

Load YAML-based configuration and provide it as strongly typed objects.
- Config Models: `AgentSmithConfig`, `ProjectConfig`, `SourceConfig`, `TicketConfig`, `AgentConfig`, `PipelineConfig`
- `YamlConfigurationLoader` implementation (only implementation in Phase 1)
- Template `agentsmith.yml` as example

### Step 5: Verify
```bash
dotnet build
dotnet test  # Empty test suite, but must pass
```

---

## Dependencies Between Steps

```
Step 1 (Solution)
    └── Step 2 (Domain)
         └── Step 3 (Contracts) ← needs Domain types
              └── Step 4 (Config) ← needs Contracts
                   └── Step 5 (Verify)
```

Strictly sequential. Each step builds on the previous one.

---

## Project References After Phase 1

```
AgentSmith.Domain          → (no dependencies)
AgentSmith.Contracts       → AgentSmith.Domain
AgentSmith.Application     → AgentSmith.Contracts, AgentSmith.Domain
AgentSmith.Infrastructure  → AgentSmith.Contracts, AgentSmith.Domain
AgentSmith.Cli            → AgentSmith.Application, AgentSmith.Infrastructure
AgentSmith.Tests           → AgentSmith.Domain, AgentSmith.Contracts, AgentSmith.Infrastructure
```

---

## NuGet Packages (Phase 1)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Infrastructure | YamlDotNet | Load YAML config |
| AgentSmith.Cli | Microsoft.Extensions.DependencyInjection | DI Container |
| AgentSmith.Cli | Microsoft.Extensions.Logging.Console | Logging |
| AgentSmith.Tests | xunit | Test Framework |
| AgentSmith.Tests | xunit.runner.visualstudio | Test Runner |
| AgentSmith.Tests | Microsoft.NET.Test.Sdk | Test Infrastructure |
| AgentSmith.Tests | Moq | Mocking |
| AgentSmith.Tests | FluentAssertions | Readable Assertions |

---

## Definition of Done (Phase 1)
- [ ] Solution compiles without errors
- [ ] All Domain Entities with properties defined
- [ ] All Value Objects defined as records
- [ ] All Interfaces defined in Contracts
- [ ] Config Loader reads YAML and returns typed config
- [ ] Example `agentsmith.yml` present
- [ ] `coding-principles.md` present
- [ ] At least 1 unit test (Config Loader)
- [ ] All files adhere to Coding Principles (20/120 rule)


---

# Phase 1 - Step 4: Configuration

## Goal
Load YAML-based configuration and provide it as strongly typed objects.
Only real implementation in Phase 1.

---

## Config Models

All in `AgentSmith.Contracts/Configuration/` (needed by multiple layers).

### AgentSmithConfig
```
File: src/AgentSmith.Contracts/Configuration/AgentSmithConfig.cs
```
- `Dictionary<string, ProjectConfig> Projects`
- `Dictionary<string, PipelineConfig> Pipelines`
- `Dictionary<string, string> Secrets`

### ProjectConfig
```
File: src/AgentSmith.Contracts/Configuration/ProjectConfig.cs
```
- `SourceConfig Source`
- `TicketConfig Tickets`
- `AgentConfig Agent`
- `string Pipeline` (name of the pipeline definition)
- `string? CodingPrinciplesPath`

### SourceConfig
```
File: src/AgentSmith.Contracts/Configuration/SourceConfig.cs
```
- `string Type` (GitHub, GitLab, AzureRepos, Local)
- `string? Url`
- `string? Path` (for Local)
- `string Auth` (token, ssh)

### TicketConfig
```
File: src/AgentSmith.Contracts/Configuration/TicketConfig.cs
```
- `string Type` (AzureDevOps, Jira, GitHub)
- `string? Organization`
- `string? Project`
- `string? Url`
- `string Auth` (token)

### AgentConfig
```
File: src/AgentSmith.Contracts/Configuration/AgentConfig.cs
```
- `string Type` (Claude, OpenAI)
- `string Model` (e.g. sonnet-4, gpt-4o)

### PipelineConfig
```
File: src/AgentSmith.Contracts/Configuration/PipelineConfig.cs
```
- `List<string> Commands` (command class names)

---

## Implementation: YamlConfigurationLoader

```
File: src/AgentSmith.Infrastructure/Configuration/YamlConfigurationLoader.cs
```
Project: `AgentSmith.Infrastructure`

**Responsibility:**
- Implements `IConfigurationLoader`
- Reads YAML file from file path
- Deserializes to `AgentSmithConfig`
- Resolves `${ENV_VAR}` placeholders in Secrets
- Throws `ConfigurationException` on errors

**Methods:**
- `AgentSmithConfig LoadConfig(string configPath)` (from interface)
- Private: `string ResolveEnvironmentVariables(string value)` - replaces `${VAR}` with `Environment.GetEnvironmentVariable`

**Behavior:**
- File not found → `ConfigurationException`
- Invalid YAML → `ConfigurationException`
- Environment variable not set → value remains empty (no error, validated only at usage time)

---

## Example Config

```
File: config/agentsmith.yml
```

```yaml
projects:
  todo-list:
    source:
      type: GitHub
      url: https://github.com/user/todo-list
      auth: token
    tickets:
      type: AzureDevOps
      organization: myorg
      project: Todo-listProject
      auth: token
    agent:
      type: Claude
      model: sonnet-4
    pipeline: fix-bug
    coding_principles_path: ./config/coding-principles.md

pipelines:
  fix-bug:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - AnalyzeCodeCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - TestCommand
      - CommitAndPRCommand

  add-feature:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - LoadCodingPrinciplesCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - GenerateTestsCommand
      - TestCommand
      - GenerateDocsCommand
      - CommitAndPRCommand

secrets:
  azure_devops_token: ${AZURE_DEVOPS_TOKEN}
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  openai_api_key: ${OPENAI_API_KEY}
  jira_token: ${JIRA_TOKEN}
  jira_email: ${JIRA_EMAIL}
```

---

## Coding Principles Template

```
File: config/coding-principles.md
```

Copied from `prompts/coding-principles.md`.
This is the file that the agent loads at runtime and sends to the LLM.

---

## Unit Tests

```
File: tests/AgentSmith.Tests/Configuration/YamlConfigurationLoaderTests.cs
```

**Test Cases:**
- `LoadConfig_ValidYaml_ReturnsConfig` - Happy Path
- `LoadConfig_FileNotFound_ThrowsConfigurationException`
- `LoadConfig_InvalidYaml_ThrowsConfigurationException`
- `LoadConfig_WithEnvVars_ResolvesPlaceholders`
- `LoadConfig_ProjectHasAllFields_MapsCorrectly`
- `LoadConfig_PipelineHasCommands_MapsCorrectly`

**Test Data:**
- Create test YAML files under `tests/AgentSmith.Tests/Configuration/TestData/`
- `valid-config.yml` - complete valid config
- `invalid-config.yml` - broken YAML

---

## Directory Structure After Step 4

```
src/AgentSmith.Contracts/Configuration/
├── AgentSmithConfig.cs
├── ProjectConfig.cs
├── SourceConfig.cs
├── TicketConfig.cs
├── AgentConfig.cs
└── PipelineConfig.cs

src/AgentSmith.Infrastructure/Configuration/
└── YamlConfigurationLoader.cs

config/
├── agentsmith.yml
└── coding-principles.md

tests/AgentSmith.Tests/Configuration/
├── YamlConfigurationLoaderTests.cs
└── TestData/
    ├── valid-config.yml
    └── invalid-config.yml
```

## Notes

- Config models need parameterless constructors (YamlDotNet deserialization).
- Properties with `{ get; set; }` (not `init`, due to deserialization).
- YAML property names in snake_case, C# properties in PascalCase → configure YamlDotNet `NamingConvention`.
- Secret resolution is intentionally lazy: unset env vars are not an error during loading.


---

# Phase 1 - Step 3: Contracts (Interfaces)

## Goal
Define all interfaces that hold the system together.
No implementation - only signatures.
Project: `AgentSmith.Contracts`

---

## Command Pattern (MediatR-Style)

The central pattern of the system. Strict separation of Command (What) and Handler (How).

### ICommandContext (Marker Interface)
```
File: src/AgentSmith.Contracts/Commands/ICommandContext.cs
```

```csharp
/// <summary>
/// Marker interface for all command contexts.
/// Each command defines its own context record implementing this interface.
/// </summary>
public interface ICommandContext;
```

### ICommandHandler\<TContext\>
```
File: src/AgentSmith.Contracts/Commands/ICommandHandler.cs
```

```csharp
/// <summary>
/// Handles a specific command context type.
/// Each handler implements exactly one ICommandContext.
/// Resolved via DI by the CommandExecutor.
/// </summary>
public interface ICommandHandler<in TContext> where TContext : ICommandContext
{
    Task<CommandResult> ExecuteAsync(
        TContext context,
        CancellationToken cancellationToken = default);
}
```

### ICommandExecutor
```
File: src/AgentSmith.Contracts/Commands/ICommandExecutor.cs
```

```csharp
/// <summary>
/// Resolves and executes the matching ICommandHandler for a given ICommandContext.
/// Central place for cross-cutting concerns (logging, error handling).
/// </summary>
public interface ICommandExecutor
{
    Task<CommandResult> ExecuteAsync<TContext>(
        TContext context,
        CancellationToken cancellationToken = default)
        where TContext : ICommandContext;
}
```

---

## PipelineContext

Concrete class in Contracts. The shared data store between pipeline steps.
Gets injected into the individual ICommandContext records.

```
File: src/AgentSmith.Contracts/Commands/PipelineContext.cs
```

**Properties & Methods:**
- Internal `Dictionary<string, object>` store
- `void Set<T>(string key, T value)` - Set a value
- `T Get<T>(string key)` - Get a value (throws if not present)
- `bool TryGet<T>(string key, out T? value)` - Get a value (safe)
- `bool Has(string key)` - Check if key exists

### ContextKeys
```
File: src/AgentSmith.Contracts/Commands/ContextKeys.cs
```

Predefined keys as constants:
```csharp
public static class ContextKeys
{
    public const string Ticket = "Ticket";
    public const string Repository = "Repository";
    public const string Plan = "Plan";
    public const string CodeChanges = "CodeChanges";
    public const string CodeAnalysis = "CodeAnalysis";
    public const string CodingPrinciples = "CodingPrinciples";
    public const string Approved = "Approved";
    public const string TestResults = "TestResults";
    public const string PullRequestUrl = "PullRequestUrl";
}
```

---

## Interaction: How Context → Handler → Executor Works

```
Pipeline Config (YAML)         ICommandContext Records         ICommandHandler<T>
─────────────────────          ──────────────────────          ──────────────────
"FetchTicketCommand"    →      FetchTicketContext       →      FetchTicketHandler
"CheckoutSourceCommand" →      CheckoutSourceContext    →      CheckoutSourceHandler
"ApprovalCommand"       →      ApprovalContext          →      ApprovalHandler
...                            ...                            ...

                    CommandExecutor
                    ───────────────
                    1. Receives ICommandContext
                    2. Resolves ICommandHandler<TContext> from DI
                    3. Calls handler.ExecuteAsync(context, ct)
                    4. Returns CommandResult
                    5. Handles logging + error wrapping
```

---

## Provider Interfaces

### ITicketProvider
```
File: src/AgentSmith.Contracts/Providers/ITicketProvider.cs
```

```csharp
public interface ITicketProvider
{
    string ProviderType { get; }
    Task<Ticket> GetTicketAsync(
        TicketId ticketId,
        CancellationToken cancellationToken = default);
}
```

### ISourceProvider
```
File: src/AgentSmith.Contracts/Providers/ISourceProvider.cs
```

```csharp
public interface ISourceProvider
{
    string ProviderType { get; }
    Task<Repository> CheckoutAsync(
        BranchName branch,
        CancellationToken cancellationToken = default);
    Task<string> CreatePullRequestAsync(
        Repository repository,
        string title,
        string description,
        CancellationToken cancellationToken = default);
    Task CommitAndPushAsync(
        Repository repository,
        string message,
        CancellationToken cancellationToken = default);
}
```

### IAgentProvider
```
File: src/AgentSmith.Contracts/Providers/IAgentProvider.cs
```

```csharp
public interface IAgentProvider
{
    string ProviderType { get; }
    Task<Plan> GeneratePlanAsync(
        Ticket ticket,
        CodeAnalysis codeAnalysis,
        string codingPrinciples,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CodeChange>> ExecutePlanAsync(
        Plan plan,
        Repository repository,
        string codingPrinciples,
        CancellationToken cancellationToken = default);
}
```

---

## Service Interfaces

### IConfigurationLoader
```
File: src/AgentSmith.Contracts/Services/IConfigurationLoader.cs
```

```csharp
public interface IConfigurationLoader
{
    AgentSmithConfig LoadConfig(string configPath);
}
```

Place config models in `AgentSmith.Contracts/Configuration/` (needed by multiple layers).

### IPipelineExecutor
```
File: src/AgentSmith.Contracts/Services/IPipelineExecutor.cs
```

```csharp
/// <summary>
/// Orchestrates a pipeline: builds ICommandContext records from command names,
/// dispatches them through the CommandExecutor, stops on first failure.
/// </summary>
public interface IPipelineExecutor
{
    Task<CommandResult> ExecuteAsync(
        IReadOnlyList<string> commandNames,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}
```

### IIntentParser
```
File: src/AgentSmith.Contracts/Services/IIntentParser.cs
```

```csharp
public interface IIntentParser
{
    Task<ParsedIntent> ParseAsync(
        string userInput,
        CancellationToken cancellationToken = default);
}
```

### ParsedIntent
```
File: src/AgentSmith.Contracts/Services/ParsedIntent.cs
```

```csharp
public sealed record ParsedIntent(
    TicketId TicketId,
    ProjectName ProjectName);
```

---

## Factory Interfaces

### ITicketProviderFactory
```
File: src/AgentSmith.Contracts/Providers/ITicketProviderFactory.cs
```

```csharp
public interface ITicketProviderFactory
{
    ITicketProvider Create(TicketConfig config);
}
```

### ISourceProviderFactory
```
File: src/AgentSmith.Contracts/Providers/ISourceProviderFactory.cs
```

```csharp
public interface ISourceProviderFactory
{
    ISourceProvider Create(SourceConfig config);
}
```

### IAgentProviderFactory
```
File: src/AgentSmith.Contracts/Providers/IAgentProviderFactory.cs
```

```csharp
public interface IAgentProviderFactory
{
    IAgentProvider Create(AgentConfig config);
}
```

---

## Directory Structure

```
src/AgentSmith.Contracts/
├── Commands/
│   ├── ICommandContext.cs
│   ├── ICommandHandler.cs
│   ├── ICommandExecutor.cs
│   ├── PipelineContext.cs
│   └── ContextKeys.cs
├── Providers/
│   ├── ITicketProvider.cs
│   ├── ISourceProvider.cs
│   ├── IAgentProvider.cs
│   ├── ITicketProviderFactory.cs
│   ├── ISourceProviderFactory.cs
│   └── IAgentProviderFactory.cs
├── Services/
│   ├── IConfigurationLoader.cs
│   ├── IPipelineExecutor.cs
│   ├── IIntentParser.cs
│   └── ParsedIntent.cs
└── Configuration/
    ├── AgentSmithConfig.cs
    ├── ProjectConfig.cs
    ├── SourceConfig.cs
    ├── TicketConfig.cs
    ├── AgentConfig.cs
    └── PipelineConfig.cs
```

## Notes

- All interfaces with XML-Doc Comments (English).
- `PipelineContext` and `ContextKeys` are the only concrete types in Contracts.
- `ICommandContext` is a pure marker interface - no methods.
- `ICommandHandler<TContext>` is generic - each handler implements exactly one combination.
- `ICommandExecutor` is the bridge between Context and Handler via DI.
- `IPipelineExecutor` works with command names (strings from YAML) and builds the Contexts.
- Config models as simple classes with properties (YamlDotNet deserialization).
- Factory interfaces take the respective config section as parameter.


---

# Phase 1 - Step 2: Domain Entities & Value Objects

## Goal
Define all core types of the system. Pure data models without infrastructure dependencies.
Project: `AgentSmith.Domain`

---

## Value Objects (Records)

Immutable, comparable by value, self-validating.

### TicketId
```
File: src/AgentSmith.Domain/ValueObjects/TicketId.cs
```
- `string Value` (not empty, not null)
- Guard clause in constructor
- Implicit conversion from/to string

### ProjectName
```
File: src/AgentSmith.Domain/ValueObjects/ProjectName.cs
```
- `string Value` (not empty, not null)
- Guard clause in constructor

### BranchName
```
File: src/AgentSmith.Domain/ValueObjects/BranchName.cs
```
- `string Value`
- Factory method: `FromTicket(TicketId ticketId, string prefix = "fix")` → e.g. `fix/12345`

### FilePath
```
File: src/AgentSmith.Domain/ValueObjects/FilePath.cs
```
- `string Value` (relative to repo root)
- Validation: no absolute path, no `..`

### CommandResult
```
File: src/AgentSmith.Domain/ValueObjects/CommandResult.cs
```
- `bool Success`
- `string Message`
- `Exception? Exception`
- Static factories: `CommandResult.Ok(message)`, `CommandResult.Fail(message, exception?)`

---

## Entities

Have identity, can change over time.

### Ticket
```
File: src/AgentSmith.Domain/Entities/Ticket.cs
```
- `TicketId Id`
- `string Title`
- `string Description`
- `string? AcceptanceCriteria`
- `string Status`
- `string Source` (e.g. "AzureDevOps", "Jira", "GitHub")

### Repository
```
File: src/AgentSmith.Domain/Entities/Repository.cs
```
- `string LocalPath`
- `BranchName CurrentBranch`
- `string RemoteUrl`

### Plan
```
File: src/AgentSmith.Domain/Entities/Plan.cs
```
- `string Summary`
- `IReadOnlyList<PlanStep> Steps`
- `string RawResponse` (original response from the agent)

### PlanStep
```
File: src/AgentSmith.Domain/Entities/PlanStep.cs
```
- `int Order`
- `string Description`
- `FilePath? TargetFile`
- `string ChangeType` (Create, Modify, Delete)

### CodeChange
```
File: src/AgentSmith.Domain/Entities/CodeChange.cs
```
- `FilePath Path`
- `string Content`
- `string ChangeType` (Create, Modify, Delete)

### CodeAnalysis
```
File: src/AgentSmith.Domain/Entities/CodeAnalysis.cs
```
- `IReadOnlyList<string> FileStructure`
- `IReadOnlyList<string> Dependencies`
- `string? Framework`
- `string? Language`

---

## Exceptions

All inherit from a base exception.

### AgentSmithException
```
File: src/AgentSmith.Domain/Exceptions/AgentSmithException.cs
```
- Base exception for all domain-specific errors
- Constructor: `(string message)`, `(string message, Exception innerException)`

### TicketNotFoundException
```
File: src/AgentSmith.Domain/Exceptions/TicketNotFoundException.cs
```
- Inherits from `AgentSmithException`
- Constructor: `(TicketId ticketId)`
- Message: `$"Ticket '{ticketId.Value}' not found."`

### ConfigurationException
```
File: src/AgentSmith.Domain/Exceptions/ConfigurationException.cs
```
- Inherits from `AgentSmithException`
- Constructor: `(string message)`

### ProviderException
```
File: src/AgentSmith.Domain/Exceptions/ProviderException.cs
```
- Inherits from `AgentSmithException`
- `string ProviderType` Property
- Constructor: `(string providerType, string message, Exception? innerException = null)`

---

## Directory Structure

```
src/AgentSmith.Domain/
├── Entities/
│   ├── Ticket.cs
│   ├── Repository.cs
│   ├── Plan.cs
│   ├── PlanStep.cs
│   ├── CodeChange.cs
│   └── CodeAnalysis.cs
├── ValueObjects/
│   ├── TicketId.cs
│   ├── ProjectName.cs
│   ├── BranchName.cs
│   ├── FilePath.cs
│   └── CommandResult.cs
└── Exceptions/
    ├── AgentSmithException.cs
    ├── TicketNotFoundException.cs
    ├── ConfigurationException.cs
    └── ProviderException.cs
```

## Notes

- Implement all Value Objects as `record`.
- Entities as `sealed class` (no inheritance needed for now).
- Guard clauses with `ArgumentException.ThrowIfNullOrWhiteSpace()` (.NET 8).
- No using of infrastructure namespaces.
- No NuGet package needed - pure C# types.


---

# Phase 1 - Step 1: Solution Structure

## Goal
.NET 8 Solution with all projects, correct references, and NuGet packages.
`dotnet build` must succeed without errors.

---

## Commands

```bash
# Solution erstellen
dotnet new sln -n AgentSmith

# Projekte erstellen
dotnet new classlib -n AgentSmith.Domain -o src/AgentSmith.Domain -f net8.0
dotnet new classlib -n AgentSmith.Contracts -o src/AgentSmith.Contracts -f net8.0
dotnet new classlib -n AgentSmith.Application -o src/AgentSmith.Application -f net8.0
dotnet new classlib -n AgentSmith.Infrastructure -o src/AgentSmith.Infrastructure -f net8.0
dotnet new console -n AgentSmith.Cli -o src/AgentSmith.Cli -f net8.0
dotnet new xunit -n AgentSmith.Tests -o tests/AgentSmith.Tests -f net8.0

# Zur Solution hinzufügen
dotnet sln add src/AgentSmith.Domain
dotnet sln add src/AgentSmith.Contracts
dotnet sln add src/AgentSmith.Application
dotnet sln add src/AgentSmith.Infrastructure
dotnet sln add src/AgentSmith.Cli
dotnet sln add tests/AgentSmith.Tests

# Projekt-Referenzen
dotnet add src/AgentSmith.Contracts reference src/AgentSmith.Domain
dotnet add src/AgentSmith.Application reference src/AgentSmith.Contracts
dotnet add src/AgentSmith.Application reference src/AgentSmith.Domain
dotnet add src/AgentSmith.Infrastructure reference src/AgentSmith.Contracts
dotnet add src/AgentSmith.Infrastructure reference src/AgentSmith.Domain
dotnet add src/AgentSmith.Cli reference src/AgentSmith.Application
dotnet add src/AgentSmith.Cli reference src/AgentSmith.Infrastructure
dotnet add tests/AgentSmith.Tests reference src/AgentSmith.Domain
dotnet add tests/AgentSmith.Tests reference src/AgentSmith.Contracts
dotnet add tests/AgentSmith.Tests reference src/AgentSmith.Infrastructure

# NuGet Packages
dotnet add src/AgentSmith.Infrastructure package YamlDotNet
dotnet add src/AgentSmith.Cli package Microsoft.Extensions.DependencyInjection
dotnet add src/AgentSmith.Cli package Microsoft.Extensions.Logging.Console
dotnet add tests/AgentSmith.Tests package Moq
dotnet add tests/AgentSmith.Tests package FluentAssertions
```

## Directory Structure After Step 1

```
AgentSmith.sln
├── src/
│   ├── AgentSmith.Domain/
│   │   └── AgentSmith.Domain.csproj
│   ├── AgentSmith.Contracts/
│   │   └── AgentSmith.Contracts.csproj
│   ├── AgentSmith.Application/
│   │   └── AgentSmith.Application.csproj
│   ├── AgentSmith.Infrastructure/
│   │   └── AgentSmith.Infrastructure.csproj
│   └── AgentSmith.Cli/
│       ├── AgentSmith.Cli.csproj
│       └── Program.cs
├── tests/
│   └── AgentSmith.Tests/
│       └── AgentSmith.Tests.csproj
├── config/
└── prompts/
```

## Notes

- Delete all auto-generated `Class1.cs` files.
- Enable Nullable Reference Types in all projects (`<Nullable>enable</Nullable>`).
- Enable Implicit Usings (`<ImplicitUsings>enable</ImplicitUsings>`).
- Keep `Program.cs` in Host minimal for now: just `Console.WriteLine("Agent Smith")`.

## Result
```bash
dotnet build  # Must be error-free
```
