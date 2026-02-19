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

New project: `src/AgentSmith.Dispatcher`

```
AgentSmith.Dispatcher/
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