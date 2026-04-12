# Phase 70: Decision Log — Phase/Run Context

## Goal

`decisions.md` groups by category (`## Architecture`, `## Tooling`) but the
real lookup key is "which phase or run made this decision". When a phase is
implemented (by pipeline or by IDE buddy), the decisions need to land under
`## p67: API Scan Compression & ZAP Fix` not `## Architecture`.

Two changes:

1. **Code** — `IDecisionLogger.LogAsync` gets an optional `sourceLabel` that
   becomes the section header. Callers pass the ticket/run context.
   `FileDecisionLogger` uses it instead of the category enum as section header.

2. **Process** — `.claude/prompt.md` gets a workflow step: log design decisions
   to `.agentsmith/decisions.md` when implementing a phase.

## Design

### IDecisionLogger — add sourceLabel

```csharp
public interface IDecisionLogger
{
    Task LogAsync(string? repoPath, DecisionCategory category, string decision,
                  CancellationToken cancellationToken = default,
                  string? sourceLabel = null);  // e.g. "p67: API Scan Compression"
}
```

### FileDecisionLogger — section header logic

```
if sourceLabel is set:
    section = "## {sourceLabel}"          → "## p67: API Scan Compression"
else:
    section = "## {category}"             → "## Architecture" (backward compat)
```

Category is preserved as inline tag: `- [Architecture] Chose X because Y`

### Callers — pass ticket/run as sourceLabel

**GeneratePlanHandler**: Extract ticket ID from pipeline, format as
`"r{RunNumber}: {TicketSummary}"` or just `"#{TicketId}"`.

**AgenticExecuteHandler**: Same — get ticket from pipeline context.

### .claude/prompt.md — workflow step

Between step 6 (tests) and step 7 (update context.yaml):

```
6b. **Log decisions** — append design decisions to `.agentsmith/decisions.md`
    under `## p{NN}: Phase Title`. Each decision: what was chosen, what
    alternatives were considered, and why.
```

### Seed decisions.md

Create `.agentsmith/decisions.md` with decisions from p66-p68 as reference.

## Files to Modify

- `src/AgentSmith.Contracts/Decisions/IDecisionLogger.cs` — add sourceLabel param
- `src/AgentSmith.Infrastructure.Core/Services/FileDecisionLogger.cs` — use sourceLabel as section header
- `src/AgentSmith.Infrastructure.Core/Services/InMemoryDecisionLogger.cs` — match interface
- `src/AgentSmith.Application/Services/Handlers/GeneratePlanHandler.cs` — pass ticket as sourceLabel
- `src/AgentSmith.Application/Services/Handlers/AgenticExecuteHandler.cs` — pass ticket as sourceLabel
- `.claude/prompt.md` — add workflow step
- `.agentsmith/decisions.md` — create with p66-p68 seed data

## Definition of Done

- [ ] IDecisionLogger has optional sourceLabel parameter
- [ ] FileDecisionLogger uses sourceLabel as section header when present
- [ ] GeneratePlanHandler and AgenticExecuteHandler pass ticket context
- [ ] .claude/prompt.md includes decision logging step
- [ ] decisions.md created with p66-p68 decisions
- [ ] InMemoryDecisionLogger matches updated interface
- [ ] `dotnet build` passes
- [ ] `dotnet test` passes
- [ ] Existing fix-bug pipeline still writes decisions (backward compat)
