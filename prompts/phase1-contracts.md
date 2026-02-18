# Phase 1 - Schritt 3: Contracts (Interfaces)

## Ziel
Alle Interfaces definieren, die das System zusammenhalten.
Keine Implementierung - nur Signaturen.
Projekt: `AgentSmith.Contracts`

---

## Command Pattern (MediatR-Style)

Das zentrale Pattern des Systems. Strikte Trennung von Command (Was) und Handler (Wie).

### ICommandContext (Marker Interface)
```
Datei: src/AgentSmith.Contracts/Commands/ICommandContext.cs
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
Datei: src/AgentSmith.Contracts/Commands/ICommandHandler.cs
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
Datei: src/AgentSmith.Contracts/Commands/ICommandExecutor.cs
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

Konkrete Klasse in Contracts. Der gemeinsame Datenspeicher zwischen Pipeline-Schritten.
Wird in die einzelnen ICommandContext-Records injiziert.

```
Datei: src/AgentSmith.Contracts/Commands/PipelineContext.cs
```

**Properties & Methoden:**
- Interner `Dictionary<string, object>` Speicher
- `void Set<T>(string key, T value)` - Wert setzen
- `T Get<T>(string key)` - Wert holen (wirft wenn nicht vorhanden)
- `bool TryGet<T>(string key, out T? value)` - Wert holen (safe)
- `bool Has(string key)` - Prüfen ob Key existiert

### ContextKeys
```
Datei: src/AgentSmith.Contracts/Commands/ContextKeys.cs
```

Vordefinierte Keys als Konstanten:
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

## Zusammenspiel: Wie Context → Handler → Executor funktioniert

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
Datei: src/AgentSmith.Contracts/Providers/ITicketProvider.cs
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
Datei: src/AgentSmith.Contracts/Providers/ISourceProvider.cs
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
Datei: src/AgentSmith.Contracts/Providers/IAgentProvider.cs
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
Datei: src/AgentSmith.Contracts/Services/IConfigurationLoader.cs
```

```csharp
public interface IConfigurationLoader
{
    AgentSmithConfig LoadConfig(string configPath);
}
```

Config-Models in `AgentSmith.Contracts/Configuration/` ablegen (werden von mehreren Layern gebraucht).

### IPipelineExecutor
```
Datei: src/AgentSmith.Contracts/Services/IPipelineExecutor.cs
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
Datei: src/AgentSmith.Contracts/Services/IIntentParser.cs
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
Datei: src/AgentSmith.Contracts/Services/ParsedIntent.cs
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
Datei: src/AgentSmith.Contracts/Providers/ITicketProviderFactory.cs
```

```csharp
public interface ITicketProviderFactory
{
    ITicketProvider Create(TicketConfig config);
}
```

### ISourceProviderFactory
```
Datei: src/AgentSmith.Contracts/Providers/ISourceProviderFactory.cs
```

```csharp
public interface ISourceProviderFactory
{
    ISourceProvider Create(SourceConfig config);
}
```

### IAgentProviderFactory
```
Datei: src/AgentSmith.Contracts/Providers/IAgentProviderFactory.cs
```

```csharp
public interface IAgentProviderFactory
{
    IAgentProvider Create(AgentConfig config);
}
```

---

## Verzeichnisstruktur

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

## Hinweise

- Alle Interfaces mit XML-Doc Comments (englisch).
- `PipelineContext` und `ContextKeys` sind die einzigen konkreten Typen in Contracts.
- `ICommandContext` ist ein reines Marker Interface - keine Methoden.
- `ICommandHandler<TContext>` ist generisch - jeder Handler implementiert genau eine Kombination.
- `ICommandExecutor` ist die Brücke zwischen Context und Handler via DI.
- `IPipelineExecutor` arbeitet mit Command-Namen (Strings aus YAML) und baut die Contexts.
- Config-Models als einfache Klassen mit Properties (YamlDotNet Deserialisierung).
- Factory-Interfaces nehmen die jeweilige Config-Sektion als Parameter.
