# Slack Setup Guide

This guide walks through connecting Agent Smith to Slack, from app creation to
first message. It covers both local development (ngrok) and production.

---

## Prerequisites

- A Slack workspace where you have permission to install apps
- Agent Smith running and reachable via a public HTTPS URL
  - Local development: [ngrok](https://ngrok.com) to expose your local port
  - Production: reverse proxy (nginx, Traefik, etc.)

---

## Step 1: Create a Slack App

1. Go to https://api.slack.com/apps
2. Click **Create New App** > **From scratch**
3. Enter:
   - **App Name:** `Agent Smith`
   - **Pick a workspace:** select your workspace
4. Click **Create App**

---

## Step 2: Configure Bot Token Scopes

1. In the left sidebar, go to **OAuth & Permissions**
2. Scroll down to **Scopes > Bot Token Scopes**
3. Add the following scopes:

| Scope | Purpose |
|-------|---------|
| `chat:write` | Post messages to channels |
| `chat:write.public` | Post to channels the bot hasn't joined yet |
| `channels:read` | List channels |
| `channels:history` | Read messages in channels (required for Events API) |
| `app_mentions:read` | Receive `@Agent Smith` mentions |
| `im:write` | Send direct messages |

4. Scroll up and click **Install to Workspace**
5. Authorize the app
6. Copy the **Bot User OAuth Token** (starts with `xoxb-`) — you need this later

!!! warning "Reinstall after scope changes"
    After adding scopes later, Slack shows a banner asking you to reinstall.
    You must do this or the new scopes won't take effect.

!!! info "App-Level Token not needed"
    The **App-Level Token** (`xapp-...`) on the Basic Information page is for
    Socket Mode only. Agent Smith uses HTTP webhooks, not Socket Mode.

---

## Step 3: Enable Event Subscriptions

1. In the left sidebar, go to **Event Subscriptions**
2. Toggle **Enable Events** to ON
3. Set the **Request URL** to:
   ```
   https://<your-host>/slack/events
   ```
   Slack immediately sends a `url_verification` challenge. Agent Smith handles
   this automatically — you should see a green checkmark.

   !!! warning "Server must be running first"
       The server must be running and publicly reachable *before* you paste
       the URL. Start ngrok first, then paste the URL.

4. Under **Subscribe to bot events**, add:

| Event | Purpose |
|-------|---------|
| `message.channels` | Messages posted in channels the bot is a member of |
| `app_mention` | Messages that `@mention` the bot directly |

5. Click **Save Changes**

---

## Step 4: Enable Interactivity

Required for the question/answer flow (confirmation buttons, choice menus).

1. In the left sidebar, go to **Interactivity & Shortcuts**
2. Toggle **Interactivity** to ON
3. Set the **Request URL** to:
   ```
   https://<your-host>/slack/interact
   ```
4. Click **Save Changes**

---

## Step 5: Retrieve Credentials

You need exactly two values:

| Credential | Where to find it |
|------------|-----------------|
| **Bot Token** | OAuth & Permissions > Bot User OAuth Token (`xoxb-...`) |
| **Signing Secret** | Basic Information > App Credentials > Signing Secret |

---

## Step 6: Configure Environment

Add credentials to your `.env` file:

```bash
SLACK_BOT_TOKEN=xoxb-your-token-here
SLACK_SIGNING_SECRET=your-signing-secret-here
```

---

## Step 7: Start the Server

### Docker Compose (local development)

```bash
docker compose -f deploy/docker-compose.yml up -d server redis
```

The `server` service runs the chat gateway with `SPAWNER_TYPE=docker` by default,
meaning agent jobs run as Docker containers on the same host.

Verify it's running:

```bash
curl http://localhost:6000/health
# Expected: {"status":"ok","timestamp":"..."}
```

!!! tip "Service names"
    The chat gateway service is called `server` in docker-compose.yml (not
    `dispatcher`). It listens on port 8081 internally, mapped to `${DISPATCHER_PORT:-6000}`.

### Kubernetes

```bash
kubectl apply -k k8s/overlays/prod
```

Set `SPAWNER_TYPE=kubernetes` to spawn agent jobs as K8s Jobs.

---

## Step 8: Expose Locally via ngrok

Slack needs a public HTTPS URL to deliver events to your local server.

```bash
ngrok http 6000
```

ngrok prints something like:

```
Forwarding  https://abc123.ngrok.io -> http://localhost:6000
```

Use `https://abc123.ngrok.io` as your base URL in Steps 3 and 4.

!!! warning "ngrok URL changes on restart"
    Every time you restart ngrok, you get a new URL. Update both URLs in
    Slack (Event Subscriptions and Interactivity). Use a paid ngrok plan
    with a fixed subdomain to avoid this.

---

## Step 9: Invite the Bot to a Channel

The bot only receives `message.channels` events in channels it is a **member** of.

1. Open any Slack channel (e.g. `#test-agent`)
2. Type `/invite @Agent Smith`

!!! info "DMs not supported by default"
    The `message.channels` event only fires in channels. Use a channel, not a DM.

---

## Step 10: Test the Integration

### List Tickets

```
list tickets in my-project
```

### Fix a Ticket

```
fix #1 in my-project
```

The project name must match a key in your `agentsmith.yml` configuration.

---

## Supported Commands

| Command | Example |
|---------|---------|
| Fix a ticket | `fix #65 in my-project` |
| Fix (with mention) | `@Agent Smith fix #65 in my-project` |
| List open tickets | `list tickets in my-project` |
| Create a ticket | `create ticket "Title here" in my-project` |
| Security scan | `scan my-project for security issues` |
| Help | `help` |

---

## Troubleshooting

### Bot doesn't respond at all

Check that events reach the server:

```bash
docker compose -f deploy/docker-compose.yml logs server -f
```

If you see no incoming requests, the problem is the URL — either ngrok is not
running, or the Event Subscriptions URL in Slack is outdated.

### "Project X not found in configuration"

The project name in your Slack command must exactly match a key in your
`agentsmith.yml`. Check what projects are configured:

```bash
grep "^  [a-z]" config/agentsmith.yml
```

### URL verification fails (no green checkmark)

Check the order:

1. `docker compose -f deploy/docker-compose.yml up -d server redis`
2. `curl http://localhost:6000/health` — must return 200
3. `ngrok http 6000` — must be running
4. Paste the ngrok URL into Slack

### Bot doesn't respond to messages (but URL verification worked)

- `message.channels` must be in the bot event subscriptions
- `channels:history` scope must be added and the app reinstalled
- The bot must be **invited to the channel** (`/invite @Agent Smith`)

### ngrok URL changed after restart

Update both URLs in the Slack App settings:

- **Event Subscriptions** > Request URL
- **Interactivity & Shortcuts** > Request URL

---

## Checklists

### Local Development

- [ ] Slack App created with correct Bot Token Scopes
- [ ] `message.channels` and `app_mention` events subscribed
- [ ] Interactivity enabled with `/slack/interact` URL
- [ ] App installed to workspace (reinstall after scope changes)
- [ ] `.env` contains `SLACK_BOT_TOKEN` and `SLACK_SIGNING_SECRET`
- [ ] `docker compose -f deploy/docker-compose.yml up -d server redis` running
- [ ] `curl http://localhost:6000/health` returns 200
- [ ] ngrok running and URL set in Slack Event Subscriptions + Interactivity
- [ ] Bot invited to at least one channel

### Production

- [ ] Server deployed with stable public HTTPS URL
- [ ] `SLACK_BOT_TOKEN` set in K8s Secret / environment
- [ ] `SLACK_SIGNING_SECRET` set in K8s Secret / environment
- [ ] `SPAWNER_TYPE` set (`docker` or `kubernetes`)
- [ ] Slack Event Subscriptions URL pointing to production server
- [ ] Slack Interactivity URL pointing to production server
- [ ] Bot invited to all relevant channels
- [ ] Redis running
