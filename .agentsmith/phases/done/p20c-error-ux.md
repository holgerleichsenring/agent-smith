# Phase 20c: Error UX — Retry, Logs & Contact

## Goal

Transform raw exception messages into actionable, human-readable error notifications.
Give the user three clear options when something goes wrong: retry, inspect, or escalate.

No more `TF401179: An active pull request...` dumped into a code block.

---

## Current State (bad)

```
❌ Agent Smith encountered an error:
   TF401179: An active pull request for the source and target branch already exists.
```

Problems:
- Raw API error message — meaningless to most users
- No context: which step failed? which ticket?
- No actions: what can I do now?

---

## Target State (good)

```
❌ *Agent Smith failed* — ticket #58 in agent-smith-test

*Step:* 9/9 — Creating pull request
*Reason:* A pull request for this branch already exists on the remote.

[🔄 Retry]   [📋 View Logs]   [👤 Contact @holger]
```

Three components:
1. **Human-readable context** — what step, which ticket, which project
2. **Friendly reason** — the cause, not the raw exception
3. **Three action buttons** — retry, logs, contact

---

## Error Message Architecture

### ErrorContext (new record)

```csharp
public record ErrorContext(
    string JobId,
    string TicketNumber,
    string Project,
    int FailedStep,
    int TotalSteps,
    string StepName,
    string RawError,
    string? LogUrl = null);
```

### ErrorFormatter (new class)

Translates raw exception messages into user-friendly reasons:

```csharp
public static class ErrorFormatter
{
    private static readonly (Regex Pattern, string FriendlyMessage)[] Rules =
    [
        (new Regex(@"TF401179"), "A pull request for this branch already exists."),
        (new Regex(@"non-fastforwardable"), "The remote branch has conflicting history. Try again."),
        (new Regex(@"Could not connect|connection refused", RegexOptions.IgnoreCase), "Could not reach a required service. Check network connectivity."),
        (new Regex(@"401|Unauthorized|authentication", RegexOptions.IgnoreCase), "Authentication failed. Check your API tokens."),
        (new Regex(@"403|Forbidden", RegexOptions.IgnoreCase), "Permission denied. Check token scopes."),
        (new Regex(@"rate limit|429|529", RegexOptions.IgnoreCase), "AI provider rate limit hit. Wait a moment and retry."),
        (new Regex(@"not found|404", RegexOptions.IgnoreCase), "A required resource was not found. Check ticket number and project name."),
        (new Regex(@"timeout|timed out", RegexOptions.IgnoreCase), "The operation timed out. The service may be slow — try again."),
        (new Regex(@"No test framework detected", RegexOptions.IgnoreCase), "No test framework found in the repository. Tests were skipped."),
        (new Regex(@"python|node|npm|dotnet", RegexOptions.IgnoreCase), "A required runtime is not available in the agent container."),
    ];

    public static string Humanize(string rawError)
    {
        foreach (var (pattern, friendly) in Rules)
            if (pattern.IsMatch(rawError))
                return friendly;

        // Fallback: truncate raw error to first sentence / 120 chars
        var first = rawError.Split('\n')[0].Trim();
        return first.Length > 120 ? first[..120] + "..." : first;
    }
}
```

---

## Slack Message Format

Uses Slack Block Kit for structured layout with buttons:

```json
[
  {
    "type": "section",
    "text": {
      "type": "mrkdwn",
      "text": ":x: *Agent Smith failed* — ticket #58 in *agent-smith-test*\n\n*Step:* 9/9 — Creating pull request\n*Reason:* A pull request for this branch already exists."
    }
  },
  {
    "type": "actions",
    "block_id": "error_actions:<jobId>",
    "elements": [
      {
        "type": "button",
        "text": { "type": "plain_text", "text": "🔄 Retry" },
        "style": "primary",
        "value": "<jobId>:<ticketNumber>:<project>:<channelId>",
        "action_id": "error:retry"
      },
      {
        "type": "button",
        "text": { "type": "plain_text", "text": "📋 View Logs" },
        "url": "<logUrl>",
        "action_id": "error:logs"
      },
      {
        "type": "button",
        "text": { "type": "plain_text", "text": "👤 Contact @holger" },
        "value": "<ownerSlackUserId>",
        "action_id": "error:contact"
      }
    ]
  }
]
```

---

## Log URL Generation

### Docker mode (`SPAWNER_TYPE=docker`)

No persistent log URL. Show container name instead:
```
📋 Container logs: docker logs agentsmith-<jobId>
```
Or: link to a local log endpoint on the Dispatcher if implemented.

### Kubernetes mode (`SPAWNER_TYPE=kubernetes`)

Build a link to the K8s dashboard or a log aggregator:

```csharp
// Configurable via LOG_BASE_URL env var
// Example: https://grafana.internal/explore?job=agentsmith-{jobId}
// Fallback: plain kubectl command shown as code
var logUrl = string.IsNullOrEmpty(options.LogBaseUrl)
    ? null
    : $"{options.LogBaseUrl}/agentsmith-{jobId}";
```

