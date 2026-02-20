# Slack Setup Guide for Agent Smith Dispatcher

This guide is based on a real end-to-end setup. It documents exactly what you
need to do — including the pitfalls that are easy to stumble into.

---

## Prerequisites

- A Slack workspace where you have permission to install apps
- The Agent Smith Dispatcher running and reachable via a public HTTPS URL
  - For local development: use [ngrok](https://ngrok.com) to expose your local port
  - For production: deploy behind a reverse proxy (nginx, Traefik, etc.)

---

## Step 1: Create a Slack App

1. Go to https://api.slack.com/apps
2. Click **Create New App** → **From scratch**
3. Enter:
   - **App Name:** `Agent Smith`
   - **Pick a workspace:** select your workspace
4. Click **Create App**

---

## Step 2: Configure Bot Token Scopes

1. In the left sidebar, go to **OAuth & Permissions**
2. Scroll down to **Scopes → Bot Token Scopes**
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
6. Copy the **Bot User OAuth Token** (starts with `xoxb-`) — you'll need this later

> **Pitfall:** After adding scopes later, Slack will show a banner asking you to
> reinstall the app. You must do this or the new scopes won't take effect.

> **Pitfall:** There is also an "App-Level Token" section (`xapp-...`) on the
> Basic Information page. This is for Socket Mode only — **you do not need it**.
> The Dispatcher uses HTTP webhooks, not Socket Mode.

---

## Step 3: Enable Event Subscriptions

1. In the left sidebar, go to **Event Subscriptions**
2. Toggle **Enable Events** to ON
3. Set the **Request URL** to:
   ```
   https://your-dispatcher-url/slack/events
   ```
   Slack immediately sends a `url_verification` challenge. The Dispatcher
   handles this automatically — you should see a green checkmark (✅ Verified).

   > **Pitfall:** The Dispatcher must be running and publicly reachable
   > *before* you paste the URL here. Start ngrok first, then paste the URL.

4. Under **Subscribe to bot events**, click **Add Bot User Event** and add:

| Event | Purpose |
|-------|---------|
| `message.channels` | Messages posted in channels the bot is a member of |
| `app_mention` | Messages that `@mention` the bot directly |

   > **Pitfall:** There is no `message.im` event in the standard Bot Events list.
   > If you want DM support you need additional setup. For channel-based usage,
   > `message.channels` is sufficient.

5. Click **Save Changes**

---

## Step 4: Enable Interactivity (for Yes/No Buttons)

Required for the question/answer flow where the agent asks for confirmation
and the user clicks a button in Slack.

1. In the left sidebar, go to **Interactivity & Shortcuts**
2. Toggle **Interactivity** to ON
3. Set the **Request URL** to:
   ```
   https://your-dispatcher-url/slack/interact
   ```
4. Click **Save Changes**

---

## Step 5: Retrieve Credentials

You need exactly two values:

### Bot Token
- Go to **OAuth & Permissions**
- Copy **Bot User OAuth Token** (starts with `xoxb-`)

### Signing Secret
- Go to **Basic Information → App Credentials**
- Copy **Signing Secret**

---

## Step 6: Configure the Dispatcher

Add both values to your `.env` file:

```bash
SLACK_BOT_TOKEN=xoxb-your-token-here
SLACK_SIGNING_SECRET=your-signing-secret-here
```

Then start the Dispatcher:

```bash
docker compose up -d redis dispatcher
```

Verify it's running:

```bash
curl http://localhost:6000/health
# Expected: {"status":"ok","timestamp":"..."}
```

---

## Step 7: Expose Locally via ngrok

Slack needs a public HTTPS URL to deliver events to your local Dispatcher.

```bash
brew install ngrok   # if not installed
ngrok http 6000
```

ngrok prints something like:

```
Forwarding  https://abc123.ngrok.io -> http://localhost:6000
```

Use `https://abc123.ngrok.io` as your base URL in Steps 3 and 4.

> **Pitfall:** Every time you restart ngrok, you get a **new URL**. You must
> update the Event Subscriptions and Interactivity URLs in Slack each time.
> To avoid this, use a paid ngrok plan with a fixed subdomain, or deploy the
> Dispatcher to a server with a stable URL.

---

## Step 8: Invite the Bot to a Channel

The bot only receives `message.channels` events in channels it is a **member** of.

1. Open any Slack channel (e.g. `#test-agent`)
2. Type `/invite @Agent Smith`
3. The bot joins and will now receive messages in that channel

> **Pitfall:** Writing to the bot in the **Direct Messages** (Apps) tab does not
> work by default. The `message.channels` event only fires in channels.
> Use a channel, not a DM.

---

## Step 9: Test the Integration

### List Tickets

```
list tickets in agent-smith-test
```

Expected response:
```
🎫 Open tickets in agent-smith-test (3 total):
• #1 — Fix login timeout [Active]
• #2 — Add export CSV [New]
```

### Create a Ticket

```
create ticket "Add README documentation" in agent-smith-test
```

### Fix a Ticket (requires Kubernetes)

```
fix #1 in agent-smith-test
```

Expected flow:
1. 🚀 Starting Agent Smith for ticket #1 in agent-smith-test...
2. ⚙️ [1/9] FetchTicketCommand
3. ⚙️ [2/9] CheckoutSourceCommand
4. ...
5. 🚀 Done! · View Pull Request

> **Note:** `fix` spawns a Kubernetes Job. You need Kubernetes enabled
> (Docker Desktop → Settings → Kubernetes → Enable Kubernetes) for this
> to work. `list` and `create` work without Kubernetes.

---

## Supported Commands

| Command | Example |
|---------|---------|
| Fix a ticket | `fix #65 in agent-smith-test` |
| Fix (with mention) | `@Agent Smith fix #65 in agent-smith-test` |
| List open tickets | `list tickets in agent-smith-test` |
| List (alternative) | `list ticket for agent-smith-test` |
| Create a ticket | `create ticket "Title here" in agent-smith-test` |
| Create with description | `create ticket "Title" in agent-smith-test "Description"` |

The project name must match a key in `config/agentsmith.yml`.

---

## Troubleshooting

### Bot doesn't respond at all

Check that events are actually reaching the Dispatcher:

```bash
docker compose logs dispatcher -f
```

If you see no incoming requests, the problem is the URL — either ngrok is not
running, or the Event Subscriptions URL in Slack is outdated (ngrok restarted).

### "Project X not found in configuration"

The project name in your Slack command must exactly match a key in
`config/agentsmith.yml`. Check what projects are configured:

```bash
grep "^  [a-z]" config/agentsmith.yml
```

### URL verification fails (no green checkmark)

The Dispatcher must be running and reachable before Slack can verify the URL.
Check the order:

1. `docker compose up -d redis dispatcher`
2. `curl http://localhost:6000/health` — must return 200
3. `ngrok http 6000` — must be running
4. Paste the ngrok URL into Slack

### Bot doesn't respond to messages (but URL verification worked)

- Make sure `message.channels` is in the bot event subscriptions
- Make sure `channels:history` scope is added and the app is reinstalled
- Make sure the bot is **invited to the channel** (`/invite @Agent Smith`)

### "GITHUB_TOKEN is not set" error

Add the missing token to your `.env` file and restart:

```bash
docker compose up -d dispatcher
```

### ngrok URL changed after restart

Update both URLs in the Slack App settings:
- **Event Subscriptions** → Request URL
- **Interactivity & Shortcuts** → Request URL

---

## Local Development Checklist

- [ ] Slack App created with correct Bot Token Scopes
- [ ] `message.channels` and `app_mention` events subscribed
- [ ] Interactivity enabled with `/slack/interact` URL
- [ ] App installed to workspace (reinstall after scope changes)
- [ ] `.env` contains `SLACK_BOT_TOKEN` and `SLACK_SIGNING_SECRET`
- [ ] `docker compose up -d redis dispatcher` running
- [ ] `curl http://localhost:6000/health` returns 200
- [ ] ngrok running and URL set in Slack Event Subscriptions
- [ ] Bot invited to at least one channel (`/invite @Agent Smith`)
- [ ] Project names in Slack commands match `config/agentsmith.yml`

## Production Checklist

- [ ] Dispatcher deployed with stable public HTTPS URL
- [ ] `SLACK_BOT_TOKEN` set in K8s Secret / environment
- [ ] `SLACK_SIGNING_SECRET` set in K8s Secret / environment
- [ ] Slack Event Subscriptions URL pointing to production Dispatcher
- [ ] Slack Interactivity URL pointing to production Dispatcher
- [ ] Bot invited to all relevant channels
- [ ] Redis running (no PV needed for ephemeral mode)
- [ ] Kubernetes available for `fix` commands