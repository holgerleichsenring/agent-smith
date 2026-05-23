# Teams Setup Guide

This guide walks through connecting Agent Smith to Microsoft Teams. It covers
Azure Bot registration, local development with ngrok, and sideloading the app.

!!! info "Beta"
    The Teams adapter is functional but has not been tested with the Bot
    Framework Emulator yet. Report issues if you encounter them.

---

## Prerequisites

- An Azure account with permission to create App Registrations
- A Microsoft Teams tenant where you can sideload custom apps
- Agent Smith running and reachable via a public HTTPS URL
  - Local development: [ngrok](https://ngrok.com) to expose your local port
  - Production: reverse proxy (nginx, Traefik, etc.)

---

## Step 1: Create an Azure AD App Registration

1. Go to the [Azure Portal](https://portal.azure.com) > **Azure Active Directory** > **App registrations**
2. Click **New registration**
3. Enter:
   - **Name:** `Agent Smith Bot`
   - **Supported account types:** Accounts in any organizational directory (Multitenant)
   - **Redirect URI:** leave empty
4. Click **Register**
5. Copy the **Application (client) ID** — this is your `TEAMS_APP_ID`
6. Copy the **Directory (tenant) ID** — this is your `TEAMS_TENANT_ID`

### Create a Client Secret

1. In the app registration, go to **Certificates & secrets**
2. Click **New client secret**
3. Enter a description (e.g. `agent-smith-bot`) and choose an expiry
4. Click **Add**
5. Copy the **Value** immediately — this is your `TEAMS_APP_PASSWORD`

!!! warning "Secret visibility"
    The secret value is only shown once. If you lose it, you must create a new one.

---

## Step 2: Create an Azure Bot Resource

1. In the Azure Portal, search for **Azure Bot** and click **Create**
2. Enter:
   - **Bot handle:** `agent-smith`
   - **Pricing tier:** F0 (free) for development
   - **Microsoft App ID:** Select "Use existing app registration"
   - **App ID:** paste your `TEAMS_APP_ID` from Step 1
3. Click **Create**

### Enable the Teams Channel

1. Open your Azure Bot resource
2. Go to **Channels**
3. Click **Microsoft Teams**
4. Accept the terms and click **Apply**

### Set the Messaging Endpoint

1. Go to **Configuration**
2. Set the **Messaging endpoint** to:
   ```
   https://<your-host>/api/teams/messages
   ```
   For local development, this is your ngrok URL (see Step 4).

---

## Step 3: Configure Environment

Add the credentials to your `.env` file:

```bash
TEAMS_APP_ID=<Application (client) ID from Step 1>
TEAMS_APP_PASSWORD=<Client secret value from Step 1>
TEAMS_TENANT_ID=<Directory (tenant) ID from Step 1>
```

---

## Step 4: Start the Server

### Docker Compose (local development)

```bash
docker compose -f deploy/docker-compose.yml up -d server redis
```

The `server` service already includes the Teams adapter. Verify it's running:

```bash
curl http://localhost:6000/health
```

### Expose via ngrok

```bash
ngrok http 6000
```

Copy the HTTPS URL (e.g. `https://abc123.ngrok.io`) and set it as the
messaging endpoint in the Azure Bot Configuration (Step 2):

```
https://abc123.ngrok.io/api/teams/messages
```

!!! warning "ngrok URL changes on restart"
    Every time you restart ngrok, update the messaging endpoint in the
    Azure Bot Configuration. Use a paid ngrok plan with a fixed subdomain
    to avoid this.

---

## Step 5: Create the Teams App Manifest

Teams requires an app package (ZIP file) containing a manifest and two icons
to sideload the bot.

### Icon Requirements

| Icon | Size | Format | Description |
|------|------|--------|-------------|
| `color.png` | 192 x 192 px | PNG | Full-color app icon, shown in the Teams app catalog |
| `outline.png` | 32 x 32 px | PNG, transparent background | Monochrome outline, shown in the Teams activity bar |

Create both icons with your own branding.

### manifest.json

A template is provided at [`docs/setup/teams/manifest.json`](teams/manifest.json).
Replace `<TEAMS_APP_ID>` with your actual Application ID from Step 1.

### Build the App Package

Create a ZIP file containing exactly three files at the root level:

```bash
cd docs/setup/teams
zip agent-smith-teams.zip manifest.json color.png outline.png
```

---

## Step 6: Sideload into Teams

1. Open Microsoft Teams
2. Go to **Apps** (left sidebar)
3. Click **Manage your apps** (bottom left)
4. Click **Upload an app** > **Upload a custom app**
5. Select `agent-smith-teams.zip`
6. Click **Add**

The bot is now available. You can message it directly or add it to a channel.

### Add to a Channel

1. Open a channel
2. Click **+** to add a tab, or go to channel settings > **Connectors/Apps**
3. Search for "Agent Smith" and add it
4. `@Agent Smith fix #42 in my-project` in the channel

---

## Step 7: Test the Integration

Send a message to the bot in a direct chat or channel:

```
@Agent Smith help
```

Expected: an Adaptive Card showing available commands.

```
@Agent Smith list tickets in my-project
```

Expected: a response listing open tickets from the configured project.

---

## Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `TEAMS_APP_ID` | Azure AD App Registration client ID | Yes |
| `TEAMS_APP_PASSWORD` | Azure AD App Registration client secret | Yes |
| `TEAMS_TENANT_ID` | Azure AD tenant ID | Yes |
| `REDIS_URL` | Redis connection string | Yes |
| `SPAWNER_TYPE` | `kubernetes` or `docker` | Yes |
| `AGENTSMITH_IMAGE` | Docker image for spawned agent jobs | Yes |

---

## Troubleshooting

### Bot doesn't respond at all

1. Check the server logs:
   ```bash
   docker compose -f deploy/docker-compose.yml logs server -f
   ```
2. Verify the messaging endpoint URL in Azure Bot Configuration matches your
   ngrok URL + `/api/teams/messages`
3. Verify the Teams channel is enabled in the Azure Bot resource

### "Teams JWT validation failed" in logs

- The `TEAMS_APP_ID` must match the App Registration's Application ID exactly
- The `TEAMS_APP_PASSWORD` may have expired — create a new client secret

### Bot responds but can't start jobs

- Check `SPAWNER_TYPE` is set (`docker` for local, `kubernetes` for production)
- For Docker: verify the Docker socket is mounted (`/var/run/docker.sock`)
- For K8s: verify `K8S_NAMESPACE` and `K8S_SECRET_NAME` are set

### Sideload fails

- Ensure the ZIP contains exactly `manifest.json`, `color.png`, `outline.png`
  at the root (not nested in a folder)
- Validate `manifest.json` against the
  [Teams manifest schema](https://learn.microsoft.com/en-us/microsoftteams/platform/resources/schema/manifest-schema)
- Verify `color.png` is 192x192 and `outline.png` is 32x32

### ngrok URL changed after restart

Update the messaging endpoint in Azure Portal > Azure Bot > Configuration.

---

## Checklists

### Local Development

- [ ] Azure AD App Registration created
- [ ] Client secret created and copied
- [ ] Azure Bot resource created with Teams channel enabled
- [ ] `.env` contains `TEAMS_APP_ID`, `TEAMS_APP_PASSWORD`, `TEAMS_TENANT_ID`
- [ ] `docker compose -f deploy/docker-compose.yml up -d server redis` running
- [ ] `curl http://localhost:6000/health` returns 200
- [ ] ngrok running and URL set as messaging endpoint in Azure Bot
- [ ] App manifest created with correct `botId`
- [ ] Icons created (192x192 color.png, 32x32 outline.png)
- [ ] App sideloaded into Teams
- [ ] Bot responds to a test message

### Production

- [ ] Server deployed with stable public HTTPS URL
- [ ] `TEAMS_APP_ID` set in K8s Secret / environment
- [ ] `TEAMS_APP_PASSWORD` set in K8s Secret / environment
- [ ] `TEAMS_TENANT_ID` set in K8s Secret / environment
- [ ] `SPAWNER_TYPE` set (`docker` or `kubernetes`)
- [ ] Messaging endpoint set to production URL
- [ ] Redis running
