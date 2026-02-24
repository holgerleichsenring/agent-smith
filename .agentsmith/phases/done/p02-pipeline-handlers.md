# Phase 2: Free Commands (Stubs) - Implementation Plan

## Goal
Implement all Free Command Contexts + Handlers as stubs.
Handlers have real signatures, logging, and error handling, but TODO bodies for the provider calls.
Additionally: `CommandExecutor` as a real implementation (the heart of DI resolution).

After Phase 2: The pipeline is "wired up" - commands run through but don't do anything real yet.

---

## Prerequisite
- Phase 1 completed (solution compiles, contracts defined)

## Steps

### Step 1: CommandExecutor Implementation
See: `prompts/phase2-executor.md`

The central class that resolves `ICommandHandler<TContext>` via DI.
The only non-stub implementation in Phase 2.
Project: `AgentSmith.Application`

### Step 2: Command Contexts
See: `prompts/phase2-contexts.md`

Define all 9 Free Command Context records.
Project: `AgentSmith.Application/Commands/Contexts/`

### Step 3: Command Handlers (Stubs)
See: `prompts/phase2-handlers.md`

All 9 Free Command Handlers with:
- Real constructor (DI-capable, takes factories/providers)
- Logging (ILogger)
- Guard clauses
- TODO in body for real logic
- Writes dummy data to PipelineContext
Project: `AgentSmith.Application/Commands/Handlers/`

### Step 4: DI Registration
`ServiceCollectionExtensions` in Application for handler registration.

### Step 5: Tests
- CommandExecutor tests (with mock handlers)
- At least 1 handler stub test (logging, guard clauses)

### Step 6: Verify
```bash
dotnet build
dotnet test
```

---

## Dependencies

```
Step 1 (CommandExecutor)
    └── Step 2 (Contexts) ← can be done in parallel with 1
         └── Step 3 (Handlers) ← requires Contexts + Executor interface
              └── Step 4 (DI Registration)
                   └── Step 5 (Tests)
                        └── Step 6 (Verify)
```

Steps 1 and 2 can be done in parallel, the rest sequentially.

---

## NuGet Packages (Phase 2)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Application | Microsoft.Extensions.Logging.Abstractions | ILogger<T> |
| AgentSmith.Application | Microsoft.Extensions.DependencyInjection.Abstractions | IServiceCollection Extensions |

---

## Definition of Done (Phase 2)
- [ ] CommandExecutor resolves handlers via DI and calls ExecuteAsync
- [ ] All 9 Free Command Contexts defined
- [ ] All 9 Free Command Handlers implemented as stubs
- [ ] Each handler logs start/end and has guard clauses
- [ ] DI registration for all handlers present
- [ ] CommandExecutor tests green
- [ ] `dotnet build` + `dotnet test` error-free
- [ ] All files adhere to coding principles (20/120, English)


---

# Phase 2 - Step 2: Command Contexts

## Goal
Define all 9 Free Command Context records.
Each context is a `sealed record` that implements `ICommandContext`.

---

## Project
`AgentSmith.Application/Commands/Contexts/`

## Contexts

### FetchTicketContext
```
File: src/AgentSmith.Application/Commands/Contexts/FetchTicketContext.cs
```
```csharp
public sealed record FetchTicketContext(
    TicketId TicketId,
    TicketConfig Config,
    PipelineContext Pipeline) : ICommandContext;
```

### CheckoutSourceContext
```
File: src/AgentSmith.Application/Commands/Contexts/CheckoutSourceContext.cs
```
```csharp
public sealed record CheckoutSourceContext(
    SourceConfig Config,
    BranchName Branch,
    PipelineContext Pipeline) : ICommandContext;
```

### LoadCodingPrinciplesContext
```
File: src/AgentSmith.Application/Commands/Contexts/LoadCodingPrinciplesContext.cs
```
```csharp
public sealed record LoadCodingPrinciplesContext(
    string FilePath,
    PipelineContext Pipeline) : ICommandContext;
```

### AnalyzeCodeContext
```
File: src/AgentSmith.Application/Commands/Contexts/AnalyzeCodeContext.cs
```
```csharp
public sealed record AnalyzeCodeContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
```

### GeneratePlanContext
```
File: src/AgentSmith.Application/Commands/Contexts/GeneratePlanContext.cs
```
```csharp
public sealed record GeneratePlanContext(
    Ticket Ticket,
    CodeAnalysis CodeAnalysis,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
```

