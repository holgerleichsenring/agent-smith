# Phase 1 - Schritt 2: Domain Entities & Value Objects

## Ziel
Alle Kerntypen des Systems definieren. Reine Datenmodelle ohne Infrastruktur-Abhängigkeiten.
Projekt: `AgentSmith.Domain`

---

## Value Objects (Records)

Immutable, vergleichbar by value, selbst-validierend.

### TicketId
```
Datei: src/AgentSmith.Domain/ValueObjects/TicketId.cs
```
- `string Value` (nicht leer, nicht null)
- Guard Clause im Constructor
- Implicit conversion von/zu string

### ProjectName
```
Datei: src/AgentSmith.Domain/ValueObjects/ProjectName.cs
```
- `string Value` (nicht leer, nicht null)
- Guard Clause im Constructor

### BranchName
```
Datei: src/AgentSmith.Domain/ValueObjects/BranchName.cs
```
- `string Value`
- Factory method: `FromTicket(TicketId ticketId, string prefix = "fix")` → z.B. `fix/12345`

### FilePath
```
Datei: src/AgentSmith.Domain/ValueObjects/FilePath.cs
```
- `string Value` (relativ zum Repo Root)
- Validierung: kein absoluter Pfad, kein `..`

### CommandResult
```
Datei: src/AgentSmith.Domain/ValueObjects/CommandResult.cs
```
- `bool Success`
- `string Message`
- `Exception? Exception`
- Static factories: `CommandResult.Ok(message)`, `CommandResult.Fail(message, exception?)`

---

## Entities

Haben Identität, können sich ändern.

### Ticket
```
Datei: src/AgentSmith.Domain/Entities/Ticket.cs
```
- `TicketId Id`
- `string Title`
- `string Description`
- `string? AcceptanceCriteria`
- `string Status`
- `string Source` (z.B. "AzureDevOps", "Jira", "GitHub")

### Repository
```
Datei: src/AgentSmith.Domain/Entities/Repository.cs
```
- `string LocalPath`
- `BranchName CurrentBranch`
- `string RemoteUrl`

### Plan
```
Datei: src/AgentSmith.Domain/Entities/Plan.cs
```
- `string Summary`
- `IReadOnlyList<PlanStep> Steps`
- `string RawResponse` (Original-Antwort vom Agent)

### PlanStep
```
Datei: src/AgentSmith.Domain/Entities/PlanStep.cs
```
- `int Order`
- `string Description`
- `FilePath? TargetFile`
- `string ChangeType` (Create, Modify, Delete)

### CodeChange
```
Datei: src/AgentSmith.Domain/Entities/CodeChange.cs
```
- `FilePath Path`
- `string Content`
- `string ChangeType` (Create, Modify, Delete)

### CodeAnalysis
```
Datei: src/AgentSmith.Domain/Entities/CodeAnalysis.cs
```
- `IReadOnlyList<string> FileStructure`
- `IReadOnlyList<string> Dependencies`
- `string? Framework`
- `string? Language`

---

## Exceptions

Alle erben von einer Basis-Exception.

### AgentSmithException
```
Datei: src/AgentSmith.Domain/Exceptions/AgentSmithException.cs
```
- Basis-Exception für alle domänenspezifischen Fehler
- Constructor: `(string message)`, `(string message, Exception innerException)`

### TicketNotFoundException
```
Datei: src/AgentSmith.Domain/Exceptions/TicketNotFoundException.cs
```
- Erbt von `AgentSmithException`
- Constructor: `(TicketId ticketId)`
- Message: `$"Ticket '{ticketId.Value}' not found."`

### ConfigurationException
```
Datei: src/AgentSmith.Domain/Exceptions/ConfigurationException.cs
```
- Erbt von `AgentSmithException`
- Constructor: `(string message)`

### ProviderException
```
Datei: src/AgentSmith.Domain/Exceptions/ProviderException.cs
```
- Erbt von `AgentSmithException`
- `string ProviderType` Property
- Constructor: `(string providerType, string message, Exception? innerException = null)`

---

## Verzeichnisstruktur

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

## Hinweise

- Alle Value Objects als `record` implementieren.
- Entities als `sealed class` (vorerst kein Vererbungsbedarf).
- Guard Clauses mit `ArgumentException.ThrowIfNullOrWhiteSpace()` (.NET 8).
- Kein using von Infrastructure-Namespaces.
- Kein NuGet-Package nötig - reine C# Typen.
