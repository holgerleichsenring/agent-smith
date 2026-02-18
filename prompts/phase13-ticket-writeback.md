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
