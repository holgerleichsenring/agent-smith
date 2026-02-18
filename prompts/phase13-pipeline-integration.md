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
