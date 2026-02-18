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
