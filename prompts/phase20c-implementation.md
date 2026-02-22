# Phase 20c: Implementation Notes — Error UX

## What was built

Structured error reporting that transforms raw exceptions into actionable Slack messages with context (which step failed, which ticket) and action buttons (retry, abandon).

## Why

Before this phase, errors were dumped as raw exception messages into Slack — e.g. `TF401179: An active pull request for the source and target branch already exists.` Users had no idea what step failed, which ticket was affected, or what to do next. Now they see a formatted error with step context and can retry with one click.

## Key decisions

- **ErrorFormatter uses regex pattern matching** — a table of known error patterns mapped to friendly messages. Unknown errors get their first line truncated to 120 chars. Pure function, no I/O, fully testable.
- **ErrorContext carries pipeline state** — step number, total steps, step name, ticket, project. This is passed through `CommandResult` (extended with `FailedStep`, `TotalSteps`, `StepName` fields) and propagated via `IProgressReporter.ReportErrorAsync`.
- **BusMessage.Error extended** — now carries `Step`, `Total`, and `StepName` fields alongside the error text, so the dispatcher can reconstruct the full error context.
- **Slack Block Kit for buttons** — `SlackErrorBlockBuilder` creates structured blocks with retry/abandon buttons. Button values encode the job context (ticket, project, channel) so the retry handler can respawn the job.
- **SlackErrorActionHandler** processes button clicks — retry spawns a new job, abandon acknowledges the error.

## Files created

| File | Purpose |
|------|---------|
| `Services/ErrorFormatter.cs` | Regex-based error humanization (raw → friendly) |
| `Models/ErrorContext.cs` | Structured error state (step, ticket, project, raw error) |
| `Adapters/SlackErrorBlockBuilder.cs` | Slack Block Kit JSON for error messages with buttons |
| `Adapters/SlackErrorActionHandler.cs` | Handles retry/abandon button callbacks |

## Files modified

| File | Change |
|------|--------|
| `Domain/ValueObjects/CommandResult.cs` | Added FailedStep, TotalSteps, StepName fields |
| `Contracts/Services/IProgressReporter.cs` | Extended ReportErrorAsync with step/total/stepName params |
| `Application/Services/PipelineExecutor.cs` | Passes step context to ReportErrorAsync on failure |
| `Application/Services/ConsoleProgressReporter.cs` | Implements extended ReportErrorAsync |
| `Infrastructure/Bus/BusMessage.cs` | Error factory includes step metadata |
| `Infrastructure/Bus/RedisProgressReporter.cs` | Publishes extended error messages |
| `Dispatcher/Services/MessageBusListener.cs` | Routes error messages through ErrorFormatter |
| `Dispatcher/Adapters/SlackAdapter.cs` | SendErrorAsync uses Block Kit blocks |
