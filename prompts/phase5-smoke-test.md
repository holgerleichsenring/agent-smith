# Phase 5 - Schritt 3: Smoke Tests

## Ziel
Sicherstellen dass DI-Container korrekt aufgebaut wird und CLI-Parsing funktioniert.
Keine echten API-Calls, nur Struktur-Validierung.

---

## DI Integration Test

```
Datei: tests/AgentSmith.Tests/Integration/DiRegistrationTests.cs
```

Test baut den kompletten DI-Container und prüft ob alle Services auflösbar sind:

```csharp
[Fact]
public void AllServices_Resolvable()
{
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddAgentSmithInfrastructure();
    services.AddAgentSmithCommands();
    var provider = services.BuildServiceProvider();

    // Alle kritischen Services auflösen
    provider.GetRequiredService<ProcessTicketUseCase>();
    provider.GetRequiredService<ICommandExecutor>();
    provider.GetRequiredService<IIntentParser>();
    provider.GetRequiredService<IPipelineExecutor>();
    provider.GetRequiredService<IConfigurationLoader>();
    provider.GetRequiredService<ITicketProviderFactory>();
    provider.GetRequiredService<ISourceProviderFactory>();
    provider.GetRequiredService<IAgentProviderFactory>();
}
```

---

## CLI Smoke Test

Prüft nur die CLI-Argument-Struktur:
- `--help` gibt Hilfetext aus und exit 0
- Ohne Argumente gibt Fehler und exit 1
- `--dry-run` mit gültiger Config parsed Intent ohne Pipeline-Ausführung

---

## Was NICHT getestet wird

- Echte API-Calls (GitHub, Azure DevOps, Anthropic)
- Echte Git-Operationen
- Docker Build (das ist ein CI/CD-Thema)
