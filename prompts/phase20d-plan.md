# Phase 20d: Agentic Detail-Updates in Slack Thread

## Goal

Give users real-time visibility into what the AI agent is doing during the long
step 7/9 (AgenticExecuteCommand). Instead of staring at a static progress bar
for minutes, users see a live feed of agent activity in a Slack thread.

The channel stays clean — details are in the thread, not flooding the main view.

---

## User Experience

### Main Channel (unchanged)
```
🚀 Starting Agent Smith for ticket #58 in agent-smith-test...
⚡ [7/9] Executing plan
[████████░░] 7/9  (edited continuously)
```

### Thread (new — appears automatically under the progress message)
```
🔍 Scout: analyzing 8 files...
📄 Reading: src/AgentSmith.Application/Services/PipelineExecutor.cs
📄 Reading: src/AgentSmith.Host/Program.cs
✏️  Writing: src/AgentSmith.Application/Services/PipelineExecutor.cs
▶️  Running: dotnet build
✅  Build succeeded
🔄 Iteration 4 — refining solution...
⚡ Context compacted (18 → 7 messages)
▶️  Running: dotnet test
✅  Tests passed
```

---

## IProgressReporter Extension

Add `ReportDetailAsync` to the interface:

```csharp
public interface IProgressReporter
{
    // Existing
    Task ReportProgressAsync(int step, int total, string commandName,
        CancellationToken cancellationToken = default);

    Task<bool> AskYesNoAsync(string questionId, string text,
        bool defaultAnswer = true,
        CancellationToken cancellationToken = default);

    Task ReportDoneAsync(string summary, string? prUrl = null,
        CancellationToken cancellationToken = default);

    Task ReportErrorAsync(string text, int step = 0, int total = 0,
        string stepName = "",
        CancellationToken cancellationToken = default);

    // NEW
    /// <summary>
    /// Reports a fine-grained detail event during agentic execution.
    /// In Slack mode, this is posted as a thread reply under the progress message.
    /// In CLI mode, this is logged at Debug level.
    /// </summary>
    Task ReportDetailAsync(string text,
        CancellationToken cancellationToken = default);
}
```

---

## BusMessage Extension

Add a new `Detail` message type to the bus protocol:

```csharp
public enum BusMessageType
{
    Progress,
    Detail,     // NEW
    Question,
    Done,
    Error,
    Answer
}
```

Factory method:

```csharp
public static BusMessage Detail(string jobId, string text) => new()
{
    Type = BusMessageType.Detail,
    JobId = jobId,
    Text = text
};
```

Stream key: same `job:{jobId}:out` stream — no new stream needed.

---

## Detail Events in ClaudeAgentProvider

The following events trigger `ReportDetailAsync` calls:

| Event | Detail Text |
|---|---|
| Scout starts | `🔍 Scout: analyzing codebase...` |
| Scout reads a file | `📄 Reading: {relativePath}` |
| Scout completes | `🔍 Scout: found {n} relevant files` |
| Agent writes a file | `✏️ Writing: {relativePath}` |
| Agent reads a file | `📄 Reading: {relativePath}` |
| Agent runs a command | `▶️ Running: {command}` |
| Command output (short) | `💬 {firstLine}` |
| Iteration change | `🔄 Iteration {n}...` |
| Context compaction | `⚡ Context compacted ({before} → {after} messages)` |
| Retry after rate limit | `⏳ Rate limited — retrying in {delay}s...` |

### Rate Limiting Detail Events

To avoid flooding the thread, detail events are throttled:
- File reads: max one message per 3 files (batched: `📄 Reading 3 files...`)
- Command output: only first line, max 80 chars, only if non-empty
- Iteration: only every other iteration (1, 3, 5, ... or every iteration if ≤ 5 total)

---

## ConsoleProgressReporter

`ReportDetailAsync` logs at `Debug` level — only visible with `--verbose`:

```csharp
public Task ReportDetailAsync(string text, CancellationToken cancellationToken = default)
{
    logger.LogDebug("  [detail] {Text}", text);
    return Task.CompletedTask;
}
```

No change to default CLI output.

---

## RedisProgressReporter

Publishes `BusMessageType.Detail` to the outbound stream:

```csharp
public async Task ReportDetailAsync(string text, CancellationToken cancellationToken = default)
{
    var message = BusMessage.Detail(jobId, text);
    await messageBus.PublishAsync(message, cancellationToken);
    logger.LogDebug("Published detail for job {JobId}: {Text}", jobId, text);
}
```

