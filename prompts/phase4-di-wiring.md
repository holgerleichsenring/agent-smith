# Phase 4 - Schritt 4: DI Wiring

## Ziel
Alle Phase-4-Komponenten in DI registrieren.
Infrastructure ServiceCollectionExtensions + Host Program.cs updaten.

---

## Infrastructure Registration

```
Datei: src/AgentSmith.Infrastructure/ServiceCollectionExtensions.cs
```

Neue Extension Method: `AddAgentSmithInfrastructure()`

Registriert:
- `SecretsProvider` → Singleton
- `ITicketProviderFactory` → `TicketProviderFactory` → Singleton
- `ISourceProviderFactory` → `SourceProviderFactory` → Singleton
- `IAgentProviderFactory` → `AgentProviderFactory` → Singleton
- `IConfigurationLoader` → `YamlConfigurationLoader` → Singleton

---

## Application Registration Update

```
Datei: src/AgentSmith.Application/ServiceCollectionExtensions.cs
```

`AddAgentSmithCommands()` erweitern um:
- `IIntentParser` → `RegexIntentParser` → Transient
- `ICommandContextFactory` → `CommandContextFactory` → Transient
- `IPipelineExecutor` → `PipelineExecutor` → Transient
- `ProcessTicketUseCase` → Transient

---

## Host Program.cs

```
Datei: src/AgentSmith.Host/Program.cs
```

Minimal-CLI ohne CommandLineParser (das kommt in Phase 5):

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

## Contract Änderung

`IPipelineExecutor` Interface muss angepasst werden (ProjectConfig Parameter):
```csharp
Task<CommandResult> ExecuteAsync(
    IReadOnlyList<string> commandNames,
    ProjectConfig projectConfig,
    PipelineContext context,
    CancellationToken cancellationToken = default);
```

Diese Änderung auf `architecture.md` reflektieren!

---

## Tests

DI-Integrations-Test:
- `ServiceRegistration_AllServicesResolvable` - Baut ServiceProvider, resolved alle Typen
