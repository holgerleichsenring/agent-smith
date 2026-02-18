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
