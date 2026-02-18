# Phase 13: Ticket Writeback & Status - Implementation Plan

## Goal
Agent Smith documents its work in the ticket: status updates while working,
result summary at the end, link to the PR. On failure, posts the error to the ticket.

---

## Prerequisite
- Phase 10 completed (Container Production-Ready)

## Steps

### Step 1: ITicketProvider Extensions
See: `prompts/phase13-ticket-writeback.md`

Add `UpdateStatusAsync` and `CloseTicketAsync` to ITicketProvider with default
implementations. Implement in GitHub and Azure DevOps providers.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Pipeline & Handler Integration
See: `prompts/phase13-pipeline-integration.md`

PipelineExecutor posts "working on it" at start and error on failure.
CommitAndPRHandler posts summary with PR link and closes the ticket.
Project: `AgentSmith.Application/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (ITicketProvider Extensions)
    └── Step 2 (Pipeline + Handler Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 13)

No new packages required.

---

## Key Decisions

1. Default interface implementations (`=> Task.CompletedTask`) for backward compatibility
2. Ticket operations are fire-and-forget: failures logged as warnings, never block the pipeline
3. CommitAndPRHandler gets ITicketProviderFactory dependency for posting summary
4. PipelineExecutor gets ITicketProviderFactory for status updates and error reporting
5. Ticket comment includes: PR link, change list, status

---

## Ticket Comment Format
```markdown
## Agent Smith - Completed

**PR:** #42

### Changes
- [Created] `README.md`
- [Modified] `src/Program.cs`

This ticket was automatically processed by Agent Smith.
```

---

## Definition of Done (Phase 13)
- [ ] `ITicketProvider.UpdateStatusAsync()` with default implementation
- [ ] `ITicketProvider.CloseTicketAsync()` with default implementation
- [ ] GitHubTicketProvider: issue comment + close
- [ ] AzureDevOpsTicketProvider: work item history + state transition
- [ ] CommitAndPRContext includes TicketConfig
- [ ] CommitAndPRHandler posts summary and closes ticket
- [ ] PipelineExecutor posts "working on it" at pipeline start
- [ ] PipelineExecutor posts error details on pipeline failure
- [ ] Tests for CommitAndPRHandler (close, failure tolerance, summary content)
- [ ] Tests for PipelineExecutor (status posting, failure tolerance)
- [ ] All existing tests green
