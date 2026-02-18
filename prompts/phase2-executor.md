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
