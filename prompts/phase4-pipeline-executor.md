# Phase 4 - Schritt 2: PipelineExecutor + CommandContextFactory

## Ziel
Aus einer Liste von Command-Namen (aus YAML) die passenden Contexts bauen und
sequentiell via CommandExecutor ausführen.
Projekt: `AgentSmith.Application/Services/`

---

## CommandContextFactory

```
Datei: src/AgentSmith.Application/Services/CommandContextFactory.cs
```

Baut den passenden `ICommandContext` aus einem Command-Namen + Config + PipelineContext.

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

**Hinweise:**
- Frühe Commands (FetchTicket, Checkout) holen Daten aus Config
- Spätere Commands (GeneratePlan, Agentic) holen Daten aus PipelineContext (vorherige Steps)
- TicketId wird vor dem Pipeline-Start in den PipelineContext gesetzt
- Unbekannter Command-Name → `ConfigurationException`

---

## PipelineExecutor

```
Datei: src/AgentSmith.Application/Services/PipelineExecutor.cs
```

Implementiert `IPipelineExecutor` aus Contracts.

**Constructor:**
- `ICommandExecutor commandExecutor`
- `ICommandContextFactory contextFactory`
- `ProjectConfig projectConfig` (über Factory/DI oder direkt übergeben)
- `ILogger<PipelineExecutor> logger`

**Problem:** PipelineExecutor braucht `ProjectConfig`, die aber pro Aufruf anders ist.
**Lösung:** ProjectConfig wird nicht per DI injiziert, sondern als Parameter übergeben.

Anpassung Interface:
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
1. Für jeden Command-Namen:
   a. `contextFactory.Create(name, projectConfig, pipeline)` → ICommandContext
   b. `commandExecutor.ExecuteAsync(context, ct)` → CommandResult
   c. Log: Command-Name + Success/Fail
   d. Bei Fail → sofort return mit Fail-Result
2. Alle erfolgreich → `CommandResult.Ok("Pipeline completed")`

**Herausforderung:** `ExecuteAsync<TContext>` ist generisch, der Compile-Time-Typ
ist aber `ICommandContext`. Lösung: Reflection oder Dictionary mit Delegates.

Pragmatischer Ansatz: `ExecuteCommandAsync(ICommandContext context)` Methode
die per Pattern Matching den richtigen generischen Call macht:
```csharp
private Task<CommandResult> ExecuteCommandAsync(ICommandContext context, CancellationToken ct)
{
    return context switch
    {
        FetchTicketContext c => commandExecutor.ExecuteAsync(c, ct),
        CheckoutSourceContext c => commandExecutor.ExecuteAsync(c, ct),
        // ... alle 9 Commands
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
