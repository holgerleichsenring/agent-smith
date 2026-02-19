# Slack Setup Guide for Agent Smith Dispatcher

This guide walks you through creating a Slack App and connecting it to the
Agent Smith Dispatcher service.

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
| `chat:write.public` | Post to channels the bot hasn't joined |
| `channels:read` | List channels |
| `im:write` | Send direct messages |
| `reactions:write` | Add emoji reactions (optional, for status indicators) |

4. Scroll up and click **Install to Workspace**
5. Authorize the app
6. Copy the **Bot User OAuth Token** (starts with `xoxb-`) — you'll need this later

---

## Step 3: Enable Event Subscriptions

1. In the left sidebar, go to **Event Subscriptions**
2. Toggle **Enable Events** to ON
3. Set the **Request URL** to:
   ```
   https://your-dispatcher-url/slack/events
   ```
   Slack will immediately send a `url_verification` challenge. The Dispatcher
   handles this automatically — you should see a green checkmark.

4. Under **Subscribe to bot events**, click **Add Bot User Event** and add:
   - `message.channels` — messages posted in channels the bot is a member of
   - `app_mention` — messages that mention the bot directly (`@Agent Smith`)

5. Click **Save Changes**

---

## Step 4: Enable Interactivity (for Yes/No Buttons)

This is required for the question/answer flow where the agent asks for
confirmation and the user clicks a button.

1. In the left sidebar, go to **Interactivity & Shortcuts**
2. Toggle **Interactivity** to ON
3. Set the **Request URL** to:
   ```
   https://your-dispatcher-url/slack/interact
   ```
4. Click **Save Changes**

---

## Step 5: Retrieve Credentials

You need two values:

### Bot Token
- Go to **OAuth & Permissions**
- Copy **Bot User OAuth Token** (starts with `xoxb-`)

### Signing Secret
- Go to **Basic Information → App Credentials**
- Copy **Signing Secret**

---

## Step 6: Configure the Dispatcher

Set these environment variables on the Dispatcher service:

```bash
SLACK_BOT_TOKEN=xoxb-your-token-here
SLACK_SIGNING_SECRET=your-signing-secret-here
```

In Kubernetes, add them to the `agentsmith-secrets` Secret:

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: agentsmith-secrets
  namespace: default
type: Opaque
stringData:
  slack-bot-token: "xoxb-your-token-here"
  slack-signing-secret: "your-signing-secret-here"
  anthropic-api-key: "sk-ant-..."
  azure-devops-token: "..."
  github-token: "ghp_..."
  redis-url: "redis://redis:6379"
```

---

## Step 7: Invite the Bot to a Channel

1. Open the Slack channel where you want to use Agent Smith
2. Type `/invite @Agent Smith`
3. The bot is now a member and will receive messages in that channel

---

## Step 8: Test the Integration

### Local Testing with ngrok

```bash
# Install ngrok
brew install ngrok

# Expose the Dispatcher port (default 5000 for ASP.NET Core dev)
ngrok http 5000
```

Copy the `https://xxxxx.ngrok.io` URL and use it as your base URL in
Steps 3 and 4 above.

### Verify with a Health Check

```bash
curl https://your-dispatcher-url/health
# Expected: {"status":"ok","timestamp":"..."}
```

### Send a Test Message

In the Slack channel where you invited the bot:

```
fix #54 in agent-smith-test
```

Expected flow:
1. Bot replies: `:rocket: Starting Agent Smith for ticket #54 in agent-smith-test...`
2. Progress updates appear: `:gear: [1/9] FetchTicketCommand...`
3. If agent has a question: buttons appear (Yes / No)
4. On completion: `:rocket: Done! ... :link: View Pull Request`

### List Tickets

```
list tickets in agent-smith-test
```

### Create a Ticket

```
create ticket "Add README documentation" in agent-smith-test
```

---

## Supported Commands

| Command | Example |
|---------|---------|
| Fix a ticket | `fix #65 in todo-list` |
| Fix (with mention) | `@Agent Smith fix #65 in todo-list` |
| List open tickets | `list tickets in todo-list` |
| List (alternative) | `list ticket for todo-list` |
| Create a ticket | `create ticket "Title here" in todo-list` |
| Create with description | `create ticket "Title" in todo-list "Description here"` |

---

## Troubleshooting

### Slack shows "dispatch_failed" on URL verification
- Make sure the Dispatcher is running and accessible from the internet
- Check that ngrok is running and the URL is correct
- Check Dispatcher logs for incoming requests

### Bot doesn't respond to messages
- Verify the bot is invited to the channel (`/invite @Agent Smith`)
- Check that `message.channels` event is subscribed
- Check Dispatcher logs for incoming Slack events

### Buttons don't work (question/answer flow)
- Verify Interactivity Request URL is set correctly
- The URL must be HTTPS — ngrok provides this automatically
- Check Dispatcher logs for `/slack/interact` requests

### Signature verification fails
- Make sure `SLACK_SIGNING_SECRET` matches the value in **Basic Information → App Credentials**
- For local development without verification, leave `SLACK_SIGNING_SECRET` empty

---

## Production Checklist

- [ ] Dispatcher deployed with HTTPS
- [ ] `SLACK_BOT_TOKEN` set in K8s Secret
- [ ] `SLACK_SIGNING_SECRET` set in K8s Secret
- [ ] Slack Event Subscriptions URL pointing to production Dispatcher
- [ ] Slack Interactivity URL pointing to production Dispatcher
- [ ] Bot invited to all relevant channels
- [ ] Redis running in K8s (no PV needed for ephemeral mode)
- [ ] `agentsmith-secrets` K8s Secret contains all provider tokens