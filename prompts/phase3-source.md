# Phase 3 - Step 3: Source Providers

## Goal
Real implementations for Git operations (checkout, commit, push, create PR).
Project: `AgentSmith.Infrastructure/Providers/Source/`

---

## LocalSourceProvider
```
File: src/AgentSmith.Infrastructure/Providers/Source/LocalSourceProvider.cs
```

**NuGet:** `LibGit2Sharp`

For local repositories that already exist on disk.

**Constructor:**
- `string basePath` (from config `source.path`)

**CheckoutAsync:**
1. Open repository with `new LibGit2Sharp.Repository(basePath)`
2. Create branch: `repo.CreateBranch(branch.Value)`
3. Checkout: `Commands.Checkout(repo, branch)`
4. Return Domain `Repository(basePath, branch, remote.Url)`

**CommitAndPushAsync:**
1. Stage all changes: `Commands.Stage(repo, "*")`
2. Commit: `repo.Commit(message, signature, signature)`
3. Push: `repo.Network.Push(remote, refspec)`

**CreatePullRequestAsync:**
- For Local Provider: Only log, no PR possible
- Return: `"Local repository - no PR created, branch pushed: {branch}"`

**Notes:**
- `LibGit2Sharp.Signature` requires name + email → from Git config or default
- Push requires credentials → SSH key or token
- Error on non-existent path → `ProviderException`

---

## GitHubSourceProvider
```
File: src/AgentSmith.Infrastructure/Providers/Source/GitHubSourceProvider.cs
```

**NuGet:** `Octokit` + `LibGit2Sharp`

Combined: LibGit2Sharp for Git ops, Octokit for PR creation.

**Constructor:**
- `string owner`, `string repo` (extracted from URL)
- `string token`
- `string cloneUrl`

**CheckoutAsync:**
1. Clone if not present: `LibGit2Sharp.Repository.Clone(cloneUrl, localPath)`
2. Create branch + checkout (same as LocalSourceProvider)
3. Return Domain `Repository(localPath, branch, cloneUrl)`

**CommitAndPushAsync:**
1. Stage + commit (same as LocalSourceProvider)
2. Push with token credentials: `UsernamePasswordCredentials`

**CreatePullRequestAsync:**
1. Create `GitHubClient` with token
2. `client.PullRequest.Create(owner, repo, new NewPullRequest(title, branch, "main"))`
3. Return: PR URL (`pullRequest.HtmlUrl`)

**Notes:**
- Clone target: Temp directory under `/tmp/agentsmith/{owner}/{repo}`
- PR is always created against `main` (configurable in the future)
- Extract owner/repo from URL (same logic as GitHubTicketProvider)

---

## Handler Updates

**CheckoutSourceHandler:** Stub → real implementation
```csharp
var provider = factory.Create(context.Config);
var repo = await provider.CheckoutAsync(context.Branch, cancellationToken);
context.Pipeline.Set(ContextKeys.Repository, repo);
```

**CommitAndPRHandler:** Stub → real implementation
```csharp
var provider = factory.Create(context.SourceConfig);
var message = $"fix: {context.Ticket.Title} (#{context.Ticket.Id})";
await provider.CommitAndPushAsync(context.Repository, message, cancellationToken);
var prUrl = await provider.CreatePullRequestAsync(
    context.Repository, context.Ticket.Title, context.Ticket.Description, cancellationToken);
context.Pipeline.Set(ContextKeys.PullRequestUrl, prUrl);
```

---

## Tests

**LocalSourceProviderTests:**
- `CheckoutAsync_ValidRepo_CreatesBranch` (real temp Git repo)
- `CommitAndPushAsync_WithChanges_Commits` (real temp Git repo)

**GitHubSourceProviderTests:**
- `CreatePullRequestAsync_ValidInput_ReturnsPrUrl` (mocked Octokit client)
