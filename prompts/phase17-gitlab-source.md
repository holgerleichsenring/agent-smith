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
