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
