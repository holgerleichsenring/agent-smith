# Phase 13: Ticket Writeback & Status - Implementation Plan

## Goal
Agent Smith documents its work in the ticket: status updates while working,
result summary at the end, link to the PR. On failure, posts the error to the ticket.

---

## Prerequisite
- Phase 10 completed (Container Production-Ready)

## Steps

### Step 1: ITicketProvider Extensions
See: `prompts/phase13-ticket-writeback.md`

Add `UpdateStatusAsync` and `CloseTicketAsync` to ITicketProvider with default
implementations. Implement in GitHub and Azure DevOps providers.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Pipeline & Handler Integration
See: `prompts/phase13-pipeline-integration.md`

PipelineExecutor posts "working on it" at start and error on failure.
CommitAndPRHandler posts summary with PR link and closes the ticket.
Project: `AgentSmith.Application/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (ITicketProvider Extensions)
    └── Step 2 (Pipeline + Handler Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 13)

No new packages required.

---

## Key Decisions

1. Default interface implementations (`=> Task.CompletedTask`) for backward compatibility
2. Ticket operations are fire-and-forget: failures logged as warnings, never block the pipeline
3. CommitAndPRHandler gets ITicketProviderFactory dependency for posting summary
4. PipelineExecutor gets ITicketProviderFactory for status updates and error reporting
5. Ticket comment includes: PR link, change list, status

---

## Ticket Comment Format
```markdown
## Agent Smith - Completed

**PR:** #42

### Changes
- [Created] `README.md`
- [Modified] `src/Program.cs`

This ticket was automatically processed by Agent Smith.
```

---

## Definition of Done (Phase 13)
- [ ] `ITicketProvider.UpdateStatusAsync()` with default implementation
- [ ] `ITicketProvider.CloseTicketAsync()` with default implementation
- [ ] GitHubTicketProvider: issue comment + close
- [ ] AzureDevOpsTicketProvider: work item history + state transition
- [ ] CommitAndPRContext includes TicketConfig
- [ ] CommitAndPRHandler posts summary and closes ticket
- [ ] PipelineExecutor posts "working on it" at pipeline start
- [ ] PipelineExecutor posts error details on pipeline failure
- [ ] Tests for CommitAndPRHandler (close, failure tolerance, summary content)
- [ ] Tests for PipelineExecutor (status posting, failure tolerance)
- [ ] All existing tests green


---

# Phase 13: Pipeline & Handler Integration - Implementation Details

## Overview
Pipeline-level status updates and error reporting via ITicketProviderFactory.
CommitAndPRHandler posts result summary and closes the ticket.

---

## PipelineExecutor Changes

### New Dependency
Add `ITicketProviderFactory ticketFactory` to constructor.

### Status at Pipeline Start
```csharp
await PostTicketStatusAsync(projectConfig, context,
    "Agent Smith is working on this issue...", cancellationToken);
```

### Error Reporting on Failure
```csharp
await PostTicketStatusAsync(projectConfig, context,
    $"## Agent Smith - Failed\n\n**Step:** {commandName}\n**Error:** {result.Message}",
    cancellationToken);
```

### PostTicketStatusAsync Helper
```csharp
private async Task PostTicketStatusAsync(...)
{
    try
    {
        if (!context.TryGet<TicketId>(ContextKeys.TicketId, out var ticketId))
            return;
        var ticketProvider = ticketFactory.Create(projectConfig.Tickets);
        await ticketProvider.UpdateStatusAsync(ticketId, message, cancellationToken);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Failed to post status update to ticket");
    }
}
```

All ticket operations wrapped in try/catch - never blocks the pipeline.

---

## CommitAndPRHandler Changes

### New Dependency
Add `ITicketProviderFactory ticketFactory` to constructor.

### CommitAndPRContext
Add `TicketConfig TicketConfig` parameter to the record.
CommandContextFactory passes `project.Tickets` when creating the context.

### After PR Creation
Call `CloseTicketWithSummaryAsync` which:
1. Creates a markdown summary with PR link and change list
2. Calls `ticketProvider.CloseTicketAsync(ticketId, summary)`
3. On failure: logs warning, PR was still created successfully

---

## Test Updates
- PipelineExecutorTests: add `ITicketProviderFactory` mock to constructor
- New CommitAndPRHandlerTests: verify close, failure tolerance, summary content


---

# Phase 13: Ticket Writeback - Implementation Details

## Overview
Extend ITicketProvider with methods for posting comments and closing tickets.
Each provider implements these for its platform (GitHub Issues, Azure DevOps Work Items).

---

## ITicketProvider Extensions (Contracts Layer)

```csharp
public interface ITicketProvider
{
    // ... existing GetTicketAsync ...

    Task UpdateStatusAsync(TicketId ticketId, string comment, 
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    Task CloseTicketAsync(TicketId ticketId, string resolution,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

Default implementations return `Task.CompletedTask` - existing providers work
without changes. Only providers that implement these methods will post updates.

---

## GitHubTicketProvider (Infrastructure Layer)

### UpdateStatusAsync
```csharp
await _client.Issue.Comment.Create(_owner, _repo, issueNumber, comment);
```

### CloseTicketAsync
```csharp
await _client.Issue.Comment.Create(_owner, _repo, issueNumber, resolution);
await _client.Issue.Update(_owner, _repo, issueNumber,
    new IssueUpdate { State = ItemState.Closed });
```

---

## AzureDevOpsTicketProvider (Infrastructure Layer)

### UpdateStatusAsync
Uses `JsonPatchDocument` to update `System.History` field (adds comment to work item).

### CloseTicketAsync
Patches both `System.History` (resolution comment) and `System.State` to "Closed".

---

## Error Handling
Both providers silently return if the ticket ID cannot be parsed to int.
Callers wrap all ticket operations in try/catch to prevent blocking the pipeline.
