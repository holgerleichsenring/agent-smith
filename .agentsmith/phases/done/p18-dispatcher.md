# Phase 18: Multi-User Chat Gateway

## Goal

Evolve Agent Smith from a single-user CLI/webhook tool into a multi-user,
multi-channel platform where Teams, Slack, and WhatsApp users can trigger
and interact with agentic runs in real time.

Each "fix ticket" request runs in its own ephemeral K8s Job (container).
"List tickets" and "create ticket" are handled directly by the Dispatcher
without spawning a job.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     CHAT PLATFORMS                          │
│          Teams        Slack        WhatsApp                  │
└────────────┬────────────┬──────────────┬────────────────────┘
             │            │              │
             ▼            ▼              ▼
┌─────────────────────────────────────────────────────────────┐
│              DISPATCHER SERVICE (always-on)                  │
│                    ASP.NET Core Minimal API                  │
│                                                              │
│  ┌───────────────┐  ┌──────────────┐  ┌──────────────────┐  │
│  │ Platform      │  │ Chat Intent  │  │ Conversation     │  │
│  │ Adapters      │  │ Parser       │  │ StateManager     │  │
│  │ (Teams/Slack) │  │ (list/fix/   │  │ (Redis)          │  │
│  └───────────────┘  │  create)     │  └──────────────────┘  │
│                     └──────────────┘                         │
│  ┌──────────────────────┐  ┌──────────────────────────────┐  │
│  │ JobSpawner           │  │ FastCommandExecutor           │  │
│  │ (K8s .NET Client)    │  │ (list/create, no job needed) │  │
│  └──────────────────────┘  └──────────────────────────────┘  │
└───────────────────────────┬─────────────────────────────────┘
                            │
                    Redis Streams
                    (job:{id}:out / job:{id}:in)
                            │
┌───────────────────────────▼─────────────────────────────────┐
│                  AGENT CONTAINER (K8s Job)                   │
│         [ephemeral - lives only during the request]          │
│                                                              │
│  ENV: TICKET_ID, PROJECT, JOB_ID, REDIS_URL, CHANNEL_ID,   │
│       USER_ID, PLATFORM, AGENT_SMITH_CONFIG (base64)        │
│                                                              │
│  Publishes → { type: progress|question|done|error, text }   │
│  Reads    ← { type: answer, content }                        │
└─────────────────────────────────────────────────────────────┘
```

---

## Intent Types & Execution Model

| Intent | Example | Execution | Interactive | Duration |
|--------|---------|-----------|-------------|----------|
| `FixTicketIntent` | "fix #65 in todo-list" | K8s Job | Yes | Minutes |
| `ListTicketsIntent` | "list tickets in todo-list" | Direct (Dispatcher) | No | <5s |
| `CreateTicketIntent` | "create ticket 'Add logging' in todo-list" | Direct (Dispatcher) | No | <5s |

---

## Communication Protocol (Redis Streams)

### Streams
- `job:{jobId}:out` — Agent → Dispatcher (progress, questions, completion)
- `job:{jobId}:in`  — Dispatcher → Agent (user answers)
- `conversation:{channelId}` — Active job mapping (which job owns this channel)

### Message Schema

Agent → Dispatcher (`job:{id}:out`):
```json
{ "type": "progress", "step": 3, "total": 9, "text": "Analyzing code..." }
{ "type": "question", "questionId": "q1", "text": "Should I write tests?" }
{ "type": "done",     "prUrl": "https://...", "summary": "1 file changed" }
{ "type": "error",    "text": "Could not clone repository" }
```

Dispatcher → Agent (`job:{id}:in`):
```json
{ "type": "answer", "questionId": "q1", "content": "yes" }
```

### Stream Lifecycle
- All keys use TTL of 2 hours (auto-cleanup after job ends)
- MAXLEN 1000 per stream (prevent unbounded growth)
- Consumer group `dispatcher` reads from `job:{id}:out`

---

## Redis Deployment (K8s)

Redis runs **without a PersistentVolume** initially:
- Messages are ephemeral (job lifetime = minutes)
- If Redis pod restarts, in-flight jobs are considered lost (K8s Jobs restart too)
- One Redis instance serves all concurrent jobs (lightweight)
- Upgrade path: add PVC + `appendonly yes` with one config change

```yaml
# redis-deployment.yaml (minimal, no PV)
apiVersion: apps/v1
kind: Deployment
metadata:
  name: redis
spec:
  replicas: 1
  template:
    spec:
      containers:
        - name: redis
          image: redis:7-alpine
          args: ["--maxmemory", "256mb", "--maxmemory-policy", "allkeys-lru"]
          ports:
            - containerPort: 6379