### ApprovalContext
```
File: src/AgentSmith.Application/Commands/Contexts/ApprovalContext.cs
```
```csharp
public sealed record ApprovalContext(
    Plan Plan,
    PipelineContext Pipeline) : ICommandContext;
```

### AgenticExecuteContext
```
File: src/AgentSmith.Application/Commands/Contexts/AgenticExecuteContext.cs
```
```csharp
public sealed record AgenticExecuteContext(
    Plan Plan,
    Repository Repository,
    string CodingPrinciples,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
```

### TestContext
```
File: src/AgentSmith.Application/Commands/Contexts/TestContext.cs
```
```csharp
public sealed record TestContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    PipelineContext Pipeline) : ICommandContext;
```

### CommitAndPRContext
```
File: src/AgentSmith.Application/Commands/Contexts/CommitAndPRContext.cs
```
```csharp
public sealed record CommitAndPRContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    Ticket Ticket,
    SourceConfig SourceConfig,
    PipelineContext Pipeline) : ICommandContext;
```

---

## Notes
- All `sealed record` - immutable by design.
- Each context has `PipelineContext Pipeline` as the last parameter.
- Records need the correct `using` statements for domain types and config types.
- No behavior in contexts - data only.
- One type per file.


---

# Phase 2 - Step 1: CommandExecutor

## Goal
The central class that resolves `ICommandHandler<TContext>` via DI and executes it.
The only non-stub implementation in Phase 2.

---

## CommandExecutor
```
File: src/AgentSmith.Application/Commands/CommandExecutor.cs
```
Project: `AgentSmith.Application`

Implements: `ICommandExecutor` (from Contracts)

### Responsibility
1. Takes `IServiceProvider` via constructor injection
2. On `ExecuteAsync<TContext>` -> resolves `ICommandHandler<TContext>` from DI
3. Calls `handler.ExecuteAsync(context, ct)`
4. Catches exceptions, wraps them in `CommandResult.Fail`
5. Logs start, result, and errors if applicable

### Behavior
- Handler not found -> `CommandResult.Fail("No handler registered for {typeof(TContext).Name}")`
- Handler throws exception -> `CommandResult.Fail(ex.Message, ex)` + logging
- Handler returns `CommandResult` -> pass through

### Code Sketch
```csharp
public sealed class CommandExecutor(
    IServiceProvider serviceProvider,
    ILogger<CommandExecutor> logger) : ICommandExecutor
{
    public async Task<CommandResult> ExecuteAsync<TContext>(
        TContext context, CancellationToken ct)
        where TContext : ICommandContext
    {
        var contextName = typeof(TContext).Name;
        logger.LogInformation("Executing {Command}...", contextName);

        var handler = serviceProvider.GetService<ICommandHandler<TContext>>();
        if (handler is null)
            return CommandResult.Fail($"No handler registered for {contextName}");

        try
        {
            var result = await handler.ExecuteAsync(context, ct);
            LogResult(contextName, result);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Handler {Command} failed", contextName);
            return CommandResult.Fail(ex.Message, ex);
        }
    }

    private void LogResult(string name, CommandResult result) { ... }
}
```

### Notes
- Use primary constructor (.NET 8).
- `IServiceProvider.GetService<T>()` instead of `GetRequiredService<T>()` -> custom error message instead of DI exception.
- Logging levels: Info for start/end, Error for exceptions.
- `sealed` class.


---

# Phase 2 - Step 3: Command Handlers (Stubs)

## Goal
Implement all 9 Free Command Handlers as stubs.
Each handler has:
- DI-capable constructor (takes the correct factories/providers)
- `ILogger<T>` for logging
- Guard clauses at the beginning
- TODO comment where the real logic goes
- Writes dummy data to PipelineContext (so the pipeline runs through)

---

## Project
`AgentSmith.Application/Commands/Handlers/`

## Pattern for Each Handler

```csharp
public sealed class XxxHandler(
    IXxxFactory factory,           // or appropriate provider/service
    ILogger<XxxHandler> logger)
    : ICommandHandler<XxxContext>
{
    public async Task<CommandResult> ExecuteAsync(
        XxxContext context, CancellationToken ct)
    {
        logger.LogInformation("Starting {Command}...", nameof(XxxContext));

        // TODO: Implement in Phase 3
        await Task.CompletedTask;

        // Write stub data to pipeline
        context.Pipeline.Set(ContextKeys.Xxx, stubData);

        return CommandResult.Ok("Xxx completed (stub)");
    }
}
```

---

## Handlers

