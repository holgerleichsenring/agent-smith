# Phase 30: Systemic Fixes — Progress, Orphan Cleanup, Optional Parameters

## Requirements

Three systemic issues identified during Phase 29 testing:

1. **PR URL propagation** — PR URLs set by CommitAndPRHandler/InitCommitHandler in PipelineContext
   are lost because PipelineExecutor returns a generic CommandResult without them.
   Slack never shows the PR link.

2. **Orphan job cleanup** — Docker containers that crash without sending Done/Error leave
   ConversationState stuck in Redis (2h TTL), blocking the channel. Need active detection
   and cleanup of orphaned jobs.

3. **Optional parameter elimination** — Remove `CancellationToken = default` everywhere so tokens
   are always explicitly propagated. Replace other optional params (prUrl, step/total/stepName,
   headless, codeMap, projectContext) with required parameters.

Additional fix: docker-compose.yml missing explicit `image:` tag on agentsmith service.

## Scope

### New files
- `src/AgentSmith.Dispatcher/Services/OrphanJobDetector.cs` — BackgroundService for orphan detection
- `tests/AgentSmith.Tests/Dispatcher/OrphanJobDetectorTests.cs`

### Modified files (~110 total)
- CommandResult, ProcessTicketUseCase, Program.cs, docker-compose.yml (PR URL + image fix)
- ConversationState, ConversationStateManager, MessageBusListener (orphan detection)
- All Contracts interfaces (CancellationToken defaults)
- All Infrastructure + Application + Dispatcher implementations (CancellationToken defaults)
- IProgressReporter, IAgentProvider, IContextGenerator (optional param removal)
- BusMessage, ErrorContext, ConsoleProgressReporter (optional param cleanup)
- All test files (explicit CancellationToken.None)

## Verification
- `dotnet build` after each step
- `dotnet test` — all existing + 5 new tests pass (288 total)
- Manual: init + fix via Slack show progress steps and PR URL
- Manual: kill container — orphan detector cleans up within ~2 minutes
