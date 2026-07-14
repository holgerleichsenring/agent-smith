# Trigger: webhooks

What you want for production. Your tracker POSTs to Agent Smith on every ticket update, Agent Smith filters down to the events that matter (status change, label add, comment with a command) and starts a run.

The server listens on `POST /webhook` (port 8081) and detects the platform from the request's headers and payload. Explicit per-platform routes exist too — `POST /webhook/github`, `/webhook/gitlab`, `/webhook/jira` — they all land in the same handler. For docker-compose / k8s deploys this is exposed on the server service; for the CLI binary it's not (the CLI is one-shot).

All examples use the fictional `TodoList` project on `acme-org`. Substitute your URLs.

## Azure DevOps

In Azure DevOps: **Project Settings → Service hooks → Create subscription → Web Hooks**.

Pick the trigger:

- Event: **Work item updated** (covers status changes, label edits, comment additions).
- Filters: **Area Path** = the area path your project lives under (or leave blank to receive everything from the project). **Work item type** = whatever types you want to trigger from (User Story, Bug, Task).

For the action:

- URL: `https://agent-smith.your-host.example/webhook`
- Resource details to send: **All**.
- Messages to send: **None**.
- Detailed messages to send: **None** (the framework reads the payload).

Azure DevOps doesn't sign webhook payloads by default. Add a Basic Auth header instead — set the **Basic authentication** password in the Service Hook setup, and give the server the same value via the `AZDO_WEBHOOK_SECRET` environment variable:

```bash
# on the server process (compose env, k8s Secret)
AZDO_WEBHOOK_SECRET=...
```

The server compares the `Authorization` header against it and rejects mismatches. If the variable is unset, requests without the header pass — set it.

Verify the wiring with a test work item: tag it `TodoList`, set status to `Active`. Within a second the orchestrator log should show `webhook received, ticket TID-4471, project azuredevops-todolist`.

## Jira

In Jira Cloud: **System → System Webhooks → Create a Webhook**.

- URL: `https://agent-smith.your-host.example/webhook/jira`
- Events: **Issue created**, **Issue updated**.
- JQL filter: `project = TL` (or whatever your project key is) — narrows webhooks to just the project Agent Smith manages.
- Secret: paste your `JIRA_WEBHOOK_SECRET` value. When Jira sends a signature header, the server HMAC-verifies the body against it.

Jira is the one platform where the secret lives in the config (per project, on the trigger block) rather than in a server env var:

```yaml
projects:
  acme-rules:
    # ...
    jira_trigger:
      secret: ${JIRA_WEBHOOK_SECRET}
      # ...
```

Jira Cloud system webhooks don't send a signature header at all — in that case the request is accepted; use the JQL filter plus network-level controls to keep the endpoint quiet.

For Jira Server / Data Center the webhook UI is similar but lives under **System → Webhooks**.

## GitHub

Either a per-repo webhook or a single org-level webhook.

**Per-repo** (for one or two repos): **Repo Settings → Webhooks → Add webhook**.

**Org-level** (recommended when you have many repos): **Org Settings → Webhooks → Add webhook**.

Either way:

- Payload URL: `https://agent-smith.your-host.example/webhook/github`
- Content type: **application/json**
- Secret: paste your `GITHUB_WEBHOOK_SECRET` value.
- Events: **Issues** (state changes + label adds) and optionally **Issue comments** (if you want comment-driven triggers via the project's `comment_keyword`).

Give the server the same value as an environment variable:

```bash
GITHUB_WEBHOOK_SECRET=...
```

GitHub HMACs the body with the secret and sends it in `X-Hub-Signature-256`. The server verifies.

## GitLab

**Project Settings → Webhooks** (or for group-wide: **Group Settings → Webhooks**).

- URL: `https://agent-smith.your-host.example/webhook/gitlab`
- Trigger: **Issues events**.
- Secret token: paste your `GITLAB_WEBHOOK_TOKEN` value.

Give the server the same value as an environment variable:

```bash
GITLAB_WEBHOOK_TOKEN=...
```

GitLab sends the token in the `X-Gitlab-Token` header, plain text (not HMAC). The server string-compares.

## Reachability

Webhooks need a publicly-reachable URL for the orchestrator. Three common shapes:

- **Public ingress** — orchestrator behind your standard ingress / load balancer. TLS terminates there.
- **Cloudflare Tunnel / ngrok** — for development or for trackers you don't want to expose your network to. Free Cloudflare Tunnel works fine.
- **VPN / private link** — for Azure DevOps Server or Jira Server inside a corporate network, with the tracker and the orchestrator on the same VPN.

If you can't reach the orchestrator from the tracker, use [polling](polling.md) instead.

## What the framework does on receipt

1. Detect the platform and verify the secret (HMAC for GitHub, HMAC when present for Jira, token compare for GitLab, basic-auth for Azure DevOps).
2. Parse the payload, extract the ticket id and the changed fields.
3. Decide if this event matters: did the status change to one of `trigger_statuses`? Did a `pipeline_from_label` label get added? Did a comment with the project's `comment_keyword` land? If none of the above, return 200 OK and stop.
4. If the event matters: claim the ticket — the claim is a database lease, so a webhook and a poll racing on the same ticket can't double-trigger, and one ticket never has two live runs (that's enforced by construction, not by timing). Then check capacity: if the run's whole footprint doesn't fit right now, it queues in strict FIFO order instead of failing (see [Capacity & queueing](../reference/operations/capacity.md)).
5. The run spawns and executes; every state change lands in the [dashboard](../reference/operations/dashboard.md).

Webhook responses are always 200 OK if the framework received the payload correctly, even when the event was filtered out. That tells the tracker not to retry. Errors during the run itself land in the ticket as a comment.

## Next

- [Polling](polling.md) — the fallback when webhooks aren't an option.
- [Labels](labels.md) — `pipeline_from_label` and the `agent-smith:*` lifecycle labels.
- [Host it](../host-it/docker-compose.md) — for the public URL, you need a real host. docker-compose + a reverse proxy is the easiest path.
