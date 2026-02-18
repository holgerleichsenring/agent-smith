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
