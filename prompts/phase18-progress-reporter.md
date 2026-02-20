# Phase 18 – Step 2: IProgressReporter Extensions (Redis Mode)

## Goal

Extend the existing `IProgressReporter` abstraction with a Redis Streams implementation
so agent containers running in K8s can publish their progress back to the Dispatcher
over the message bus instead of writing directly to the console.

---

## Context

In local/CLI mode the agent writes progress to stdout via `ConsoleProgressReporter`.
In K8s job mode (when `--job-id` is set) the agent must instead publish structured
messages to the Redis stream `job:{jobId}:out` so the Dispatcher can relay them to Slack.

---

## What Already Exists

```
AgentSmith.Contracts/Services/IProgressReporter.cs  ← interface (unchanged)
```

The interface already defines:
- `ReportProgressAsync(int step, int total, string commandName)`
- `AskYesNoAsync(string questionId, string text, bool defaultAnswer)`
- `ReportDoneAsync(string summary, string? prUrl)`
- `ReportErrorAsync(string text)`

---

## New File: `RedisProgressReporter`

**Location:** `src/AgentSmith.Dispatcher/Services/RedisProgressReporter.cs`

### Behaviour

| Method | Redis action |
|--------|-------------|
| `ReportProgressAsync` | `XADD job:{jobId}:out` → `BusMessage.Progress` |
| `AskYesNoAsync` | `XADD job:{jobId}:out` → `BusMessage.Question`, then `XREAD BLOCK` on `job:{jobId}:in` up to 5 min |
| `ReportDoneAsync` | `XADD job:{jobId}:out` → `BusMessage.Done` |
| `ReportErrorAsync` | `XADD job:{jobId}:out` → `BusMessage.Error` |

### `AskYesNoAsync` answer mapping

| User input | Result |
|------------|--------|
| `yes`, `y`, `true`, `1` | `true` |
| `no`, `n`, `false`, `0` | `false` |
| anything else / timeout | `defaultAnswer` |

Timeout: **5 minutes**. If no answer arrives within the timeout the method logs a warning
and returns `defaultAnswer`.

### Implementation

```csharp
public sealed class RedisProgressReporter(
    IMessageBus messageBus,
    string jobId,
    ILogger<RedisProgressReporter> logger) : IProgressReporter
{
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromMinutes(5);

    public async Task ReportProgressAsync(int step, int total, string commandName,
        CancellationToken cancellationToken = default)
    {
        var message = BusMessage.Progress(jobId, step, total, commandName);
        await messageBus.PublishAsync(message, cancellationToken);
    }

    public async Task<bool> AskYesNoAsync(string questionId, string text,
        bool defaultAnswer = true, CancellationToken cancellationToken = default)
    {
        var question = BusMessage.Question(jobId, questionId, text);
        await messageBus.PublishAsync(question, cancellationToken);

        var answer = await messageBus.ReadAnswerAsync(jobId, AnswerTimeout, cancellationToken);
        if (answer is null) return defaultAnswer;

        return answer.Content?.Trim().ToLowerInvariant() switch
        {
            "yes" or "y" or "true" or "1" => true,
            "no"  or "n" or "false" or "0" => false,
            _ => defaultAnswer
        };
    }

    public async Task ReportDoneAsync(string summary, string? prUrl = null,
        CancellationToken cancellationToken = default)
    {
        var message = BusMessage.Done(jobId, prUrl, summary);
        await messageBus.PublishAsync(message, cancellationToken);
    }

    public async Task ReportErrorAsync(string text,
        CancellationToken cancellationToken = default)
    {
        var message = BusMessage.Error(jobId, text);
        await messageBus.PublishAsync(message, cancellationToken);
    }
}
```

---

## DI Wiring (AgentSmith.Host)

In `Program.cs` (Host project), after parsing CLI args:

```csharp
if (!string.IsNullOrWhiteSpace(jobId))
{
    // K8s job mode: publish progress to Redis
    services.AddSingleton<IMessageBus, RedisMessageBus>();
    services.AddSingleton<IProgressReporter>(sp =>
        new RedisProgressReporter(
            sp.GetRequiredService<IMessageBus>(),
            jobId,
            sp.GetRequiredService<ILogger<RedisProgressReporter>>()));
}
else
{
    // Local mode: write progress to stdout
    services.AddSingleton<IProgressReporter, ConsoleProgressReporter>();
}
```

No composite reporter is needed — Docker logs (`docker logs`) are always available
via stdout even in K8s, and adding a second sink would double-write every message.

---

## Files Touched

| File | Change |
|------|--------|
| `src/AgentSmith.Dispatcher/Services/RedisProgressReporter.cs` | **New** |
| `src/AgentSmith.Host/Program.cs` | Add Redis branch in DI wiring |

---

## Definition of Done

- [ ] `RedisProgressReporter` implements all 4 methods of `IProgressReporter`
- [ ] Timeout handling: returns `defaultAnswer` when no answer within 5 min
- [ ] Answer content is parsed case-insensitively
- [ ] `AskYesNoAsync` logs the received content and final bool result
- [ ] DI wiring selects `RedisProgressReporter` when `--job-id` is set
- [ ] `dotnet build` clean