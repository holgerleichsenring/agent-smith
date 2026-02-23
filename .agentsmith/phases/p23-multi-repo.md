# Phase 23: Multi-Repo Support

## Goal

Agent Smith can process a ticket that spans multiple repositories. One ticket →
multiple PRs in different repos, coordinated as a single unit of work.

## Problem

Today's `agentsmith.yml` has a 1:1 relationship: one project config → one repo.
A ticket like "Update the shared API contract and all consuming services" requires
changes across multiple repos.

## Requirements

### Step 1: Config Structure Change

Extend `agentsmith.yml` with optional project groups (backward compatible):

```yaml
project-groups:
  platform-update:
    description: "Changes that span API + consumers"
    repos:
      - project: my-api
        role: primary          # changes start here
      - project: service-a
        role: consumer         # receives contract changes
    strategy: sequential       # or: parallel, dependency-order
```

- `role: primary` — main change repo
- `role: consumer` — repos that adapt to primary change
- Individual project definitions remain unchanged

### Step 2: Multi-Repo Pipeline Orchestration

`MultiRepoPipelineExecutor` that:

1. Runs full pipeline on `primary` repo first
2. Takes resulting diff as context for consumer repos
3. Runs pipeline on each `consumer` with cross-repo context
4. Creates linked PRs (descriptions reference each other)

### Step 3: Cross-Repo Context Passing

After primary repo PR: extract diff, include in system prompt for consumer repos:

```
## Cross-Repo Context
The following changes were made in `my-api` as part of the same ticket:
{git diff from primary repo}
Adapt this service to be compatible with these changes.
```

### Step 4: Linked PR Management

- All PRs reference parent ticket + sibling PRs
- Ticket writeback lists ALL created PRs
- If any consumer PR fails, user is notified but others are not rolled back

## Architecture

- `ProjectGroupConfig` record in Contracts
- `MultiRepoPipelineExecutor` in Application (wraps existing `PipelineExecutor`)
- `CrossRepoContext` record in Domain (carries diff + metadata between runs)
- `ProcessTicketUseCase` gets a branch: single-repo → existing, multi-repo → new executor
- Factory decides based on whether project is part of a group

## Definition of Done

- [ ] Config parsing for project groups (backward compatible)
- [ ] Sequential multi-repo execution works
- [ ] Cross-repo diff is passed as context
- [ ] Linked PRs reference each other
- [ ] Ticket writeback lists all PRs
- [ ] Single-repo projects still work exactly as before (no regression)
- [ ] Unit tests for group config parsing
- [ ] Integration test with 2 repos
