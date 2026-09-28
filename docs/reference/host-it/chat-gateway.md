# Chat Gateway (Slack / Teams)

The server doubles as a gateway between chat platforms and Agent Smith. Users trigger pipelines from Slack or Teams, and progress streams back into the thread. There is no separate process for it: `AgentSmith.Server` receives the chat events on the same port as the webhooks (8081).

## Architecture

```
┌─────────┐   HTTPS    ┌──────────────────────┐   Redis   ┌──────────┐
│  Slack  │───────────▶│  AgentSmith.Server   │◀─────────▶│  Redis   │
│  Teams  │            │                      │           └──────────┘
└─────────┘            │  platform adapter    │                ▲
                       │  intent engine       │           progress
                       │  job spawner         │                │
                       └──────────┬───────────┘           ┌────┴─────┐
                                  │                       │  run     │
                          K8s Job or Docker container ───▶│  (job)   │
                                                          └──────────┘
```

## How it works

1. **A user sends a message** in Slack: `fix #42 in my-api`.
2. **The platform adapter** receives it (Slack Events API at `/slack/events`, Teams at `/api/teams/messages`) and checks the platform's signature.
3. **The intent engine** parses the message: regex patterns first, and an LLM call on the agent's `reasoning` model for input the patterns don't match.
4. **The project resolver** maps `my-api` to a configured project.
5. **The job spawner** starts the run in its own container, a Kubernetes Job or a Docker container depending on `SPAWNER_TYPE`.
6. **Progress streams** through Redis back to the server, which relays it into the originating channel or thread.
7. **The container ends** when the pipeline completes.

## Supported platforms

| Platform | Adapter | Status |
|----------|---------|--------|
| Slack    | `SlackAdapter` | Production-ready — [setup guide](../setup/slack.md) |
| Teams    | `TeamsAdapter` | Beta — [setup guide](../setup/teams.md) |

## Slack setup

### 1. Create a Slack app

1. Go to [api.slack.com/apps](https://api.slack.com/apps) and create a new app.
2. Under **OAuth & Permissions**, add these scopes:
    - `chat:write`
    - `commands`
    - `app_mentions:read`
    - `im:history`
    - `channels:history`
3. Under **Event Subscriptions**, enable events and set the request URL to `https://your-host/slack/events`.
4. Under **Interactivity**, set the request URL to `https://your-host/slack/interact`, and point the slash command at `https://your-host/slack/commands`.
5. Subscribe to bot events: `app_mention`, `message.im`.
6. Install the app to your workspace.

### 2. Configure secrets

```bash
# .env next to the compose file, or the agentsmith-secrets Secret on Kubernetes
SLACK_BOT_TOKEN=xoxb-your-bot-token
SLACK_SIGNING_SECRET=your-signing-secret
```

### 3. Run the server

Nothing extra to deploy: the server from [docker-compose](../../host-it/docker-compose.md) or [Kubernetes](../../host-it/kubernetes.md) serves the Slack endpoints as soon as the two variables are set. Put a public HTTPS endpoint in front of port 8081 (the same one your webhooks use).

## Chat commands

### Messages

| Message | What it does |
|---------|--------------|
| `fix #42 in my-api` | Runs the `code` pipeline for ticket 42 in project `my-api` |
| `fix PROJ-42 in my-api` | The same with a Jira key; `ticket` and `fix` are optional (`#42`, `PROJ-42`) |
| `list tickets in my-api` | Lists the project's open tickets (Jira included) |
| `create ticket "Title" in my-api` | Files a ticket; a second quoted string adds a description |
| `security-review my-api` / `security-review PR#12 in my-api` | Runs a security review |
| `init my-api` | Runs `init-project` for the project (see [Onboarding](../setup/onboarding.md)) |
| `help` | Shows the available commands |

When the engine can't tell the project or the command, it asks in the thread. The conversation state is tracked per channel and thread.

### Slash command and modal

The slash command opens a modal where you pick a command and a project: Fix Bug, Fix Bug (no tests), Add Feature, Security Review, MAD Discussion, Legal Analysis, List Tickets, Create Ticket, Init Project.

## One container per request

Each request runs in its own container:

- **Kubernetes:** a `batch/v1` Job running the CLI image in the server's namespace.
- **Docker:** a container through the Docker socket, removed when it's done.

That keeps each run isolated, lets Kubernetes enforce CPU and memory limits, and cleans up after itself.

## Orphan job detection

The server runs an `OrphanJobDetector` that periodically looks for jobs whose container stopped without reporting completion, clears their state and tells the originating channel.

## Environment variables

| Variable | Description | Required |
|----------|-------------|----------|
| `SLACK_BOT_TOKEN` | Slack bot OAuth token | Slack |
| `SLACK_SIGNING_SECRET` | Slack request signing secret | Slack |
| `TEAMS_APP_ID` | Azure AD App Registration client ID | Teams |
| `TEAMS_APP_PASSWORD` | Azure AD App Registration client secret | Teams |
| `TEAMS_TENANT_ID` | Azure AD tenant ID | Teams |
| `REDIS_URL` | Redis connection, `host:port` | Yes |
| `SPAWNER_TYPE` | `kubernetes` (default) or `docker` | Yes |
| `K8S_NAMESPACE` | Namespace for spawned pods | K8s only |
| `K8S_SECRET_NAME` | Secret to mount in spawned pods | K8s only |
| `IMAGE_PULL_POLICY` | Image pull policy for spawned pods | K8s only |

The image a spawned run uses comes from `deployment.version` (and `deployment.registry`), set under **Configuration → Deployment**.
