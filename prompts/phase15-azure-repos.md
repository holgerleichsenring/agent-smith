# Phase 15 - Step 1: Azure Repos Source Provider

## Goal
Implement `ISourceProvider` for Azure DevOps repositories.
Uses LibGit2Sharp for clone/branch/commit/push (same as GitHub provider),
Azure DevOps REST API for pull request creation.

---

## File
```
File: src/AgentSmith.Infrastructure/Providers/Source/AzureReposSourceProvider.cs
```
Project: `AgentSmith.Infrastructure`

## Constructor Parameters
- `string organizationUrl` - e.g. `https://dev.azure.com/myorg`
- `string project` - Azure DevOps project name
- `string repoName` - Repository name
- `string personalAccessToken` - PAT for authentication
- `ILogger<AzureReposSourceProvider> logger`

## Interface Implementation

### ProviderType
Returns `"AzureRepos"`

### CheckoutAsync(BranchName branch, CancellationToken ct)
1. Build clone URL: `https://{pat}@dev.azure.com/{org}/{project}/_git/{repoName}`
2. Clone to temp path if not already cloned (same pattern as GitHubSourceProvider)
3. Create branch if it doesn't exist
4. Checkout branch
5. Return `Repository(localPath, branch, cloneUrl)`

### CommitAndPushAsync(Repository repository, string message, CancellationToken ct)
1. Stage all changes via LibGit2Sharp
2. Commit with signature
3. Push to remote with explicit refspec (same pattern as GitHubSourceProvider)

### CreatePullRequestAsync(Repository repository, string title, string description, CancellationToken ct)
1. Use `GitHttpClient` from `Microsoft.TeamFoundation.SourceControl.WebApi`
2. Create `GitPullRequest` object with:
   - `SourceRefName = $"refs/heads/{repository.CurrentBranch.Value}"`
   - `TargetRefName = "refs/heads/main"`
   - `Title = title`
   - `Description = description`
3. Call `client.CreatePullRequestAsync(pullRequest, project, repoName, ct)`
4. Return the PR URL: `{organizationUrl}/{project}/_git/{repoName}/pullrequest/{pr.PullRequestId}`

## Credential Handling
Use `UsernamePasswordCredentials` with PAT as password (same as GitHub).
For Azure DevOps HTTPS clone URLs, embed the PAT in the URL or use CredentialsHandler.

## Local Path
```
{TempPath}/agentsmith/{org}/{project}/{repoName}
```

## URL Parsing
Parse Azure DevOps repo URLs in these formats:
- `https://dev.azure.com/{org}/{project}/_git/{repo}`
- `https://{org}.visualstudio.com/{project}/_git/{repo}` (legacy)

## Notes
- Reuse the same git operations pattern from `GitHubSourceProvider`
- Consider extracting shared git operations to a helper if duplication is significant
- `sealed class` with primary constructor
- All log messages in English
