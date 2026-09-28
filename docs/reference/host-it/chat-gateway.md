# Chat Gateway (Slack / Teams)

The server doubles as a gateway between chat platforms and Agent Smith. Users start runs from Slack or Teams, and the server runs them exactly like a run a webhook or the poller started: admitted against capacity, queued, and executed in the server's sandboxes. There is no separate process for it: `AgentSmith.Server` receives the chat events on the same port as the webhooks (8081).

## Architecture

```
┌─────────┐   HTTPS    ┌──────────────────────┐  enqueue  ┌──────────┐
│  Slack  │───────────▶│  AgentSmith.Server   │──────────▶│  Redis   │
│  Teams  │◀───────────│                      │           │  queue   │
└─────────┘  run id    │  platform adapter    │           └────┬─────┘
                       │  intent engine       │                │
                       │  run launcher        │◀───────────────┘
                       └──────────┬───────────┘   queue consumer
                                  │
                           sandboxes (Kubernetes or Docker)
```

## How it works

1. **A user sends a message** in Slack: `fix #42 in my-api`.
2. **The platform adapter** receives it (Slack Events API at `/slack/events`, Teams at `/api/teams/messages`) and checks the platform's signature.
3. **The intent engine** parses the message: regex patterns first, and an LLM call on the agent's `reasoning` model for input the patterns don't match.
4. **The project resolver** maps `my-api` to a configured project.
5. **The run launcher** starts the run through the same door every other run uses (see [Chat runs go through the queue](#chat-runs-go-through-the-queue)).
6. **The server answers in the thread** with the run id the run is queued under, and a link to the run in the dashboard when `dialogue.dashboard_url` is set.
7. **The run executes in the server**, in the sandboxes the server is configured with, and shows up on the runs board under that id. Its questions and its outcome come back to the thread that started it — see [A chat run's lifecycle](#a-chat-runs-lifecycle).

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
| `fix #42 in my-api` | Runs the `code` pipeline for ticket 42 in project `my-api` (a fix always runs `code`) |
| `fix PROJ-42 in my-api` | The same with a Jira key; `ticket` and `fix` are optional (`#42`, `PROJ-42`) |
| `list tickets in my-api` | Lists the project's open tickets (Jira included) |
| `create ticket "Title" in my-api` | Files a ticket; a second quoted string adds a description |
| `security-review my-api` / `security-review PR#12 in my-api` | Runs `security-scan` on the project, or on the head of pull request 12 — a pull request needs a project with exactly one repo |
| `init my-api` | Runs `init-project` for the project (see [Onboarding](../setup/onboarding.md)) |
| `help` | Shows the available commands |

When the engine can't tell the project or the command, it asks in the thread.

### Slash command and modal

The slash command opens a modal where you pick a command and a project: Fix Bug, Fix Bug (no tests), Add Feature, Security Review, MAD Discussion, List Tickets, Create Ticket, Init Project. The three coding commands run `code` on the ticket you pick, MAD Discussion runs `mad-discussion` on it, and Security Review runs `security-scan` on the project. [Legal analysis](../pipelines/legal-analysis.md) needs a document and is a CLI pipeline, so the modal does not offer it.

## Chat runs go through the queue

A run started from chat is admitted, queued and executed like a run a webhook or the poller started:

- **A ticket run** (`fix`, the modal's coding commands, MAD Discussion) goes through the same spawn path as a polled ticket. It speaks for the project's tracker, so the run moves the ticket to the tracker trigger's `done_status` or `failed_status` like any routed run. A ticket that already has a run in flight is refused, and a run that does not fit the capacity budget waits in the capacity queue — the reply says so and names the run id it waits under. Because a person named the run, it is not asked whether a label would have routed that pipeline, and a waiting run is not dropped when the ticket sits outside the trigger's `trigger_statuses`.
- **A run without a ticket** (`security-review`, `init`) is admitted on the spot, like the dashboard's **Initialize** button: it starts, or it is refused with the reason — it never waits in the capacity queue, which re-checks a ticket and this run has none. Its row is on the runs board with trigger `manual` from the moment the reply names it.

See [Capacity](../operations/capacity.md) for how admission and the queue work.

## A chat run's lifecycle

A run started from chat is bound to the thread that started it. The binding is a row in the server's database — run id, platform, channel, thread and the person who asked — written when the run is launched, so it outlives the conversation, a restart of the server and the replica that received the message.

- **The announcement.** The reply to the command goes into the thread: the run id, why it waits when it waits in the capacity queue, and the dashboard link when `dialogue.dashboard_url` is set. The URL is read at the moment of the reply, so a change made in the dashboard's configuration applies to the next reply without a restart.
- **Questions.** When the run asks a question, it is posted to the thread — buttons for a yes/no, a choice or an approval, and a prompt for a free-text answer. It is posted once, whichever replica sees it first. A run that waits long enough to park keeps its question in the database, so the question is still posted after a restart, when the live question stream has expired.
- **Answers.** A button click, a Teams card, or — for a free-text question — the next message in the thread is delivered to the run through the same durable answer inbox the dashboard's answer box uses. A run waiting live picks it up at once; a parked run resumes from it, days later if need be. Nothing about the answer depends on chat state held in memory or in Redis.
- **The outcome.** When the run ends, the thread is told: the pull requests it opened when it delivered, the reason from its record when it failed, the status otherwise. A run that leaves no record at all half an hour after it was bound (it never started, or its record was deleted) is reported as such. The binding is then closed.
- **One run per thread.** While a thread's run has not reported its outcome, a second run requested in the same thread is refused with the id of the one still going; start a new thread for another run. In Slack every top-level message starts its own thread, so this only stops a second command written inside the thread. A ticket that already has a run in flight is refused either way.

Every replica follows the open bindings every few seconds, reading them from the database. Which replica posts a question or an outcome is decided by a conditional update on the binding, so each is posted exactly once. Progress between the question and the outcome is not posted — the dashboard is where a run is watched.

## Environment variables

| Variable | Description | Required |
|----------|-------------|----------|
| `SLACK_BOT_TOKEN` | Slack bot OAuth token | Slack |
| `SLACK_SIGNING_SECRET` | Slack request signing secret | Slack |
| `TEAMS_APP_ID` | Azure AD App Registration client ID | Teams |
| `TEAMS_APP_PASSWORD` | Azure AD App Registration client secret | Teams |
| `TEAMS_TENANT_ID` | Azure AD tenant ID | Teams |
| `REDIS_URL` | Redis connection, `host:port` | Yes |

Chat runs need nothing chat-specific beyond these: they run in the sandboxes the server is already configured with.