```

---

## K8s Job per Request

Each "fix ticket" request spawns a K8s Job:

```yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: agentsmith-job-{jobId}
spec:
  ttlSecondsAfterFinished: 300
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: agentsmith
          image: agentsmith:latest
          args: ["--headless", "--job-id", "{jobId}", "--redis-url", "redis://redis:6379", "fix #{ticketId} in {project}"]
          env:
            - name: ANTHROPIC_API_KEY
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: anthropic-api-key
            - name: AZURE_DEVOPS_TOKEN
              valueFrom:
                secretKeyRef:
                  name: agentsmith-secrets
                  key: azure-devops-token
```

---

## New CLI Parameters (Agent Container)

```
--job-id       <guid>    Correlation ID for Redis Streams communication
--redis-url    <url>     Redis connection string (redis://host:port)
--channel-id   <string>  Source channel (for context, not used by agent)
--platform     <string>  Source platform (slack|teams|whatsapp)
```

When `--job-id` is set:
- Progress is published to `job:{jobId}:out` instead of Console only
- Questions are published to `job:{jobId}:out` and agent blocks on `job:{jobId}:in`
- Completion publishes `done` message and agent exits

Without `--job-id`: existing behavior (Console only, interactive stdin).

---

## Dispatcher Service

New project: `src/AgentSmith.Server`

```
AgentSmith.Server/
  Program.cs                    # ASP.NET Core Minimal API entry point
  Services/
    ChatIntentParser.cs         # "fix #65 in todo-list" → FixTicketIntent
    JobSpawner.cs               # Creates K8s Jobs via KubernetesClient
    ConversationStateManager.cs # Redis: jobId ↔ channelId mapping
    FastCommandExecutor.cs      # list/create without K8s job
    MessageBusListener.cs       # Redis consumer group, routes to platform
  Adapters/
    IplatformAdapter.cs         # SendMessage, SendProgress, AskQuestion
    SlackAdapter.cs             # Slack Web API
    TeamsAdapter.cs             # Bot Framework / Incoming Webhook
  Models/
    ChatIntent.cs               # FixTicketIntent, ListTicketsIntent, etc.
    BusMessage.cs               # Progress, Question, Done, Answer
    ConversationState.cs        # jobId, channelId, userId, platform, startedAt
```

---

## IProgressReporter Extension

Currently `IProgressReporter` only writes to Console. In Phase 18 it gets a
second implementation that publishes to Redis Streams.

```
IProgressReporter
  ├── ConsoleProgressReporter   (existing - local/CLI mode)
  └── RedisProgressReporter     (new - publishes to job:{id}:out stream)
```

The agent resolves the correct implementation at startup based on whether
`--job-id` is present. Composite reporter (Console + Redis) is supported
so Docker logs remain visible for debugging.

---

## Interactive Question/Answer Flow

```
Agent needs clarification
        │
        ▼
RedisProgressReporter.AskQuestionAsync(questionId, text)
  → XADD job:{id}:out { type:"question", questionId, text }
  → XREAD BLOCK 300000 job:{id}:in   (blocks up to 5 minutes)
        │
        │  (meanwhile in Dispatcher)
        │  MessageBusListener reads question
        │  → SlackAdapter.AskQuestion(text, buttons=[Yes/No/Cancel])
        │  → User clicks button in Slack
        │  → SlackAdapter receives interaction callback
        │  → XADD job:{id}:in { type:"answer", questionId, content }
        │
        ▼
Agent receives answer → continues execution
```

Timeout (no answer in 5 min): agent proceeds with default or aborts.

---

## Phase 18 Steps

| Step | File | Description |
|------|------|-------------|
| 18-1 | `phase18-redis-bus.md` | IMessageBus, RedisMessageBus, stream protocol |
| 18-2 | `phase18-progress-reporter.md` | RedisProgressReporter, CompositeProgressReporter |
| 18-3 | `phase18-cli-extensions.md` | --job-id, --redis-url CLI params, DI wiring |
| 18-4 | `phase18-dispatcher.md` | Dispatcher service, ChatIntentParser, JobSpawner |
| 18-5 | `phase18-conversation-state.md` | ConversationStateManager, Redis key schema |
| 18-6 | `phase18-slack-adapter.md` | Slack Events API, chat.postMessage, interactivity |
| 18-7 | `phase18-teams-adapter.md` | Bot Framework or Power Automate connector |
| 18-8 | `phase18-k8s-manifests.md` | K8s Deployment (Dispatcher), Job template, Secrets |

---

## What Stays Unchanged

- Local CLI mode (`dotnet run` / `docker run` without `--job-id`) works exactly as before
- All existing providers (GitHub, Azure DevOps, Jira, GitLab) unchanged
- All pipeline commands unchanged
- Config format (`agentsmith.yml`) unchanged
- Headless mode (`--headless`) unchanged

Phase 18 is purely additive. No existing behavior is modified.

---

## Dependencies

- `StackExchange.Redis` — Redis client for .NET
- `KubernetesClient` (`k8s-client`) — K8s Job spawning
- `Microsoft.AspNetCore` — Dispatcher API host
- Slack: raw HTTP to `https://slack.com/api/*` (no SDK needed)
- Teams: `Microsoft.Bot.Builder` or simple incoming webhook URL

---

## Success Criteria

- [ ] Slack message "fix #54 in agent-smith-test" → K8s Job spawned
- [ ] Progress messages appear in Slack channel in real time (1/9, 2/9, ...)
- [ ] Agent question appears as Slack message with Yes/No buttons
- [ ] User clicks button → Agent continues
- [ ] PR URL posted to Slack on completion
- [ ] "list tickets in agent-smith-test" → ticket list in Slack, no K8s Job
- [ ] Local `docker run` without --job-id still works as before
- [ ] Redis runs without PV, cleans up after job TTL

---

# Phase 18 – Step 5: ConversationStateManager

## Goal

Track which K8s job is currently running for each chat channel.
Enables the dispatcher to route Redis Stream messages back to the correct
channel, and to prevent duplicate jobs from being spawned.

---

## Files

- `src/AgentSmith.Server/Models/ConversationState.cs`
- `src/AgentSmith.Server/Services/ConversationStateManager.cs`

---

## ConversationState (Model)

```csharp
namespace AgentSmith.Server.Models;

/// <summary>
/// Tracks an active agent job linked to a specific chat channel.
/// Stored in Redis with TTL = 2 hours.
/// Key: conversation:{platform}:{channelId}
/// </summary>
public sealed record ConversationState
{
    public required string JobId { get; init; }
    public required string ChannelId { get; init; }
    public required string UserId { get; init; }
    public required string Platform { get; init; }
    public required string Project { get; init; }
    public required int TicketId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// The questionId currently waiting for an answer, if any.
    /// Null when no question is pending.
    /// </summary>
    public string? PendingQuestionId { get; init; }

    public ConversationState WithPendingQuestion(string questionId) =>
        this with { PendingQuestionId = questionId };

    public ConversationState ClearPendingQuestion() =>
        this with { PendingQuestionId = null };
}
```

---

## ConversationStateManager

```csharp
using System.Text.Json;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Server.Services;

/// <summary>
/// Manages active conversation state in Redis.
/// Maps a chat channel to its currently running K8s job.
///
/// Key schema:
///   conversation:{platform}:{channelId}  → JSON(ConversationState)
///   job-index:{jobId}                    → conversation key (reverse lookup)
///
/// TTL: 2 hours (auto-cleanup after job ends or times out).
/// </summary>
public sealed class ConversationStateManager(
    IConnectionMultiplexer redis,
    ILogger<ConversationStateManager> logger)
{
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Persists a new conversation state for the given channel.
    /// Overwrites any existing state (one active job per channel).
    /// </summary>
    public async Task SetAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(state.Platform, state.ChannelId);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        await db.StringSetAsync(key, json, StateTtl);
        logger.LogInformation(
            "Stored conversation state for channel {ChannelId} on {Platform}: job {JobId}",
            state.ChannelId, state.Platform, state.JobId);
    }

    /// <summary>
    /// Retrieves the active conversation state for a channel.
    /// Returns null if no active job exists.
    /// </summary>
    public async Task<ConversationState?> GetAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var key = BuildKey(platform, channelId);
        var json = await db.StringGetAsync(key);
        if (json.IsNullOrEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for channel {ChannelId}", channelId);
            return null;
        }
    }

    /// <summary>
    /// Reverse lookup: finds a channel's state by job ID.
    /// Uses a secondary index key stored alongside the primary state.
    /// </summary>
    public async Task<ConversationState?> GetByJobIdAsync(string jobId,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var channelKey = await db.StringGetAsync(JobIndexKey(jobId));
        if (channelKey.IsNullOrEmpty) return null;
        var json = await db.StringGetAsync((string)channelKey!);
        if (json.IsNullOrEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize<ConversationState>(json!, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize conversation state for job {JobId}", jobId);
            return null;
        }
    }

    /// <summary>
    /// Stores a secondary index: jobId → channel key.
    /// Must be called alongside SetAsync to enable GetByJobIdAsync.
    /// </summary>
    public async Task IndexJobAsync(ConversationState state,
        CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var channelKey = BuildKey(state.Platform, state.ChannelId);
        await db.StringSetAsync(JobIndexKey(state.JobId), channelKey.ToString(), StateTtl);
    }

    /// <summary>Updates the pending question on an existing state.</summary>
    public async Task SetPendingQuestionAsync(string platform, string channelId,
        string questionId, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null)
        {
            logger.LogWarning(
                "Cannot set pending question: no state for channel {ChannelId}", channelId);
            return;
        }
        await SetAsync(existing.WithPendingQuestion(questionId), cancellationToken);
    }

    /// <summary>Clears the pending question from an existing state.</summary>
    public async Task ClearPendingQuestionAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        if (existing is null) return;
        await SetAsync(existing.ClearPendingQuestion(), cancellationToken);
    }

    /// <summary>
    /// Removes all state for a channel after the job completes.
    /// Also removes the job-id index entry.
    /// </summary>
    public async Task RemoveAsync(string platform, string channelId,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(platform, channelId, cancellationToken);
        var db = redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(platform, channelId).ToString());
        if (existing is not null)
            await db.KeyDeleteAsync(JobIndexKey(existing.JobId).ToString());
        logger.LogInformation(
            "Removed conversation state for channel {ChannelId} on {Platform}",
            channelId, platform);
    }

    // --- Helpers ---

    private static RedisKey BuildKey(string platform, string channelId) =>
        $"conversation:{platform.ToLowerInvariant()}:{channelId}";

    private static RedisKey JobIndexKey(string jobId) =>
        $"job-index:{jobId}";
}
```

---

## Redis Key Schema

| Key | Value | TTL | Purpose |
|-----|-------|-----|---------|
| `conversation:{platform}:{channelId}` | `JSON(ConversationState)` | 2h | Primary state lookup by channel |
| `job-index:{jobId}` | conversation key string | 2h | Reverse lookup: job → channel |

---

## Usage in Program.cs

```csharp
// After spawning a job:
var state = new ConversationState
{
    JobId = jobId,
    ChannelId = intent.ChannelId,
    UserId = intent.UserId,
    Platform = intent.Platform,
    Project = intent.Project,
    TicketId = intent.TicketId,
    StartedAt = DateTimeOffset.UtcNow
};
await stateManager.SetAsync(state);
await stateManager.IndexJobAsync(state);

// When a Redis bus message arrives (in MessageBusListener):
var state = await stateManager.GetByJobIdAsync(message.JobId);
// route to state.Platform adapter, post to state.ChannelId

// When a Slack button is clicked:
var state = await stateManager.GetAsync("slack", channelId);
// check state.PendingQuestionId matches
await messageBus.PublishAnswerAsync(state.JobId, questionId, answer);
await stateManager.ClearPendingQuestionAsync("slack", channelId);

// After job Done or Error:
await stateManager.RemoveAsync(state.Platform, state.ChannelId);
```

---

## DI Registration

```csharp
builder.Services.AddSingleton<ConversationStateManager>();
```

---

## Design Notes

- **One job per channel**: `SetAsync` overwrites existing state. Before spawning, check `GetAsync` and reject if a job is already running.
- **No locking needed**: Redis `SET` is atomic. Race conditions between two simultaneous messages in the same channel are acceptable (last write wins, both would reject on the "already running" check).
- **TTL as safety net**: If a job crashes without publishing Done/Error, the state expires automatically after 2 hours, allowing new jobs to start.
- **Secondary index**: `job-index:{jobId}` lets the `MessageBusListener` route bus messages back to channels without scanning all keys.

---

## Definition of Done

- [ ] `ConversationState` record compiles with all required properties
- [ ] `ConversationStateManager` reads/writes/deletes from Redis correctly
- [ ] Secondary index is stored alongside primary state
- [ ] Pending question can be set and cleared atomically
- [ ] TTL is applied on every write
- [ ] `dotnet build` clean

---

# Phase 18 – Step 3: Dispatcher Service

## What This Step Builds

The `AgentSmith.Server` project: a long-running ASP.NET Core Minimal API that acts as the
bridge between chat platforms (Slack, Teams) and ephemeral agent K8s Jobs.

New project: `src/AgentSmith.Server/`

---

## Entry Point: Program.cs

ASP.NET Core Minimal API with three endpoints:

| Endpoint | Purpose |
|----------|---------|
| `GET /health` | Liveness check (returns `{ status: "ok", timestamp }`) |
| `POST /slack/events` | Receives Slack Events API payloads (messages, app_mention) |
| `POST /slack/interact` | Receives Slack interactive component callbacks (button clicks) |

### DI Registration

```csharp
// Redis
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisUrl));

// Core services
builder.Services.AddSingleton<IMessageBus, RedisMessageBus>();
builder.Services.AddSingleton<ConversationStateManager>();
builder.Services.AddSingleton<ChatIntentParser>();

// Job spawner
builder.Services.AddSingleton<JobSpawnerOptions>(...);
builder.Services.AddSingleton<JobSpawner>();

// Platform adapters
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SlackAdapterOptions>(...);
builder.Services.AddSingleton<SlackAdapter>();
builder.Services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());

// Background listener
builder.Services.AddSingleton<MessageBusListener>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MessageBusListener>());

// Agent Smith infrastructure (list/create fast-path)
builder.Services.AddAgentSmithInfrastructure();
```

### Slack Signature Verification

Every incoming Slack request is verified using HMAC-SHA256 before processing:

```
X-Slack-Request-Timestamp  →  reject if older than 5 minutes (replay attack)
X-Slack-Signature          →  v0=<hmac(signing_secret, "v0:{ts}:{body}")>
```

If `SLACK_SIGNING_SECRET` is empty, verification is skipped (local dev without ngrok).

### POST /slack/events Flow

```
Receive payload
  └─ Verify signature
  └─ Parse JSON
  └─ type == "url_verification" → return { challenge } (Slack setup handshake)
  └─ type == "event_callback"
       └─ event.type == "message" | "app_mention"
       └─ Skip bot messages (bot_id present)
       └─ Strip @mention prefix from text
       └─ Fire-and-forget: HandleSlackMessageAsync(text, userId, channelId)
       └─ Return 200 immediately (Slack requires <3s response)
```

### POST /slack/interact Flow

```
Receive form-encoded payload (payload=<url-encoded JSON>)
  └─ Verify signature
  └─ Parse JSON from payload field
  └─ type == "block_actions"
  └─ Extract: userId, channelId, actionId, value
  └─ actionId format: "{questionId}:yes" | "{questionId}:no"
  └─ Fire-and-forget: HandleSlackInteractionAsync(channelId, questionId, answer, payload)
  └─ Return 200 immediately
```

---

## ChatIntentParser

Parses natural-language messages into typed `ChatIntent` instances using compiled regex.
No LLM call. Runs in microseconds.

```
src/AgentSmith.Server/Services/ChatIntentParser.cs
```

### Supported Patterns

| Pattern | Intent |
|---------|--------|
| `fix #65 in todo-list` | `FixTicketIntent` |
| `@Agent Smith fix #65 in todo-list` | `FixTicketIntent` (mention stripped) |
| `list tickets in todo-list` | `ListTicketsIntent` |
| `list ticket for todo-list` | `ListTicketsIntent` |
| `create ticket "Add logging" in todo-list` | `CreateTicketIntent` |
| `create ticket "Title" in project "Description"` | `CreateTicketIntent` (with description) |
| anything else | `UnknownIntent` |

### Regex Patterns

```csharp
// Fix: "fix #65 in todo-list"
private static readonly Regex FixPattern = new(
    @"^fix\s+#(\d+)\s+in\s+(\S+)$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

// List: "list tickets in todo-list" | "list ticket for todo-list"
private static readonly Regex ListPattern = new(
    @"^list\s+tickets?\s+(?:in|for)\s+(\S+)$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

// Create (no description): create ticket "Title" in project
private static readonly Regex CreatePattern = new(
    @"^create\s+ticket\s+[""'](.+?)[""']\s+in\s+(\S+)$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);

// Create (with description): create ticket "Title" in project "Description"
private static readonly Regex CreateWithDescPattern = new(
    @"^create\s+ticket\s+[""'](.+?)[""']\s+in\s+(\S+)\s+[""'](.+?)[""']$",
    RegexOptions.IgnoreCase | RegexOptions.Compiled);
```

### Parse Priority

`CreateWithDesc` is tried before `Create` (longer pattern wins).

---

## JobSpawner

Creates K8s `batch/v1 Job` objects for each `FixTicketIntent`.

```
src/AgentSmith.Server/Services/JobSpawner.cs
```

### Job ID Generation

```csharp
var jobId = Guid.NewGuid().ToString("N")[..12]; // e.g. "a3f8c1d92e04"
var jobName = $"agentsmith-{jobId}";            // e.g. "agentsmith-a3f8c1d92e04"
```

### Job Spec

```yaml
apiVersion: batch/v1
kind: Job
spec:
  ttlSecondsAfterFinished: 300   # auto-delete 5 minutes after completion
  backoffLimit: 0                # no retries (agent manages its own retry)
  template:
    spec:
      restartPolicy: Never
      containers:
        - name: agentsmith
          image: agentsmith:latest
          args:
            - "--headless"
            - "--job-id",  "{jobId}"
            - "--redis-url", "$(REDIS_URL)"
            - "--platform", "{platform}"
            - "--channel-id", "{channelId}"
            - "fix #{ticketId} in {project}"
          env:
            - ANTHROPIC_API_KEY  (from secret)
            - AZURE_DEVOPS_TOKEN (from secret)
            - GITHUB_TOKEN       (from secret)
            - OPENAI_API_KEY     (from secret, optional)
            - GEMINI_API_KEY     (from secret, optional)
            - REDIS_URL          (from secret)
            - JOB_ID, TICKET_ID, PROJECT, CHANNEL_ID, USER_ID, PLATFORM (plain values)
          resources:
            requests: cpu=250m, memory=512Mi
            limits:   cpu=1000m, memory=1Gi
```

### JobSpawnerOptions

```csharp
public sealed class JobSpawnerOptions
{
    public string Namespace { get; set; } = "default";
    public string Image { get; set; } = "agentsmith:latest";
    public string ImagePullPolicy { get; set; } = "IfNotPresent";
    public string SecretName { get; set; } = "agentsmith-secrets";
    public int TtlSecondsAfterFinished { get; set; } = 300;
}
```

Populated from environment variables in `Program.cs`:

```csharp
builder.Services.AddSingleton(new JobSpawnerOptions
{
    Namespace        = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? "default",
    Image            = Environment.GetEnvironmentVariable("AGENTSMITH_IMAGE") ?? "agentsmith:latest",
    ImagePullPolicy  = Environment.GetEnvironmentVariable("IMAGE_PULL_POLICY") ?? "IfNotPresent",
    SecretName       = Environment.GetEnvironmentVariable("K8S_SECRET_NAME") ?? "agentsmith-secrets",
    TtlSecondsAfterFinished = 300
});
```

---

## MessageBusListener

Background service (`IHostedService`) that subscribes to Redis Streams for active jobs and
relays messages to the appropriate platform adapter.

```
src/AgentSmith.Server/Services/MessageBusListener.cs
```

### Subscription Model

One `Task` per active job, tracked in a `Dictionary<string, Task>`.
A periodic cleanup loop removes completed tasks every 30 seconds.

```csharp
public async Task TrackJobAsync(string jobId, CancellationToken cancellationToken)
```

Called immediately after `JobSpawner.SpawnAsync` succeeds. Starts the subscription task.

### Message Routing

```
BusMessageType.Progress → adapter.SendProgressAsync(channelId, step, total, commandName)
BusMessageType.Question → adapter.AskQuestionAsync(channelId, questionId, text)
                          stateManager.SetPendingQuestionAsync(...)
BusMessageType.Done     → adapter.SendDoneAsync(channelId, summary, prUrl)
                          stateManager.RemoveAsync(...)
                          messageBus.CleanupJobAsync(jobId)
BusMessageType.Error    → adapter.SendErrorAsync(channelId, text)
                          stateManager.RemoveAsync(...)
                          messageBus.CleanupJobAsync(jobId)
```

State lookup uses `ConversationStateManager.GetByJobIdAsync(jobId)` which does a reverse
lookup via the job-index Redis key.

---

## Intent Handlers

### HandleFixTicketAsync

```
1. Check for existing active job in this channel → reject with message if found
2. Send ":rocket: Starting Agent Smith for ticket #{id} in {project}..."
3. spawner.SpawnAsync(intent) → jobId
4. stateManager.SetAsync(state)
5. stateManager.IndexJobAsync(state)
6. listener.TrackJobAsync(jobId)
```

### HandleListTicketsAsync (fast-path, no K8s Job)

```
1. configLoader.LoadConfig("config/agentsmith.yml")
2. ticketFactory.Create(projectConfig.Tickets)
3. ticketProvider.ListOpenAsync()
4. Format and post to Slack (max 20 tickets)
```

### HandleCreateTicketAsync (fast-path, no K8s Job)

```
1. configLoader.LoadConfig("config/agentsmith.yml")
2. ticketFactory.Create(projectConfig.Tickets)
3. ticketProvider.CreateAsync(title, description)
4. Post confirmation with fix command hint
```

---

## Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `REDIS_URL` | Yes | `redis://redis:6379` |
| `SLACK_BOT_TOKEN` | Yes | `xoxb-...` |
| `SLACK_SIGNING_SECRET` | Yes (prod) | Found in Slack Basic Information |
| `K8S_NAMESPACE` | No | Default: `default` |
| `AGENTSMITH_IMAGE` | No | Default: `agentsmith:latest` |
| `IMAGE_PULL_POLICY` | No | Default: `IfNotPresent` |
| `K8S_SECRET_NAME` | No | Default: `agentsmith-secrets` |
| `ASPNETCORE_URLS` | No | Default: `http://+:8080` |

---

## NuGet Packages

```xml
<PackageReference Include="StackExchange.Redis" Version="2.8.*" />
<PackageReference Include="KubernetesClient" Version="13.*" />
<PackageReference Include="Microsoft.AspNetCore.App" />  <!-- framework reference -->
```

---

## Definition of Done

- [ ] `GET /health` returns `{ status: "ok" }`
- [ ] `POST /slack/events` handles `url_verification` challenge
- [ ] `POST /slack/events` parses `message` and `app_mention` events
- [ ] Bot messages are ignored (no echo loops)
- [ ] `FixTicketIntent` spawns a K8s Job and tracks it via MessageBusListener
- [ ] `ListTicketsIntent` returns ticket list without spawning a job
- [ ] `CreateTicketIntent` creates a ticket and confirms in Slack
- [ ] `UnknownIntent` sends a help message
- [ ] `POST /slack/interact` routes button clicks to the correct job's inbound stream
- [ ] Slack signature verification passes for real Slack requests
- [ ] `dotnet build` passes without warnings

---

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

**Location:** `src/AgentSmith.Server/Services/RedisProgressReporter.cs`

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

## DI Wiring (AgentSmith.Cli)

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
| `src/AgentSmith.Server/Services/RedisProgressReporter.cs` | **New** |
| `src/AgentSmith.Cli/Program.cs` | Add Redis branch in DI wiring |

---

## Definition of Done

- [ ] `RedisProgressReporter` implements all 4 methods of `IProgressReporter`
- [ ] Timeout handling: returns `defaultAnswer` when no answer within 5 min
- [ ] Answer content is parsed case-insensitively
- [ ] `AskYesNoAsync` logs the received content and final bool result
- [ ] DI wiring selects `RedisProgressReporter` when `--job-id` is set
- [ ] `dotnet build` clean

---

# Phase 18 – Step 1: IMessageBus + RedisMessageBus + BusMessage Protocol

## Goal

Implement the Redis Streams communication layer between agent containers and the
dispatcher service. This is the backbone of the Phase 18 architecture: every
progress update, question, answer and completion message flows through this bus.

---

## Files to Create

### `src/AgentSmith.Server/Models/BusMessage.cs`

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

### `src/AgentSmith.Server/Services/IMessageBus.cs`

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

### `src/AgentSmith.Server/Services/RedisMessageBus.cs`

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

Already referenced in `AgentSmith.Server.csproj`.

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

---

# Phase 18 – Step 6: Slack Adapter

## Goal

Implement the Slack platform adapter that translates generic dispatcher actions
into Slack Web API calls. No Slack SDK — raw HTTP only.

---

## Files

### `src/AgentSmith.Server/Adapters/IPlatformAdapter.cs`

Common contract for all chat platform adapters:

```csharp
namespace AgentSmith.Server.Adapters;

public interface IPlatformAdapter
{
    string Platform { get; }

    Task SendMessageAsync(string channelId, string text,
        CancellationToken cancellationToken = default);

    Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken = default);

    Task<string> AskQuestionAsync(string channelId, string questionId, string text,
        CancellationToken cancellationToken = default);

    Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken = default);

    Task SendErrorAsync(string channelId, string text,
        CancellationToken cancellationToken = default);

    Task UpdateQuestionAnsweredAsync(string channelId, string messageId, string questionText,
        string answer, CancellationToken cancellationToken = default);
}
```

---

### `src/AgentSmith.Server/Adapters/SlackAdapter.cs`

- `Platform` returns `"slack"`
- All API calls POST JSON to `https://slack.com/api/{method}`
- Bearer token from `SlackAdapterOptions.BotToken`
- No SDK dependency — uses `HttpClient` directly

#### `SendMessageAsync`
Posts plain text via `chat.postMessage`.

#### `SendProgressAsync`
Posts a progress bar message:
```
:gear: *[3/9]* `AnalyzeCodeCommand`
`[███░░░░░░░]` 3/9
```
On the final step, uses `:white_check_mark:` emoji instead of `:gear:`.

Progress bar formula: fill `█` for completed steps, `░` for remaining, bar length = 10.

#### `AskQuestionAsync`
Posts a Block Kit message with two buttons (Yes / No):

```json
{
  "channel": "...",
  "text": ":thought_balloon: *Question text*",
  "blocks": [
    {
      "type": "section",
      "text": { "type": "mrkdwn", "text": ":thought_balloon: *Question text*" }
    },
    {
      "type": "actions",
      "block_id": "{questionId}",
      "elements": [
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "Yes :white_check_mark:" },
          "style": "primary",
          "value": "yes",
          "action_id": "{questionId}:yes"
        },
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "No :x:" },
          "style": "danger",
          "value": "no",
          "action_id": "{questionId}:no"
        }
      ]
    }
  ]
}
```

Returns the message timestamp (`ts`) from the Slack response — used later
to update/replace the message when the user answers.

#### `SendDoneAsync`
Posts a completion message:
```
:rocket: *Done!* {summary}
:link: <{prUrl}|View Pull Request>
```
If `prUrl` is null, omits the link line.

#### `SendErrorAsync`
Posts an error message with the stack in a code block:
```
:x: *Agent Smith encountered an error:*
```{errorText}```
```

#### `UpdateQuestionAnsweredAsync`
Calls `chat.update` to replace the button message with the answer:
```
:thought_balloon: *{questionText}*
:white_check_mark: Answered: *yes*
```
Removes all blocks (no more buttons). This is called after the user clicks a button.

---

### `SlackAdapterOptions`

```csharp
public sealed class SlackAdapterOptions
{
    public string BotToken { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
}
```

Populated from environment variables in `Program.cs`:

```csharp
builder.Services.AddSingleton(new SlackAdapterOptions
{
    BotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? string.Empty,
    SigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty
});
builder.Services.AddSingleton<SlackAdapter>();
builder.Services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());
```

---

## Slack Signature Verification

All incoming Slack requests are verified using HMAC-SHA256 before processing.

Slack sends two headers:
- `X-Slack-Request-Timestamp` — Unix timestamp (reject if > 5 minutes old)
- `X-Slack-Signature` — `v0=<hex(HMAC-SHA256(signingSecret, "v0:{timestamp}:{body}"))>`

Verification steps:
1. Read raw body (must happen before `ctx.Request.Body` is consumed by middleware)
2. Check timestamp age (prevent replay attacks)
3. Compute HMAC-SHA256 with `SLACK_SIGNING_SECRET`
4. Compare computed signature to `X-Slack-Signature` (constant-time comparison)

If `SLACK_SIGNING_SECRET` is empty (local dev), skip verification and return `true`.

```csharp
static async Task<bool> VerifySlackSignatureAsync(HttpRequest request, string signingSecret)
{
    if (string.IsNullOrEmpty(signingSecret)) return true;

    if (!request.Headers.TryGetValue("X-Slack-Request-Timestamp", out var tsValue)
        || !long.TryParse(tsValue, out var timestamp))
        return false;

    var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp;
    if (Math.Abs(age) > 300) return false; // replay attack protection

    // body must be buffered before calling this method
    var body = (string)request.HttpContext.Items["rawBody"]!;
    var sigBase = $"v0:{timestamp}:{body}";

    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(sigBase));
    var computed = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

    var received = request.Headers["X-Slack-Signature"].ToString();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(computed),
        Encoding.UTF8.GetBytes(received));
}
```

---

## Endpoints in `Program.cs`

### `POST /slack/events`

Handles Slack Events API payloads:

1. Verify signature
2. Parse JSON body
3. If `type == "url_verification"` → return `{ challenge }` immediately
4. If `type != "event_callback"` → return 200 OK
5. If `event.type` is neither `"message"` nor `"app_mention"` → return 200 OK
6. If `event.bot_id` is set → skip (ignore bot messages)
7. Strip bot mention prefix: `<@BOTID>` at start of text (for `app_mention` events)
8. Fire-and-forget: call `HandleSlackMessageAsync` in a background task
9. Return 200 OK immediately (Slack requires a response within 3 seconds)

**Strip mention helper:**
```csharp
static string StripMention(string text)
{
    var stripped = Regex.Replace(text.Trim(), @"^<@[A-Z0-9]+>\s*", string.Empty);
    return stripped.Trim();
}
```

### `POST /slack/interact`

Handles interactive component payloads (button clicks):

1. Verify signature
2. Read form body: `payload=<url-encoded JSON>`
3. Parse `payload` JSON
4. Check `type == "block_actions"`
5. Extract: `user.id`, `channel.id`, `actions[0].action_id`, `actions[0].value`
6. Parse `action_id` → `"{questionId}:{answer}"` (split on last `:`)
7. Fire-and-forget: call `HandleSlackInteractionAsync`
8. Return 200 OK immediately (removes Slack's loading spinner)

---

## Message Handlers

### `HandleSlackMessageAsync`

```
parse intent
→ FixTicketIntent → HandleFixTicketAsync
→ ListTicketsIntent → HandleListTicketsAsync
→ CreateTicketIntent → HandleCreateTicketAsync
→ UnknownIntent → send help message
```

### `HandleFixTicketAsync`

1. Check if there's already an active job for this channel (via `ConversationStateManager`)
   - If yes: post "already running" message and return
2. Post `:rocket: Starting Agent Smith for ticket #N in project...`
3. `JobSpawner.SpawnAsync(intent)` → get `jobId`
4. Create `ConversationState` and persist via `stateManager.SetAsync` + `IndexJobAsync`
5. `MessageBusListener.TrackJobAsync(jobId)`

### `HandleListTicketsAsync`

1. Load config via `IConfigurationLoader`
2. Resolve project config
3. Create ticket provider via `ITicketProviderFactory`
4. Call `ListOpenAsync()`
5. Format and post up to 20 tickets

### `HandleCreateTicketAsync`

1. Load config, resolve project
2. Create ticket via `ITicketProviderFactory` + `CreateAsync(title, description)`
3. Post confirmation with the new ticket ID and a ready-to-use `fix #N in project` command

### `HandleSlackInteractionAsync`

1. Resolve `ConversationState` for the channel
2. Validate `PendingQuestionId == questionId`
3. `messageBus.PublishAnswerAsync(jobId, questionId, answer)`
4. `adapter.UpdateQuestionAnsweredAsync(...)` to remove buttons from the message
5. `stateManager.ClearPendingQuestionAsync(...)`

---

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `SLACK_BOT_TOKEN` | Bot User OAuth Token (`xoxb-...`) |
| `SLACK_SIGNING_SECRET` | Signing secret for request verification |

Both are optional for local development (no verification, no Slack posts).

---

## Definition of Done

- [ ] `SlackAdapter` implements `IPlatformAdapter`
- [ ] `IPlatformAdapter` registered in DI (both concrete and interface)
- [ ] `POST /slack/events` handles messages + app mentions
- [ ] `POST /slack/interact` handles button clicks
- [ ] Signature verification works with real Slack credentials
- [ ] Empty `SLACK_SIGNING_SECRET` skips verification (local dev)
- [ ] Bot messages are ignored (no echo loops)
- [ ] Progress bar renders correctly in Slack
- [ ] Question buttons appear and disappear after answer