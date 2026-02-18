# Phase 3 - Step 2: Ticket Providers

## Goal
Real implementations for fetching tickets from external systems.
Project: `AgentSmith.Infrastructure/Providers/Tickets/`

---

## AzureDevOpsTicketProvider
```
File: src/AgentSmith.Infrastructure/Providers/Tickets/AzureDevOpsTicketProvider.cs
```

**NuGet:** `Microsoft.TeamFoundationServer.Client`

**Constructor:**
- `string organizationUrl` (e.g. `https://dev.azure.com/myorg`)
- `string project`
- `string personalAccessToken`

**GetTicketAsync:**
1. Create `VssConnection` with PAT
2. Get `WorkItemTrackingHttpClient`
3. `GetWorkItemAsync(int.Parse(ticketId))`
4. Map `WorkItem` → Domain `Ticket`
   - Title: `workItem.Fields["System.Title"]`
   - Description: `workItem.Fields["System.Description"]`
   - AcceptanceCriteria: `workItem.Fields["Microsoft.VSTS.Common.AcceptanceCriteria"]`
   - Status: `workItem.Fields["System.State"]`
   - Source: `"AzureDevOps"`
5. Not found → `TicketNotFoundException`

**Notes:**
- `VssBasicCredential` for PAT authentication
- WorkItem Fields are dictionary-based - use defensive access with `TryGetValue`
- Organization URL from config: `https://dev.azure.com/{organization}`

---

## GitHubTicketProvider
```
File: src/AgentSmith.Infrastructure/Providers/Tickets/GitHubTicketProvider.cs
```

**NuGet:** `Octokit`

**Constructor:**
- `string owner` (extracted from URL)
- `string repo` (extracted from URL)
- `string token`

**GetTicketAsync:**
1. Create `GitHubClient` with `Credentials(token)`
2. `client.Issue.Get(owner, repo, int.Parse(ticketId))`
3. Map `Issue` → Domain `Ticket`
   - Title: `issue.Title`
   - Description: `issue.Body`
   - AcceptanceCriteria: `null` (GitHub Issues don't have this)
   - Status: `issue.State.StringValue`
   - Source: `"GitHub"`
4. Not found → `TicketNotFoundException`

**Notes:**
- Extract owner/repo from the source URL: `https://github.com/{owner}/{repo}`
- `ProductHeaderValue("AgentSmith")` for API calls
- Be mindful of rate limiting (GitHub API limit: 5000/hour with token)

---

## FetchTicketHandler Update

Replace the stub in `FetchTicketHandler` with the real implementation:

```csharp
public async Task<CommandResult> ExecuteAsync(
    FetchTicketContext context, CancellationToken cancellationToken = default)
{
    logger.LogInformation("Fetching ticket {TicketId}...", context.TicketId);

    var provider = factory.Create(context.Config);
    var ticket = await provider.GetTicketAsync(context.TicketId, cancellationToken);

    context.Pipeline.Set(ContextKeys.Ticket, ticket);
    return CommandResult.Ok($"Ticket '{ticket.Title}' fetched from {provider.ProviderType}");
}
```

---

## Tests

**AzureDevOpsTicketProviderTests:**
- `GetTicketAsync_ValidId_ReturnsTicket` (mocked HTTP client)
- `GetTicketAsync_NotFound_ThrowsTicketNotFoundException`

**GitHubTicketProviderTests:**
- `GetTicketAsync_ValidIssue_ReturnsTicket` (mocked Octokit client)
- `GetTicketAsync_NotFound_ThrowsTicketNotFoundException`
