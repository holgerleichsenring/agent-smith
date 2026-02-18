# Phase 2 - Schritt 1: CommandExecutor

## Ziel
Die zentrale Klasse die `ICommandHandler<TContext>` per DI auflöst und ausführt.
Einzige nicht-Stub Implementierung in Phase 2.

---

## CommandExecutor
```
Datei: src/AgentSmith.Application/Commands/CommandExecutor.cs
```
Projekt: `AgentSmith.Application`

Implementiert: `ICommandExecutor` (aus Contracts)

### Verantwortung
1. Nimmt `IServiceProvider` per Constructor Injection
2. Bei `ExecuteAsync<TContext>` → löst `ICommandHandler<TContext>` aus DI auf
3. Ruft `handler.ExecuteAsync(context, ct)` auf
4. Fängt Exceptions, wrapped sie in `CommandResult.Fail`
5. Loggt Start, Ergebnis und ggf. Fehler

### Verhalten
- Handler nicht gefunden → `CommandResult.Fail("No handler registered for {typeof(TContext).Name}")`
- Handler wirft Exception → `CommandResult.Fail(ex.Message, ex)` + Logging
- Handler gibt `CommandResult` zurück → durchreichen

### Code-Skizze
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

### Hinweise
- Primary Constructor verwenden (.NET 8).
- `IServiceProvider.GetService<T>()` statt `GetRequiredService<T>()` → eigene Fehlermeldung statt DI-Exception.
- Logging Levels: Info für Start/Ende, Error für Exceptions.
- `sealed` Klasse.
