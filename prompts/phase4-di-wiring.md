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
File: src/AgentSmith.Host/Program.cs
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
