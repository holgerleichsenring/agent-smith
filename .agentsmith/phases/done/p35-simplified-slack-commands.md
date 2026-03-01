# Phase 35: Simplified Slack Commands

## Context

The Slack modal exposed "Fix Ticket" as a single command with a separate Pipeline dropdown
(fix-bug, fix-no-test, add-feature). The pipeline is an implementation detail that shouldn't
be visible to users. Users should pick **what they want to do**, not which pipeline to run.

## Changes

### ModalCommandType — split FixTicket into three
`FixTicket` replaced with `FixBug`, `FixBugNoTests`, `AddFeature`.
Each maps implicitly to its pipeline preset.

### SlackModalBuilder — new command options, remove pipeline
- 6 commands in dropdown: Fix Bug, Fix Bug (no tests), Add Feature, List Tickets, Create Ticket, Init Project
- Pipeline dropdown removed entirely (`BuildPipelineBlock()` deleted)
- `BuildUpdatedView()` no longer accepts `pipelineNames` parameter
- All three ticket commands show the Ticket selector

### SlackModalSubmissionHandler — implicit pipeline routing
- `FixBug` → `"fix-bug"`, `FixBugNoTests` → `"fix-no-test"`, `AddFeature` → `"add-feature"`
- Pipeline determined by command type, no longer read from modal state

### Cleanup
- `SlackBlockPipeline` / `SlackActionPipeline` constants removed from DispatcherDefaults
- `PipelinePresets.Names` no longer referenced from Dispatcher
- `using AgentSmith.Contracts.Commands` removed from WebApplicationExtensions

## Files Modified
1. `src/AgentSmith.Dispatcher/Models/ModalCommandType.cs`
2. `src/AgentSmith.Dispatcher/Services/Adapters/SlackModalBuilder.cs`
3. `src/AgentSmith.Dispatcher/Services/Handlers/SlackModalSubmissionHandler.cs`
4. `src/AgentSmith.Dispatcher/Extensions/WebApplicationExtensions.cs`
5. `src/AgentSmith.Dispatcher/Services/DispatcherDefaults.cs`
6. `tests/AgentSmith.Tests/Dispatcher/SlackModalBuilderTests.cs`
7. `tests/AgentSmith.Tests/Dispatcher/SlackModalSubmissionHandlerTests.cs`

## Result
413 tests, all passing.
