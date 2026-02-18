# Phase 15: Azure Repos Source Provider - Implementation Plan

## Goal
Add Azure Repos as a source provider so Agent Smith can clone repositories, create branches,
commit, push, and create pull requests on Azure DevOps.

---

## Prerequisite
- Phase 10-14 completed (all passing)
- `Microsoft.TeamFoundationServer.Client` already in Infrastructure.csproj

## Steps

### Step 1: Azure Repos Source Provider
See: `prompts/phase15-azure-repos.md`

Implement `AzureReposSourceProvider` using LibGit2Sharp for git operations
and Azure DevOps REST API (Microsoft.TeamFoundationServer.Client) for pull requests.

### Step 2: Factory Integration
Update `SourceProviderFactory` to create `AzureReposSourceProvider` for type "azurerepos".

### Step 3: Tests
- Factory test for "azurerepos" type
- Unit tests for URL parsing and provider creation

### Step 4: Verify
```bash
dotnet build
dotnet test
```

---

## Dependencies

```
Step 1 (Provider Implementation)
    └── Step 2 (Factory Integration)
         └── Step 3 (Tests)
              └── Step 4 (Verify)
```

---

## NuGet Packages

No new packages needed. `Microsoft.TeamFoundationServer.Client` and `LibGit2Sharp` are already referenced.

---

## Definition of Done
- [ ] `AzureReposSourceProvider` implements `ISourceProvider`
- [ ] Clone via HTTPS + PAT works
- [ ] Branch creation and checkout works
- [ ] Commit and push works
- [ ] Pull request creation via Azure DevOps API works
- [ ] `SourceProviderFactory` handles "azurerepos" type
- [ ] Tests pass
- [ ] `dotnet build` + `dotnet test` clean
