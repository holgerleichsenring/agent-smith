# Phase 18 – Step 3: Dispatcher Service

## What This Step Builds

The `AgentSmith.Dispatcher` project: a long-running ASP.NET Core Minimal API that acts as the
bridge between chat platforms (Slack, Teams) and ephemeral agent K8s Jobs.

New project: `src/AgentSmith.Dispatcher/`

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
src/AgentSmith.Dispatcher/Services/ChatIntentParser.cs
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
src/AgentSmith.Dispatcher/Services/JobSpawner.cs
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
src/AgentSmith.Dispatcher/Services/MessageBusListener.cs
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