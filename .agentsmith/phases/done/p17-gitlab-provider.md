# Phase 17: GitLab Provider (Source + Tickets) - Implementation Plan

## Goal
Add GitLab as both a source and ticket provider. Agent Smith can clone GitLab repos,
create merge requests, read issues, post comments, and close issues.

---

## Prerequisite
- Phase 10-14 completed

## Steps

### Step 1: GitLab Ticket Provider
See: `prompts/phase17-gitlab-tickets.md`

Implement `GitLabTicketProvider` using GitLab REST API v4 via HttpClient.

### Step 2: GitLab Source Provider
See: `prompts/phase17-gitlab-source.md`

Implement `GitLabSourceProvider` using LibGit2Sharp for git ops,
GitLab REST API for merge request creation.

### Step 3: Factory Integration
Update both `TicketProviderFactory` and `SourceProviderFactory`.

### Step 4: Tests + Verify
```bash
dotnet build
dotnet test
```

---

## NuGet Packages

No new packages needed. Use `HttpClient` for GitLab REST API, `LibGit2Sharp` for git.

---

## Definition of Done
- [ ] `GitLabTicketProvider` implements `ITicketProvider` (read, comment, close)
- [ ] `GitLabSourceProvider` implements `ISourceProvider` (clone, branch, commit, push, MR)
- [ ] Both factories handle "gitlab" type
- [ ] Tests pass
- [ ] `dotnet build` + `dotnet test` clean


---

# Phase 17 - Step 2: GitLab Source Provider

## Goal
Implement `ISourceProvider` for GitLab repositories.
LibGit2Sharp for git operations, GitLab REST API for merge request creation.

---

## File
```
File: src/AgentSmith.Infrastructure/Providers/Source/GitLabSourceProvider.cs
```

## Constructor Parameters
- `string baseUrl` - GitLab instance URL
- `string projectPath` - URL-encoded project path
- `string cloneUrl` - HTTPS clone URL
- `string privateToken` - Personal access token
- `HttpClient httpClient` - for REST API calls
- `ILogger<GitLabSourceProvider> logger`

## Interface Implementation

### ProviderType
Returns `"GitLab"`

### CheckoutAsync(BranchName branch, CancellationToken ct)
Same pattern as GitHubSourceProvider:
1. Clone via HTTPS if not already cloned
2. Credential: `UsernamePasswordCredentials("oauth2", privateToken)`
3. Create and checkout branch
4. Return `Repository(localPath, branch, cloneUrl)`

### CommitAndPushAsync(Repository repository, string message, CancellationToken ct)
Same pattern as GitHubSourceProvider:
1. Stage all changes
2. Commit with signature
3. Push with explicit refspec

### CreatePullRequestAsync(Repository repository, string title, string description, CancellationToken ct)
GitLab calls these "Merge Requests":
1. `POST /api/v4/projects/{projectPath}/merge_requests`
2. Body:
```json
{
  "source_branch": "{repository.CurrentBranch.Value}",
  "target_branch": "main",
  "title": "{title}",
  "description": "{description}"
}
```
3. Return the MR URL from response: `web_url`

## Clone URL Construction
- `https://oauth2:{token}@gitlab.com/{projectPath}.git`
- Or for self-hosted: `https://oauth2:{token}@{host}/{projectPath}.git`

## Local Path
```
{TempPath}/agentsmith/gitlab/{projectPath}
```

## URL Parsing
Parse GitLab repo URLs:
- `https://gitlab.com/{group}/{project}`
- `https://gitlab.com/{group}/{subgroup}/{project}`
- Self-hosted: `https://gitlab.example.com/{group}/{project}`

## Notes
- GitLab uses `oauth2` as username for token-based HTTPS authentication
- Merge requests (not pull requests) - different terminology
- `sealed class`, all messages in English


---

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
