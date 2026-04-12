# Phase 40b: Git Provider Extraction (GitHub, AzureDevOps, GitLab, Jira)

## Goal

Extract all ticket and source providers from `AgentSmith.Infrastructure` into
dedicated, independently deployable projects. Each provider references only
`AgentSmith.Contracts` and `AgentSmith.Domain`.

---

## Provider Projects

### AgentSmith.Providers.GitHub

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      Octokit, LibGit2Sharp
```

Contains:
- `GitHubTicketProvider` — ITicketProvider
  - `GetAttachmentRefsAsync`: returns refs for GitHub issue attachments (image URLs in body)
- `GitHubSourceProvider` — ISourceProvider
- `GitHubStorageReader` — IStorageReader
  - `CanHandle`: checks for `github.com` or `githubusercontent.com` host
  - Reads GitHub CDN content with authentication

Config via `IOptions<GitHubProviderOptions>`:
```csharp
public sealed class GitHubProviderOptions
{
    public string Token { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.github.com";
}
```

Registration: `services.AddGitHubProviders(configuration);`

### AgentSmith.Providers.AzureDevOps

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      Microsoft.TeamFoundationServer.Client, LibGit2Sharp
```

Contains:
- `AzureDevOpsTicketProvider` — ITicketProvider
  - `GetAttachmentRefsAsync`: from work item attachments API
- `AzureReposSourceProvider` — ISourceProvider

Registration: `services.AddAzureDevOpsProviders(configuration);`

### AgentSmith.Providers.Jira

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      (none — uses HttpClient)
```

Contains:
- `JiraTicketProvider` — ITicketProvider
  - `GetAttachmentRefsAsync`: from Jira REST `/issue/{id}?fields=attachment`

Registration: `services.AddJiraProvider(configuration);`

### AgentSmith.Providers.GitLab

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      LibGit2Sharp
```

Contains:
- `GitLabTicketProvider` — ITicketProvider
- `GitLabSourceProvider` — ISourceProvider

Registration: `services.AddGitLabProviders(configuration);`

---

## Migration Steps

For each provider:
1. Create new project with `AgentSmith.Providers.{Name}.csproj`
2. Move existing provider class from Infrastructure
3. Add `IOptions<T>` config instead of factory-passed config
4. Add `ServiceCollectionExtensions` with `Add{Name}Providers()`
5. Move existing tests to provider-specific test project (or keep in main tests with reference)
6. Remove from Infrastructure
7. Update Host to reference new project

---

## Files to Create (per provider)

```
src/providers/AgentSmith.Providers.{Name}/
  AgentSmith.Providers.{Name}.csproj
  {Name}TicketProvider.cs
  {Name}SourceProvider.cs  (if applicable)
  {Name}ProviderOptions.cs
  Extensions/ServiceCollectionExtensions.cs
```

## Files to Delete from Infrastructure

- All provider classes that have been moved

## Files to Modify

- `AgentSmith.sln` — add 4 provider projects
- `src/AgentSmith.Cli/AgentSmith.Cli.csproj` — reference provider projects
- `src/AgentSmith.Cli/Program.cs` — call `Add{Name}Providers()` methods

---

## Definition of Done

- [ ] All 4 provider projects created and building
- [ ] Zero provider classes remain in Infrastructure for these providers
- [ ] All existing tests pass
- [ ] Each provider has its own `Add{Name}Providers()` extension
- [ ] Host only references providers it needs

---

## Estimation

~100 lines new code per provider (DI wiring, options class).
Most code is moved, not rewritten. ~400 lines total.
