# Phase 44: Rename ProcessTicketUseCase → ExecutePipelineUseCase

## Goal

Agent Smith is no longer a "ticket processing" tool. It orchestrates arbitrary
AI workflows (code, legal, security, discussions). The central use case class
and related naming must reflect this.

---

## What to Rename

### Class rename (30+ files affected)

`ProcessTicketUseCase` → `ExecutePipelineUseCase`

This touches:
- The class itself (`src/AgentSmith.Application/Services/ProcessTicketUseCase.cs`)
- Every file that references it (Host, Dispatcher, WebhookListener, tests, DI registration)
- Logger type parameters
- Phase docs that reference it (historical, low priority)

### Related renames (while we're at it)

| Current | New | Reason |
|---------|-----|--------|
| `ProcessTicketUseCase` | `ExecutePipelineUseCase` | Not ticket-specific |
| `ScoutAgent` comment "before the primary coding agent" | Neutral wording | Not coding-specific |
| `CodingPrinciplesGenerator` prompt "AI coding agent" | "AI orchestration agent" | Not coding-specific |

### NOT renamed (too deep, separate concern)

- `Ticket`, `TicketId`, `ITicketProvider` — these are the domain model for
  one type of input. Legal has `SourceFilePath`, security has `--repo`. The
  ticket concept stays as-is for ticket-based pipelines.
- `FetchTicketCommand` — same reason, it fetches tickets specifically.

---

## Definition of Done

- [ ] `ProcessTicketUseCase` renamed to `ExecutePipelineUseCase` across all files
- [ ] File renamed: `ProcessTicketUseCase.cs` → `ExecutePipelineUseCase.cs`
- [ ] Test file renamed: `ProcessTicketUseCaseTests.cs` → `ExecutePipelineUseCaseTests.cs`
- [ ] All references updated (DI, Host, Dispatcher, WebhookListener)
- [ ] Neutral wording in ScoutAgent and CodingPrinciplesGenerator comments
- [ ] All tests green
- [ ] Single commit, no behavioral changes
