# Phase 18 – Step 1: IMessageBus + RedisMessageBus + BusMessage Protocol

## Goal

Implement the Redis Streams communication layer between agent containers and the
dispatcher service. This is the backbone of the Phase 18 architecture: every
progress update, question, answer and completion message flows through this bus.

---

## Files to Create

### `src/AgentSmith.Dispatcher/Models/BusMessage.cs`

Discriminated union record for all message types exchanged over Redis Streams.

**Message types:**

| Type | Direction | Description |
|------|-----------|-------------|
| `Progress` | Agent → Dispatcher | Pipeline step update (step, total, commandName) |
| `Question` | Agent → Dispatcher | Agent needs yes/no input (questionId, text) |
| `Done` | Agent → Dispatcher | Job completed successfully (prUrl, summary) |
| `Error` | Agent → Dispatcher | Job failed (text) |
| `Answer` | Dispatcher → Agent | User's reply to a question (questionId, content) |

**Key decisions:**
- Single `BusMessage` record with optional fields (not a proper discriminated union)
  to keep Redis serialization simple (flat key-value entries)
- Static factory methods (`Progress(...)`, `Question(...)`, etc.) for ergonomic construction
- `JobId` is always required — every message is scoped to exactly one job

```csharp
public enum BusMessageType { Progress, Question, Done, Error, Answer }

public sealed record BusMessage
{
    public required BusMessageType Type { get; init; }
    public required string JobId { get; init; }
    public int? Step { get; init; }
    public int? Total { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? QuestionId { get; init; }
    public string? PrUrl { get; init; }
    public string? Summary { get; init; }
    public string? Content { get; init; }

    public static BusMessage Progress(string jobId, int step, int total, string text) => ...
    public static BusMessage Question(string jobId, string questionId, string text) => ...
    public static BusMessage Done(string jobId, string? prUrl, string summary) => ...
    public static BusMessage Error(string jobId, string text) => ...
    public static BusMessage Answer(string jobId, string questionId, string content) => ...
}
```

---

### `src/AgentSmith.Dispatcher/Services/IMessageBus.cs`

Interface for all Redis Streams operations.

```csharp
public interface IMessageBus
{
    // Agent container → outbound stream (job:{jobId}:out)
    Task PublishAsync(BusMessage message, CancellationToken cancellationToken = default);

    // Dispatcher → inbound stream (job:{jobId}:in)
    Task PublishAnswerAsync(string jobId, string questionId, string content,
        CancellationToken cancellationToken = default);

    // Dispatcher subscribes after spawning a job.
    // Completes when Done/Error is received or token is cancelled.
    IAsyncEnumerable<BusMessage> SubscribeToJobAsync(string jobId,
        CancellationToken cancellationToken = default);

    // Agent container waits for user reply on inbound stream.
    // Returns null on timeout.
    Task<BusMessage?> ReadAnswerAsync(string jobId, TimeSpan timeout,
        CancellationToken cancellationToken = default);

    // Cleanup both streams after job ends.
    Task CleanupJobAsync(string jobId, CancellationToken cancellationToken = default);
}
```

---

### `src/AgentSmith.Dispatcher/Services/RedisMessageBus.cs`

`StackExchange.Redis` implementation of `IMessageBus`.

**Stream key schema:**
```
job:{jobId}:out   ← agent publishes here (progress, question, done, error)
job:{jobId}:in    ← dispatcher publishes answers here
```

**Implementation notes:**

- `PublishAsync` → `XADD job:{id}:out MAXLEN ~ 1000 * field value ...`
  then `EXPIRE job:{id}:out 7200` (2-hour TTL)
- `SubscribeToJobAsync` → polling loop with `XREAD COUNT 10 STREAMS job:{id}:out lastId`
  500ms poll interval; yields each message; terminates on `Done` or `Error` type
- `ReadAnswerAsync` → polls `XREAD COUNT 1 STREAMS job:{id}:in lastId`
  deadline-based loop, returns `null` on timeout
- `PublishAnswerAsync` → `XADD job:{id}:in MAXLEN ~ 1000 * ...`
- `CleanupJobAsync` → `DEL job:{id}:out job:{id}:in`

**Serialization:**
Each message is stored as flat `NameValueEntry[]` fields (not JSON).
All fields are always written; empty string for missing optionals.

```
type        "Progress"
jobId       "abc123"
step        "3"
total       "9"
text        "Analyzing code..."
questionId  ""
prUrl       ""
summary     ""
content     ""
```

Deserialization uses a type-switch after parsing `BusMessageType` from the `type` field.
Malformed messages are silently skipped (swallow `Exception` in deserializer).

---

## NuGet Packages

```xml
<PackageReference Include="StackExchange.Redis" Version="2.*" />
```

Already referenced in `AgentSmith.Dispatcher.csproj`.

---

## Stream Lifecycle Summary

```
1. Dispatcher receives "fix #65 in todo-list"
2. JobSpawner.SpawnAsync() → K8s Job created
3. MessageBusListener.TrackJobAsync(jobId) → starts SubscribeToJobAsync
4. Agent container starts, publishes Progress messages to job:{id}:out
5. Dispatcher receives Progress → SlackAdapter.SendProgressAsync()
6. Agent publishes Question → Dispatcher posts buttons in Slack
7. User clicks button → Dispatcher.PublishAnswerAsync() to job:{id}:in
8. Agent ReadAnswerAsync() unblocks → continues execution
9. Agent publishes Done → Dispatcher posts PR link, cleans up
```

---

## Definition of Done

- [ ] `BusMessage` with all 5 types and factory methods
- [ ] `IMessageBus` with all 5 methods
- [ ] `RedisMessageBus` implementing `IMessageBus`
- [ ] Stream keys follow `job:{jobId}:out` / `job:{jobId}:in` schema
- [ ] All streams use MAXLEN 1000 and 2-hour TTL
- [ ] `SubscribeToJobAsync` terminates on Done/Error
- [ ] `ReadAnswerAsync` returns null on timeout (no exception)
- [ ] `CleanupJobAsync` deletes both streams
- [ ] Deserialization errors are silently skipped
- [ ] `dotnet build` clean