---

## MessageBusListener Extension

Add `HandleDetailAsync` alongside the existing `HandleProgressAsync`:

```csharp
case BusMessageType.Detail:
    await HandleDetailAsync(adapter, state, message, cancellationToken);
    break;
```

```csharp
private static Task HandleDetailAsync(
    IPlatformAdapter adapter,
    ConversationState state,
    BusMessage message,
    CancellationToken cancellationToken)
{
    return adapter.SendDetailAsync(
        state.ChannelId,
        message.Text,
        cancellationToken);
}
```

---

## IPlatformAdapter Extension

```csharp
/// <summary>
/// Sends a fine-grained detail event as a thread reply under the
/// current progress message. If no progress message exists yet,
/// the detail is silently dropped.
/// </summary>
Task SendDetailAsync(string channelId, string text,
    CancellationToken cancellationToken = default);
```

---

## SlackAdapter: Thread Support

The `SlackAdapter` already tracks the progress message `ts` per channel
(added in the progress update work). `SendDetailAsync` uses this `ts`
as the `thread_ts` to post thread replies:

```csharp
public async Task SendDetailAsync(string channelId, string text,
    CancellationToken cancellationToken = default)
{
    // Only post if we have a progress message to thread under
    if (!_progressMessageTs.TryGetValue(channelId, out var threadTs))
        return;

    var payload = new
    {
        channel = channelId,
        thread_ts = threadTs,
        text
    };

    await PostAsync("chat.postMessage", payload, cancellationToken);
}
```

No new state required — reuses the existing `_progressMessageTs` dictionary.

---

## ConversationState Extension

No changes needed. The thread `ts` is stored in `SlackAdapter` memory
(scoped to the Dispatcher process lifetime). If the Dispatcher restarts
mid-job, thread replies resume as new top-level messages — acceptable
degradation.

For a more robust solution (Dispatcher restart resilience), the thread `ts`
could be stored in `ConversationState` in Redis. This is out of scope for
this phase but noted as a future improvement.

---

## Phase 20d Steps

| Step | File | Description |
|------|------|-------------|
| 20d-1 | `phase20d-progress-reporter.md` | Add ReportDetailAsync to IProgressReporter, ConsoleProgressReporter, RedisProgressReporter |
| 20d-2 | `phase20d-bus-message.md` | Add Detail to BusMessageType + BusMessage factory + RedisMessageBus serialization |
| 20d-3 | `phase20d-claude-provider.md` | Instrument ClaudeAgentProvider with ReportDetailAsync calls |
| 20d-4 | `phase20d-slack-thread.md` | IPlatformAdapter.SendDetailAsync + SlackAdapter thread implementation |
| 20d-5 | `phase20d-dispatcher-wiring.md` | MessageBusListener: handle Detail messages, route to adapter |

---

## Constraints & Notes

- `ReportDetailAsync` is fire-and-forget in ClaudeAgentProvider — errors are
  swallowed (a failed detail post must never abort the agentic loop)
- Detail messages are NOT stored in `ConversationState` — they are ephemeral
- The Slack API rate limit is 1 message/second per channel. Detail events must
  be throttled (see rate limiting section above) to stay within limits
- Thread replies do NOT trigger Slack notifications for channel members
  (by default) — this is intentional, no noise
- OpenAI and Gemini providers should also be instrumented (20d-3 covers all providers)
- `--verbose` flag in CLI mode enables detail output at Debug level

---

## Success Criteria

- [ ] During step 7/9, a Slack thread appears under the progress message
- [ ] Thread shows scout activity, file reads, writes, commands, iterations
- [ ] Thread does NOT appear for steps 1-6 and 8-9 (no detail events there)
- [ ] Main channel progress message continues updating normally
- [ ] CLI mode with `--verbose` shows detail lines at debug level
- [ ] CLI mode without `--verbose` shows no detail output
- [ ] A failed `SendDetailAsync` call does not abort the pipeline
- [ ] Detail events are rate-throttled (no Slack rate limit errors)
- [ ] Context compaction events are always shown (never throttled)

---

## Dependencies

- Phase 20c complete (IProgressReporter.ReportErrorAsync signature already extended)
- `BusMessage` and `RedisMessageBus` already handle extensible message types
- `SlackAdapter._progressMessageTs` already exists (from progress update feature)
- `ClaudeAgentProvider` instrumentation requires access to `IProgressReporter`
  (already injected via `AgenticExecuteHandler` → confirm DI chain)