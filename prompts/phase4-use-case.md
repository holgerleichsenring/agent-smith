# Phase 4 - Schritt 3: ProcessTicketUseCase

## Ziel
Zentraler Einstiegspunkt: User Input → Config → Intent → Pipeline → Ergebnis.
Projekt: `AgentSmith.Application/UseCases/`

---

## ProcessTicketUseCase

```
Datei: src/AgentSmith.Application/UseCases/ProcessTicketUseCase.cs
```

**Constructor:**
- `IConfigurationLoader configLoader`
- `IIntentParser intentParser`
- `IPipelineExecutor pipelineExecutor`
- `ILogger<ProcessTicketUseCase> logger`

**ExecuteAsync(string userInput, string configPath, CancellationToken):**

1. Config laden: `configLoader.LoadAsync(configPath, ct)`
2. Intent parsen: `intentParser.ParseAsync(userInput, ct)`
3. Project finden: `config.Projects[intent.ProjectName.Value]`
   - Nicht gefunden → `ConfigurationException("Project '{name}' not found")`
4. Pipeline finden: `config.Pipelines[projectConfig.Pipeline]`
   - Nicht gefunden → `ConfigurationException("Pipeline '{name}' not found")`
5. PipelineContext erstellen, TicketId setzen
6. Pipeline ausführen: `pipelineExecutor.ExecuteAsync(commands, projectConfig, pipeline, ct)`
7. Ergebnis loggen und zurückgeben

**Return:** `CommandResult` (Ok mit PR URL oder Fail mit Fehlerdetails)

---

## Hinweise

- UseCase ist dünn, orchestriert nur
- Keine Business-Logik, nur Wiring
- Fehlerbehandlung: Exceptions aus Providern werden zum Caller propagiert
- Log-Level: Information für Start/Ende, Warning für Fehler

---

## Tests

**ProcessTicketUseCaseTests:**
- `ExecuteAsync_ValidInput_RunsPipeline` (alle Dependencies gemockt)
- `ExecuteAsync_UnknownProject_ThrowsConfigurationException`
- `ExecuteAsync_UnknownPipeline_ThrowsConfigurationException`
