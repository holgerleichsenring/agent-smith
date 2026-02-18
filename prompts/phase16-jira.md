# Phase 16 - Step 1: Jira Ticket Provider

## Goal
Implement `ITicketProvider` for Jira Cloud using REST API v3.
No SDK dependency - pure HttpClient with JSON.

---

## File
```
File: src/AgentSmith.Infrastructure/Providers/Tickets/JiraTicketProvider.cs
```
Project: `AgentSmith.Infrastructure`

## Constructor Parameters
- `string baseUrl` - Jira instance URL, e.g. `https://mycompany.atlassian.net`
- `string email` - Jira user email (for Basic Auth)
- `string apiToken` - Jira API token
- `HttpClient httpClient` - injected for testability

## Authentication
Jira Cloud uses Basic Auth with email:apiToken encoded as Base64.
```
Authorization: Basic {base64(email:apiToken)}
```

## Interface Implementation

### ProviderType
Returns `"Jira"`

### GetTicketAsync(TicketId ticketId, CancellationToken ct)
1. `GET /rest/api/3/issue/{ticketId.Value}?fields=summary,description,status,customfield_*`
2. Parse JSON response
3. Map to `Ticket`:
   - `Id` = ticketId
   - `Title` = `fields.summary`
   - `Description` = Convert ADF (Atlassian Document Format) to plain text, or use `fields.description.content[].content[].text`
   - `AcceptanceCriteria` = Check common custom field names or null
   - `Status` = `fields.status.name`
   - `Source` = "Jira"
4. HTTP 404 -> throw `TicketNotFoundException`

### UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken ct)
1. `POST /rest/api/3/issue/{ticketId.Value}/comment`
2. Body: `{ "body": { "type": "doc", "version": 1, "content": [{ "type": "paragraph", "content": [{ "type": "text", "text": "{comment}" }] }] } }`
3. ADF format required for Jira Cloud v3

### CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken ct)
1. First post the resolution as a comment (reuse UpdateStatusAsync)
2. Get available transitions: `GET /rest/api/3/issue/{ticketId.Value}/transitions`
3. Find transition with name containing "Done" or "Closed" (case-insensitive)
4. Execute transition: `POST /rest/api/3/issue/{ticketId.Value}/transitions`
5. Body: `{ "transition": { "id": "{transitionId}" } }`
6. If no matching transition found, log warning but don't fail

## ADF (Atlassian Document Format) Handling
Jira v3 uses ADF for rich text. For reading descriptions, extract plain text recursively
from the `content` tree. For writing comments, wrap plain text in minimal ADF structure.

Simple helper method:
```csharp
private static string ExtractTextFromAdf(JsonElement? adfNode)
```
Recursively walks `content` arrays, concatenates all `text` nodes.

## Environment Variables
- `JIRA_URL` - Base URL
- `JIRA_EMAIL` - User email
- `JIRA_TOKEN` - API token

## Factory Integration
```csharp
// In TicketProviderFactory
private JiraTicketProvider CreateJira(TicketConfig config)
{
    var url = config.Url ?? secrets.GetRequired("JIRA_URL");
    var email = secrets.GetRequired("JIRA_EMAIL");
    var token = secrets.GetRequired("JIRA_TOKEN");
    return new JiraTicketProvider(url, email, token, new HttpClient());
}
```

## Notes
- No Atlassian SDK needed - REST API is straightforward
- ADF parsing only needs plain text extraction, not full rendering
- `sealed class`, all log messages in English
- Transition names vary by Jira workflow - search flexibly
