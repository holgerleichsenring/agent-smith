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
