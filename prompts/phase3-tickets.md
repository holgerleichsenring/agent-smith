# Phase 3 - Schritt 2: Ticket Providers

## Ziel
Echte Implementierungen für das Holen von Tickets aus externen Systemen.
Projekt: `AgentSmith.Infrastructure/Providers/Tickets/`

---

## AzureDevOpsTicketProvider
```
Datei: src/AgentSmith.Infrastructure/Providers/Tickets/AzureDevOpsTicketProvider.cs
```

**NuGet:** `Microsoft.TeamFoundationServer.Client`

**Constructor:**
- `string organizationUrl` (z.B. `https://dev.azure.com/myorg`)
- `string project`
- `string personalAccessToken`

**GetTicketAsync:**
1. Erstelle `VssConnection` mit PAT
2. Hole `WorkItemTrackingHttpClient`
3. `GetWorkItemAsync(int.Parse(ticketId))`
4. Mappe `WorkItem` → Domain `Ticket`
   - Title: `workItem.Fields["System.Title"]`
   - Description: `workItem.Fields["System.Description"]`
   - AcceptanceCriteria: `workItem.Fields["Microsoft.VSTS.Common.AcceptanceCriteria"]`
   - Status: `workItem.Fields["System.State"]`
   - Source: `"AzureDevOps"`
5. Nicht gefunden → `TicketNotFoundException`

**Hinweise:**
- `VssBasicCredential` für PAT-Authentifizierung
- WorkItem Fields sind Dictionary-basiert - defensive Zugriffe mit `TryGetValue`
- Organization URL aus Config: `https://dev.azure.com/{organization}`

---

## GitHubTicketProvider
```
Datei: src/AgentSmith.Infrastructure/Providers/Tickets/GitHubTicketProvider.cs
```

**NuGet:** `Octokit`

**Constructor:**
- `string owner` (aus URL extrahiert)
- `string repo` (aus URL extrahiert)
- `string token`

**GetTicketAsync:**
1. Erstelle `GitHubClient` mit `Credentials(token)`
2. `client.Issue.Get(owner, repo, int.Parse(ticketId))`
3. Mappe `Issue` → Domain `Ticket`
   - Title: `issue.Title`
   - Description: `issue.Body`
   - AcceptanceCriteria: `null` (GitHub Issues haben das nicht)
   - Status: `issue.State.StringValue`
   - Source: `"GitHub"`
4. Nicht gefunden → `TicketNotFoundException`

**Hinweise:**
- Owner/Repo aus der Source-URL extrahieren: `https://github.com/{owner}/{repo}`
- `ProductHeaderValue("AgentSmith")` für API-Aufrufe
- Rate Limiting beachten (GitHub API Limit: 5000/Stunde mit Token)

---

## FetchTicketHandler Update

Den Stub in `FetchTicketHandler` durch die echte Implementierung ersetzen:

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
- `GetTicketAsync_ValidId_ReturnsTicket` (gemockter HTTP Client)
- `GetTicketAsync_NotFound_ThrowsTicketNotFoundException`

**GitHubTicketProviderTests:**
- `GetTicketAsync_ValidIssue_ReturnsTicket` (gemockter Octokit Client)
- `GetTicketAsync_NotFound_ThrowsTicketNotFoundException`
