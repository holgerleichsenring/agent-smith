# Phase 17 - Step 1: GitLab Ticket Provider

## Goal
Implement `ITicketProvider` for GitLab using REST API v4.

---

## File
```
File: src/AgentSmith.Infrastructure/Providers/Tickets/GitLabTicketProvider.cs
```

## Constructor Parameters
- `string baseUrl` - GitLab instance URL, e.g. `https://gitlab.com` or self-hosted
- `string projectPath` - URL-encoded project path, e.g. `mygroup%2Fmyproject`
- `string privateToken` - GitLab personal access token
- `HttpClient httpClient`

## Authentication
```
PRIVATE-TOKEN: {privateToken}
```
Header on every request.

## Interface Implementation

### ProviderType
Returns `"GitLab"`

### GetTicketAsync(TicketId ticketId, CancellationToken ct)
1. `GET /api/v4/projects/{projectPath}/issues/{ticketId.Value}`
2. Map JSON to `Ticket`:
   - `Title` = `title`
   - `Description` = `description` (plain markdown, no ADF conversion needed)
   - `AcceptanceCriteria` = null (GitLab has no dedicated field)
   - `Status` = `state` ("opened", "closed")
   - `Source` = "GitLab"
3. HTTP 404 -> throw `TicketNotFoundException`

### UpdateStatusAsync(TicketId ticketId, string comment, CancellationToken ct)
1. `POST /api/v4/projects/{projectPath}/issues/{ticketId.Value}/notes`
2. Body: `{ "body": "{comment}" }`

### CloseTicketAsync(TicketId ticketId, string resolution, CancellationToken ct)
1. Post resolution as comment (reuse UpdateStatusAsync)
2. `PUT /api/v4/projects/{projectPath}/issues/{ticketId.Value}`
3. Body: `{ "state_event": "close" }`

## Environment Variables
- `GITLAB_URL` - Base URL (defaults to `https://gitlab.com`)
- `GITLAB_TOKEN` - Personal access token
- `GITLAB_PROJECT` - Project path (e.g. `mygroup/myproject`)

## Notes
- GitLab REST API is straightforward JSON, no special format needed
- Description is plain markdown (unlike Jira's ADF)
- `sealed class`, all messages in English