If no `LOG_BASE_URL` configured: omit the "View Logs" button,
show `kubectl logs job/agentsmith-<jobId>` as a code snippet instead.

---

## Retry Button Handler

When user clicks 🔄 Retry:

1. Dispatcher receives button interaction (`action_id: error:retry`)
2. Parses `value`: `<jobId>:<ticketNumber>:<project>:<channelId>`
3. Checks if channel is free (no active job in `ConversationStateManager`)
4. Spawns new job with same parameters via `IJobSpawner`
5. Posts confirmation: `🚀 Retrying ticket #58 in agent-smith-test...`

```csharp
private async Task HandleRetryAsync(
    string channelId, string ticketNumber, string project,
    string userId, string platform, CancellationToken ct)
{
    var existing = await stateManager.GetAsync(platform, channelId, ct);
    if (existing is not null)
    {
        await adapter.SendMessageAsync(channelId,
            ":hourglass: A job is already running for this channel. Please wait.", ct);
        return;
    }

    var intent = new FixTicketIntent(
        TicketId: int.Parse(ticketNumber),
        Project: project,
        ChannelId: channelId,
        UserId: userId,
        Platform: platform);

    await HandleFixTicketAsync(services, intent, ct);
}
```

---

## Contact Button Handler

When user clicks 👤 Contact:

1. Dispatcher receives button interaction (`action_id: error:contact`)
2. Reads `OWNER_SLACK_USER_ID` from environment
3. Sends a message mentioning the owner:

```
👤 <@U012AB3CD> — Agent Smith failed on ticket #58 in agent-smith-test.
A user needs help. Job ID: agentsmith-<jobId>
```

`OWNER_SLACK_USER_ID` is optional. If not set: button is omitted from the error message.

---

## BusMessage Extension

The `Error` bus message is extended to carry step context:

```csharp
// Before:
{ "type": "error", "text": "raw exception message" }

// After:
{
  "type": "error",
  "text": "raw exception message",
  "step": 9,
  "total": 9,
  "stepName": "CommitAndPRCommand"
}
```

`PipelineExecutor` passes current step info when calling `ReportErrorAsync`.
`IProgressReporter` is extended:

```csharp
Task ReportErrorAsync(
    string text,
    int step = 0,
    int total = 0,
    string stepName = "",
    CancellationToken cancellationToken = default);
```

---

## New Environment Variables

| Variable | Purpose | Default |
|---|---|---|
| `OWNER_SLACK_USER_ID` | Slack user ID for contact button | (empty = button hidden) |
| `LOG_BASE_URL` | Base URL for log links (Grafana, Kibana, etc.) | (empty = kubectl hint) |

Both optional. Graceful degradation when not set.

---

## Phase 20c Steps

| Step | File | Description |
|------|------|-------------|
| 20c-1 | `phase20c-error-formatter.md` | ErrorFormatter, ErrorContext, humanize rules |
| 20c-2 | `phase20c-bus-message.md` | BusMessage Error extension with step context |
| 20c-3 | `phase20c-slack-blocks.md` | SlackAdapter.SendErrorAsync with Block Kit buttons |
| 20c-4 | `phase20c-retry-handler.md` | Retry button callback handler in Dispatcher |
| 20c-5 | `phase20c-contact-handler.md` | Contact button + OWNER_SLACK_USER_ID wiring |

---

## Constraints & Notes

- `ErrorFormatter.Humanize` is pure (no I/O) and fully testable
- The retry flow reuses `HandleFixTicketAsync` — no duplication
- "View Logs" is a URL button (no Dispatcher involvement after click)
- Contact button sends a new message — does NOT create a thread or DM
- All three buttons are optional — omitted cleanly if prerequisites not met
- Raw error is still logged server-side at `LogError` level for debugging

---

## Success Criteria

- [ ] Error message shows step number, ticket, project, friendly reason
- [ ] 🔄 Retry spawns a new job with the same parameters
- [ ] Retry is blocked if a job is already running for the channel
- [ ] 📋 View Logs shows a URL (if LOG_BASE_URL set) or kubectl hint
- [ ] 👤 Contact button mentions the owner (if OWNER_SLACK_USER_ID set)
- [ ] Both optional buttons are cleanly omitted when env vars not set
- [ ] `ErrorFormatter` translates all known error patterns correctly
- [ ] Raw exception is never shown to the user in Slack

---

## Dependencies

- Phase 20a complete (ResolvedIntent, IntentEngine for retry path)
- Phase 20b complete (HelpHandler reused in retry clarification edge case)
- `IProgressReporter.ReportErrorAsync` signature extension
- `BusMessage.Error` factory method updated
- `ConversationStateManager` unchanged (already handles active job check)

---

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
