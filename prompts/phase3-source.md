# Phase 3 - Schritt 3: Source Providers

## Ziel
Echte Implementierungen für Git-Operationen (Checkout, Commit, Push, PR erstellen).
Projekt: `AgentSmith.Infrastructure/Providers/Source/`

---

## LocalSourceProvider
```
Datei: src/AgentSmith.Infrastructure/Providers/Source/LocalSourceProvider.cs
```

**NuGet:** `LibGit2Sharp`

Für lokale Repositories die bereits auf Disk liegen.

**Constructor:**
- `string basePath` (aus Config `source.path`)

**CheckoutAsync:**
1. Öffne Repository mit `new LibGit2Sharp.Repository(basePath)`
2. Erstelle Branch: `repo.CreateBranch(branch.Value)`
3. Checkout: `Commands.Checkout(repo, branch)`
4. Returniere Domain `Repository(basePath, branch, remote.Url)`

**CommitAndPushAsync:**
1. Stage all changes: `Commands.Stage(repo, "*")`
2. Commit: `repo.Commit(message, signature, signature)`
3. Push: `repo.Network.Push(remote, refspec)`

**CreatePullRequestAsync:**
- Für Local Provider: Nur loggen, kein PR möglich
- Return: `"Local repository - no PR created, branch pushed: {branch}"`

**Hinweise:**
- `LibGit2Sharp.Signature` braucht Name + Email → aus Git Config oder Default
- Push braucht Credentials → SSH Key oder Token
- Fehler bei nicht-existierendem Pfad → `ProviderException`

---

## GitHubSourceProvider
```
Datei: src/AgentSmith.Infrastructure/Providers/Source/GitHubSourceProvider.cs
```

**NuGet:** `Octokit` + `LibGit2Sharp`

Kombiniert: LibGit2Sharp für Git-Ops, Octokit für PR-Erstellung.

**Constructor:**
- `string owner`, `string repo` (aus URL extrahiert)
- `string token`
- `string cloneUrl`

**CheckoutAsync:**
1. Clone falls nicht vorhanden: `LibGit2Sharp.Repository.Clone(cloneUrl, localPath)`
2. Erstelle Branch + Checkout (wie LocalSourceProvider)
3. Returniere Domain `Repository(localPath, branch, cloneUrl)`

**CommitAndPushAsync:**
1. Stage + Commit (wie LocalSourceProvider)
2. Push mit Token-Credentials: `UsernamePasswordCredentials`

**CreatePullRequestAsync:**
1. Erstelle `GitHubClient` mit Token
2. `client.PullRequest.Create(owner, repo, new NewPullRequest(title, branch, "main"))`
3. Return: PR URL (`pullRequest.HtmlUrl`)

**Hinweise:**
- Clone-Ziel: Temp-Verzeichnis unter `/tmp/agentsmith/{owner}/{repo}`
- PR wird immer gegen `main` erstellt (konfigurierbar in Zukunft)
- Owner/Repo aus URL extrahieren (gleiche Logik wie GitHubTicketProvider)

---

## Handler Updates

**CheckoutSourceHandler:** Stub → echte Implementierung
```csharp
var provider = factory.Create(context.Config);
var repo = await provider.CheckoutAsync(context.Branch, cancellationToken);
context.Pipeline.Set(ContextKeys.Repository, repo);
```

**CommitAndPRHandler:** Stub → echte Implementierung
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
- `CheckoutAsync_ValidRepo_CreatesBranch` (echtes temp Git Repo)
- `CommitAndPushAsync_WithChanges_Commits` (echtes temp Git Repo)

**GitHubSourceProviderTests:**
- `CreatePullRequestAsync_ValidInput_ReturnsPrUrl` (gemockter Octokit Client)