### FetchTicketHandler
```
File: src/AgentSmith.Application/Commands/Handlers/FetchTicketHandler.cs
```
- DI: `ITicketProviderFactory`
- Stub: Writes dummy ticket to PipelineContext
- TODO: `var provider = factory.Create(context.Config); var ticket = await provider.GetTicketAsync(...)`

### CheckoutSourceHandler
```
File: src/AgentSmith.Application/Commands/Handlers/CheckoutSourceHandler.cs
```
- DI: `ISourceProviderFactory`
- Stub: Writes dummy repository to PipelineContext
- TODO: `var provider = factory.Create(context.Config); var repo = await provider.CheckoutAsync(...)`

### LoadCodingPrinciplesHandler
```
File: src/AgentSmith.Application/Commands/Handlers/LoadCodingPrinciplesHandler.cs
```
- DI: only `ILogger` (reads directly from the file system)
- Stub: Writes empty string to PipelineContext
- TODO: `File.ReadAllTextAsync(context.FilePath, ct)`
- Note: This handler can already be fully implemented in Phase 2 (no provider needed)

### AnalyzeCodeHandler
```
File: src/AgentSmith.Application/Commands/Handlers/AnalyzeCodeHandler.cs
```
- DI: only `ILogger`
- Stub: Writes empty CodeAnalysis to PipelineContext
- TODO: Directory traversal, dependency detection

### GeneratePlanHandler
```
File: src/AgentSmith.Application/Commands/Handlers/GeneratePlanHandler.cs
```
- DI: `IAgentProviderFactory`
- Stub: Writes dummy plan to PipelineContext
- TODO: `var provider = factory.Create(context.AgentConfig); var plan = await provider.GeneratePlanAsync(...)`

### ApprovalHandler
```
File: src/AgentSmith.Application/Commands/Handlers/ApprovalHandler.cs
```
- DI: only `ILogger`
- Stub: Auto-approved, writes `true` to PipelineContext
- TODO: Console.ReadLine() prompt (y/n)
- Note: Can already be fully implemented in Phase 2

### AgenticExecuteHandler
```
File: src/AgentSmith.Application/Commands/Handlers/AgenticExecuteHandler.cs
```
- DI: `IAgentProviderFactory`
- Stub: Writes empty CodeChange list to PipelineContext
- TODO: `var provider = factory.Create(context.AgentConfig); var changes = await provider.ExecutePlanAsync(...)`

### TestHandler
```
File: src/AgentSmith.Application/Commands/Handlers/TestHandler.cs
```
- DI: only `ILogger`
- Stub: Writes "All tests passed" to PipelineContext
- TODO: Process.Start for `dotnet test` / `npm test` etc.

### CommitAndPRHandler
```
File: src/AgentSmith.Application/Commands/Handlers/CommitAndPRHandler.cs
```
- DI: `ISourceProviderFactory`
- Stub: Writes dummy PR URL to PipelineContext
- TODO: `var provider = factory.Create(context.SourceConfig); await provider.CommitAndPushAsync(...)`

---

## DI Registration

```
File: src/AgentSmith.Application/ServiceCollectionExtensions.cs
```

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAgentSmithCommands(
        this IServiceCollection services)
    {
        services.AddSingleton<ICommandExecutor, CommandExecutor>();

        services.AddTransient<ICommandHandler<FetchTicketContext>, FetchTicketHandler>();
        services.AddTransient<ICommandHandler<CheckoutSourceContext>, CheckoutSourceHandler>();
        services.AddTransient<ICommandHandler<LoadCodingPrinciplesContext>, LoadCodingPrinciplesHandler>();
        services.AddTransient<ICommandHandler<AnalyzeCodeContext>, AnalyzeCodeHandler>();
        services.AddTransient<ICommandHandler<GeneratePlanContext>, GeneratePlanHandler>();
        services.AddTransient<ICommandHandler<ApprovalContext>, ApprovalHandler>();
        services.AddTransient<ICommandHandler<AgenticExecuteContext>, AgenticExecuteHandler>();
        services.AddTransient<ICommandHandler<TestContext>, TestHandler>();
        services.AddTransient<ICommandHandler<CommitAndPRContext>, CommitAndPRHandler>();

        return services;
    }
}
```

---

## Notes
- Use primary constructors (.NET 8).
- All handlers `sealed`.
- `ILogger<T>` in every handler.
- Guard clauses: Check context properties for null/empty where appropriate.
- Stub data must be "realistic enough" so that subsequent commands don't crash.
- `LoadCodingPrinciplesHandler` and `ApprovalHandler` can already be fully implemented.
