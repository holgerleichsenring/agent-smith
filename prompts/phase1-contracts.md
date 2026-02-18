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
