# Chat Gateway (Slack / Teams)

The Dispatcher acts as a gateway between chat platforms and Agent Smith. Users trigger pipelines from Slack or Teams, and progress streams back in real time.

## Architecture

```
┌─────────┐    HTTP     ┌─────────────┐   Redis    ┌──────────┐
│  Slack   │───────────▶│ Dispatcher   │◀──────────▶│  Redis   │
│  Events  │            │             │            │          │
└─────────┘            │  ┌─────────┐ │            └──────────┘
                        │  │ Intent  │ │                 ▲
                        │  │ Engine  │ │                 │
                        │  └────┬────┘ │                 │
                        │       │      │            progress
                        │  ┌────▼────┐ │            pub/sub
                        │  │   Job   │ │                 │
                        │  │ Spawner │ │            ┌────┴─────┐
                        │  └────┬────┘ │            │  Agent   │
                        └───────┼──────┘            │  (Job)   │
                                │                   └──────────┘
                           creates K8s Job
                           or Docker container
```

## How It Works

1. **User sends a message** in Slack: `fix #42 in my-api`
2. **Platform Adapter** receives the event via Slack Events API
3. **Intent Engine** parses the message (regex patterns, LLM fallback for ambiguous input)
4. **Project Resolver** maps `my-api` to a configured project
5. **Job Spawner** creates an ephemeral container (K8s Job or Docker container)
6. **Agent runs** the pipeline (`fix --repo ... --ticket 42 --headless`)
7. **Progress streams** via Redis pub/sub back to the Dispatcher
8. **Dispatcher relays** updates to the Slack channel in real time
9. **Container terminates** when the pipeline completes

## Supported Platforms

| Platform | Adapter | Status |
|----------|---------|--------|
| Slack    | `SlackAdapter` | Production-ready — [setup guide](../setup/slack.md) |
| Teams    | `TeamsAdapter` | Beta — [setup guide](../setup/teams.md) |

## Slack Setup

### 1. Create a Slack App

1. Go to [api.slack.com/apps](https://api.slack.com/apps) and create a new app
2. Under **OAuth & Permissions**, add these scopes:
    - `chat:write`
    - `commands`
    - `app_mentions:read`
    - `im:history`
    - `channels:history`
3. Under **Event Subscriptions**, enable events and set the request URL to `https://your-host:6000/slack/events`
4. Subscribe to bot events: `app_mention`, `message.im`
5. Install the app to your workspace

### 2. Configure Secrets

```bash
# .env
SLACK_BOT_TOKEN=xoxb-your-bot-token
SLACK_SIGNING_SECRET=your-signing-secret
ANTHROPIC_API_KEY=sk-ant-...
GITHUB_TOKEN=ghp_...
```

### 3. Deploy the Dispatcher

**Docker Compose:**

```bash
docker compose up -d dispatcher redis
```

**Kubernetes:**

```bash
kubectl apply -k k8s/overlays/prod
```

## Chat Commands

### Natural Language

Users interact in natural language. The intent engine recognizes patterns like:

| Message | Parsed Intent |
|---------|--------------|
| `fix #42 in my-api` | Fix bug pipeline for ticket #42 in project `my-api` |
| `scan my-api for security issues` | Security scan pipeline for project `my-api` |
| `analyze the API of my-api` | API scan pipeline for project `my-api` |
| `help` | Show available commands |

### Slash Commands and Modals

The Dispatcher also supports structured input via Slack slash commands and modals:

- `/agentsmith fix` — Opens a modal to select project, ticket, and pipeline options
- `/agentsmith scan` — Opens a security scan modal

!!! tip "Ambiguous input"
    When the intent engine cannot determine the project or command, it asks for clarification in the thread. The conversation state is tracked per channel/thread.

## Intent Routing

The intent engine uses a two-stage approach:

1. **Regex patterns** for common, well-structured commands (fast, no API call)
2. **LLM-based parsing** (Claude Haiku) for ambiguous or natural language input

## Ephemeral Containers

Each request spawns an isolated container:

- **Kubernetes:** A `batch/v1` Job with `backoffLimit: 0` and TTL-based cleanup
- **Docker:** A container via the Docker socket with auto-remove

This ensures:

- **Isolation** — each request runs in its own environment
- **No shared state** — no cross-contamination between projects
- **Resource limits** — Kubernetes can enforce CPU/memory limits per Job
- **Automatic cleanup** — containers are removed after completion

## Progress Streaming

The agent publishes progress updates to Redis channels:

```
agentsmith:progress:{job-id}
```

The Dispatcher subscribes to these channels and forwards updates to the originating Slack channel/thread. Updates include:

- Pipeline step transitions (e.g., "Analyzing code...", "Generating plan...")
- Completion with PR link or scan results
- Error messages with context

## Orphan Job Detection

The Dispatcher includes an `OrphanJobDetector` that:

- Scans Redis for stale job states after restart (in-memory tracking is lost)
- Detects containers/Jobs that stopped without reporting completion
- Performs liveness checks on running containers
- Cleans up stale state and notifies the originating channel

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `SLACK_BOT_TOKEN` | Slack bot OAuth token | Slack |
| `SLACK_SIGNING_SECRET` | Slack request signing secret | Slack |
| `TEAMS_APP_ID` | Azure AD App Registration client ID | Teams |
| `TEAMS_APP_PASSWORD` | Azure AD App Registration client secret | Teams |
| `TEAMS_TENANT_ID` | Azure AD tenant ID | Teams |
| `REDIS_URL` | Redis connection | Yes |
| `SPAWNER_TYPE` | `kubernetes` or `docker` | Yes |
| `AGENTSMITH_IMAGE` | Image for spawned agents | Yes |
| `K8S_NAMESPACE` | Namespace for K8s Jobs | K8s only |
| `K8S_SECRET_NAME` | Secret to mount in Jobs | K8s only |
| `IMAGE_PULL_POLICY` | K8s image pull policy | K8s only |
