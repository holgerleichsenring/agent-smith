# Phase 1 - Step 2: Domain Entities & Value Objects

## Goal
Define all core types of the system. Pure data models without infrastructure dependencies.
Project: `AgentSmith.Domain`

---

## Value Objects (Records)

Immutable, comparable by value, self-validating.

### TicketId
```
File: src/AgentSmith.Domain/ValueObjects/TicketId.cs
```
- `string Value` (not empty, not null)
- Guard clause in constructor
- Implicit conversion from/to string

### ProjectName
```
File: src/AgentSmith.Domain/ValueObjects/ProjectName.cs
```
- `string Value` (not empty, not null)
- Guard clause in constructor

### BranchName
```
File: src/AgentSmith.Domain/ValueObjects/BranchName.cs
```
- `string Value`
- Factory method: `FromTicket(TicketId ticketId, string prefix = "fix")` → e.g. `fix/12345`

### FilePath
```
File: src/AgentSmith.Domain/ValueObjects/FilePath.cs
```
- `string Value` (relative to repo root)
- Validation: no absolute path, no `..`

### CommandResult
```
File: src/AgentSmith.Domain/ValueObjects/CommandResult.cs
```
- `bool Success`
- `string Message`
- `Exception? Exception`
- Static factories: `CommandResult.Ok(message)`, `CommandResult.Fail(message, exception?)`

---

## Entities

Have identity, can change over time.

### Ticket
```
File: src/AgentSmith.Domain/Entities/Ticket.cs
```
- `TicketId Id`
- `string Title`
- `string Description`
- `string? AcceptanceCriteria`
- `string Status`
- `string Source` (e.g. "AzureDevOps", "Jira", "GitHub")

### Repository
```
File: src/AgentSmith.Domain/Entities/Repository.cs
```
- `string LocalPath`
- `BranchName CurrentBranch`
- `string RemoteUrl`

### Plan
```
File: src/AgentSmith.Domain/Entities/Plan.cs
```
- `string Summary`
- `IReadOnlyList<PlanStep> Steps`
- `string RawResponse` (original response from the agent)

### PlanStep
```
File: src/AgentSmith.Domain/Entities/PlanStep.cs
```
- `int Order`
- `string Description`
- `FilePath? TargetFile`
- `string ChangeType` (Create, Modify, Delete)

### CodeChange
```
File: src/AgentSmith.Domain/Entities/CodeChange.cs
```
- `FilePath Path`
- `string Content`
- `string ChangeType` (Create, Modify, Delete)

### CodeAnalysis
```
File: src/AgentSmith.Domain/Entities/CodeAnalysis.cs
```
- `IReadOnlyList<string> FileStructure`
- `IReadOnlyList<string> Dependencies`
- `string? Framework`
- `string? Language`

---

## Exceptions

All inherit from a base exception.

### AgentSmithException
```
File: src/AgentSmith.Domain/Exceptions/AgentSmithException.cs
```
- Base exception for all domain-specific errors
- Constructor: `(string message)`, `(string message, Exception innerException)`

### TicketNotFoundException
```
File: src/AgentSmith.Domain/Exceptions/TicketNotFoundException.cs
```
- Inherits from `AgentSmithException`
- Constructor: `(TicketId ticketId)`
- Message: `$"Ticket '{ticketId.Value}' not found."`

### ConfigurationException
```
File: src/AgentSmith.Domain/Exceptions/ConfigurationException.cs
```
- Inherits from `AgentSmithException`
- Constructor: `(string message)`

### ProviderException
```
File: src/AgentSmith.Domain/Exceptions/ProviderException.cs
```
- Inherits from `AgentSmithException`
- `string ProviderType` Property
- Constructor: `(string providerType, string message, Exception? innerException = null)`

---

## Directory Structure

```
src/AgentSmith.Domain/
├── Entities/
│   ├── Ticket.cs
│   ├── Repository.cs
│   ├── Plan.cs
│   ├── PlanStep.cs
│   ├── CodeChange.cs
│   └── CodeAnalysis.cs
├── ValueObjects/
│   ├── TicketId.cs
│   ├── ProjectName.cs
│   ├── BranchName.cs
│   ├── FilePath.cs
│   └── CommandResult.cs
└── Exceptions/
    ├── AgentSmithException.cs
    ├── TicketNotFoundException.cs
    ├── ConfigurationException.cs
    └── ProviderException.cs
```

## Notes

- Implement all Value Objects as `record`.
- Entities as `sealed class` (no inheritance needed for now).
- Guard clauses with `ArgumentException.ThrowIfNullOrWhiteSpace()` (.NET 8).
- No using of infrastructure namespaces.
- No NuGet package needed - pure C# types.
