# Phase 2 - Schritt 3: Command Handlers (Stubs)

## Ziel
Alle 9 Free Command Handlers als Stubs implementieren.
Jeder Handler hat:
- DI-fähigen Constructor (nimmt die richtigen Factories/Providers)
- `ILogger<T>` für Logging
- Guard Clauses am Anfang
- TODO-Kommentar wo die echte Logik hinkommt
- Schreibt Dummy-Daten in PipelineContext (damit die Pipeline durchläuft)

---

## Projekt
`AgentSmith.Application/Commands/Handlers/`

## Pattern für jeden Handler

```csharp
public sealed class XxxHandler(
    IXxxFactory factory,           // oder passender Provider/Service
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
Datei: src/AgentSmith.Application/Commands/Handlers/FetchTicketHandler.cs
```
- DI: `ITicketProviderFactory`
- Stub: Schreibt Dummy-Ticket in PipelineContext
- TODO: `var provider = factory.Create(context.Config); var ticket = await provider.GetTicketAsync(...)`

### CheckoutSourceHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/CheckoutSourceHandler.cs
```
- DI: `ISourceProviderFactory`
- Stub: Schreibt Dummy-Repository in PipelineContext
- TODO: `var provider = factory.Create(context.Config); var repo = await provider.CheckoutAsync(...)`

### LoadCodingPrinciplesHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/LoadCodingPrinciplesHandler.cs
```
- DI: nur `ILogger` (liest direkt vom Dateisystem)
- Stub: Schreibt leeren String in PipelineContext
- TODO: `File.ReadAllTextAsync(context.FilePath, ct)`
- Hinweis: Dieser Handler kann schon in Phase 2 fertig implementiert werden (kein Provider nötig)

### AnalyzeCodeHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/AnalyzeCodeHandler.cs
```
- DI: nur `ILogger`
- Stub: Schreibt leere CodeAnalysis in PipelineContext
- TODO: Directory traversal, dependency detection

### GeneratePlanHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/GeneratePlanHandler.cs
```
- DI: `IAgentProviderFactory`
- Stub: Schreibt Dummy-Plan in PipelineContext
- TODO: `var provider = factory.Create(context.AgentConfig); var plan = await provider.GeneratePlanAsync(...)`

### ApprovalHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/ApprovalHandler.cs
```
- DI: nur `ILogger`
- Stub: Auto-approved, schreibt `true` in PipelineContext
- TODO: Console.ReadLine() prompt (y/n)
- Hinweis: Kann in Phase 2 schon fertig implementiert werden

### AgenticExecuteHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/AgenticExecuteHandler.cs
```
- DI: `IAgentProviderFactory`
- Stub: Schreibt leere CodeChange-Liste in PipelineContext
- TODO: `var provider = factory.Create(context.AgentConfig); var changes = await provider.ExecutePlanAsync(...)`

### TestHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/TestHandler.cs
```
- DI: nur `ILogger`
- Stub: Schreibt "All tests passed" in PipelineContext
- TODO: Process.Start für `dotnet test` / `npm test` etc.

### CommitAndPRHandler
```
Datei: src/AgentSmith.Application/Commands/Handlers/CommitAndPRHandler.cs
```
- DI: `ISourceProviderFactory`
- Stub: Schreibt Dummy-PR-URL in PipelineContext
- TODO: `var provider = factory.Create(context.SourceConfig); await provider.CommitAndPushAsync(...)`

---

## DI Registration

```
Datei: src/AgentSmith.Application/ServiceCollectionExtensions.cs
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

## Hinweise
- Primary Constructors verwenden (.NET 8).
- Alle Handler `sealed`.
- `ILogger<T>` in jedem Handler.
- Guard Clauses: Context-Properties auf null/empty prüfen wo sinnvoll.
- Stub-Daten müssen "realistisch genug" sein damit nachfolgende Commands nicht crashen.
- `LoadCodingPrinciplesHandler` und `ApprovalHandler` können schon fertig implementiert werden.
