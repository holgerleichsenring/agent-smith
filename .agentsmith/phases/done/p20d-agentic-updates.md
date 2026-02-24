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

---

# Phase 20d: Implementation Notes — Agentic Detail Updates

## What was built

Real-time detail events from the agentic loop (step 7/9) that appear as Slack thread replies under the progress message. Users see what the AI agent is doing: reading files, writing code, running builds, compacting context.

## Why

Step 7/9 "Executing plan" can run for minutes. Before this phase, users stared at a static progress bar with no idea what was happening. Now they see a live thread with scout activity, file operations, build commands, and iteration progress. The main channel stays clean — all detail goes into the thread.

## Key decisions

- **`ReportDetailAsync` added to `IProgressReporter`** — a new method alongside the existing progress/error/done methods. In CLI mode it logs at Debug level (only visible with `--verbose`). In Redis/Slack mode it publishes a `Detail` bus message.
- **Detail events are fire-and-forget** — errors in `ReportDetailAsync` are swallowed. A failed Slack API call must never abort the agentic loop. This is enforced in `ToolExecutor.ReportDetail()` with a try/catch around `GetAwaiter().GetResult()`.
- **Thread replies use existing `_progressMessageTs`** — `SlackAdapter.SendDetailAsync` posts as a thread reply using the `thread_ts` of the progress message already tracked per channel. No new state needed.
- **`BusMessageType.Detail` added** — new enum value, same outbound stream. `MessageBusListener` routes it to `adapter.SendDetailAsync`.
- **All three AI providers instrumented** — ClaudeAgentProvider, OpenAiAgenticLoop, and GeminiAgenticLoop all call `progressReporter?.ReportDetailAsync` for iteration events. ToolExecutor reports file reads, writes, and command executions.
- **`IPlatformAdapter.SendDetailAsync` added** — interface method for thread-based detail posting.

## Additional fixes applied during Phase 20d deployment

Several issues were discovered and fixed while deploying and testing:

### Dispatcher fire-and-forget race condition
`HandleSlackEventsAsync` used `ctx.RequestServices` inside `Task.Run`, but the `HttpContext` is recycled after the request returns 200. Fix: capture `IServiceScopeFactory` (a singleton) before `Task.Run` and create a new scope inside the background task.

### ToolExecutor command timeout deadlock
`ReadToEndAsync` was started before `WaitForExitAsync`, causing a deadlock when `dotnet run` (a never-ending server process) was executed. Fix: wait for process exit first (with 60s timeout), then read stdout/stderr only after the process has terminated.

### TestHandler false positive on .csproj
`DetectTestCommand` returned `dotnet test` for any repo containing a `.csproj` file, even without test projects. Fix: `HasDotNetTestProjects` reads each `.csproj` and checks for `Microsoft.NET.Test.Sdk` package reference — the standard marker for .NET test projects.

### Execution prompt improvements
The AI agent's system prompt was missing guardrails. Added: explicit prohibition of long-running server processes (`dotnet run`, `npm start`), requirement to state intent before each tool call, and `dotnet test` alongside `dotnet build` as recommended verification commands.

### Dockerfile runtime → SDK
The runtime stage used `dotnet/runtime:8.0` which has no SDK tools. The agentic loop needs `dotnet build` and `dotnet test` inside the container. Changed to `dotnet/sdk:8.0`.

## Files modified

| File | Change |
|------|--------|
| `Contracts/Services/IProgressReporter.cs` | Added ReportDetailAsync method |
| `Application/Services/ConsoleProgressReporter.cs` | Detail → Debug log |
| `Infrastructure/Bus/BusMessage.cs` | Added BusMessageType.Detail + factory |
| `Infrastructure/Bus/RedisProgressReporter.cs` | Publishes detail messages |
| `Infrastructure/Providers/Agent/ClaudeAgentProvider.cs` | Passes IProgressReporter to ToolExecutor and AgenticLoop; improved execution prompt |
| `Infrastructure/Providers/Agent/ToolExecutor.cs` | ReportDetail on file ops and commands; fixed command timeout |
| `Infrastructure/Providers/Agent/AgenticLoop.cs` | Iteration detail events |
| `Infrastructure/Providers/Agent/OpenAiAgentProvider.cs` | IProgressReporter parameter |
| `Infrastructure/Providers/Agent/OpenAiAgenticLoop.cs` | Iteration detail events |
| `Infrastructure/Providers/Agent/GeminiAgentProvider.cs` | IProgressReporter parameter |
| `Infrastructure/Providers/Agent/GeminiAgenticLoop.cs` | Iteration detail events |
| `Infrastructure/Providers/Agent/ScoutAgent.cs` | Scout detail events |
| `Dispatcher/Adapters/IPlatformAdapter.cs` | Added SendDetailAsync |
| `Dispatcher/Adapters/SlackAdapter.cs` | Thread replies via _progressMessageTs |
| `Dispatcher/Services/MessageBusListener.cs` | Routes Detail messages to adapter |
| `Dispatcher/Extensions/WebApplicationExtensions.cs` | IServiceScopeFactory fix for fire-and-forget |
| `Application/Commands/Handlers/TestHandler.cs` | Smart test project detection |
| `Application/Commands/Handlers/AgenticExecuteHandler.cs` | Passes IProgressReporter to provider |
| `Dockerfile` | SDK instead of runtime for agentic loop |
| `config/agentsmith.yml` | agent-smith-test back to fix-bug pipeline |
