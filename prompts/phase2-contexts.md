# Phase 2 - Schritt 2: Command Contexts

## Ziel
Alle 9 Free Command Context Records definieren.
Jeder Context ist ein `sealed record` das `ICommandContext` implementiert.

---

## Projekt
`AgentSmith.Application/Commands/Contexts/`

## Contexts

### FetchTicketContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/FetchTicketContext.cs
```
```csharp
public sealed record FetchTicketContext(
    TicketId TicketId,
    TicketConfig Config,
    PipelineContext Pipeline) : ICommandContext;
```

### CheckoutSourceContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/CheckoutSourceContext.cs
```
```csharp
public sealed record CheckoutSourceContext(
    SourceConfig Config,
    BranchName Branch,
    PipelineContext Pipeline) : ICommandContext;
```

### LoadCodingPrinciplesContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/LoadCodingPrinciplesContext.cs
```
```csharp
public sealed record LoadCodingPrinciplesContext(
    string FilePath,
    PipelineContext Pipeline) : ICommandContext;
```

### AnalyzeCodeContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/AnalyzeCodeContext.cs
```
```csharp
public sealed record AnalyzeCodeContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
```

### GeneratePlanContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/GeneratePlanContext.cs
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
Datei: src/AgentSmith.Application/Commands/Contexts/ApprovalContext.cs
```
```csharp
public sealed record ApprovalContext(
    Plan Plan,
    PipelineContext Pipeline) : ICommandContext;
```

### AgenticExecuteContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/AgenticExecuteContext.cs
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
Datei: src/AgentSmith.Application/Commands/Contexts/TestContext.cs
```
```csharp
public sealed record TestContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    PipelineContext Pipeline) : ICommandContext;
```

### CommitAndPRContext
```
Datei: src/AgentSmith.Application/Commands/Contexts/CommitAndPRContext.cs
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

## Hinweise
- Alle `sealed record` - immutable by design.
- Jeder Context hat `PipelineContext Pipeline` als letzten Parameter.
- Records brauchen die richtigen `using` Statements für Domain-Typen und Config-Typen.
- Kein Verhalten in Contexts - nur Daten.
- Ein Typ pro Datei.
