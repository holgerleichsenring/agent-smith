# Trigger: webhooks

What you want for production. Your tracker POSTs to Agent Smith on every ticket update, Agent Smith filters down to the events that matter (status change, label add, comment with a command) and starts a run.

The orchestrator listens on `POST /webhooks/{tracker-type}` — one endpoint per tracker family. For docker-compose / k8s deploys this is exposed on the orchestrator service; for the CLI binary it's not (the CLI is one-shot).

All examples use the fictional `TodoList` project on `acme-org`. Substitute your URLs.

## Azure DevOps

In Azure DevOps: **Project Settings → Service hooks → Create subscription → Web Hooks**.

Pick the trigger:

- Event: **Work item updated** (covers status changes, label edits, comment additions).
- Filters: **Area Path** = the area path your project lives under (or leave blank to receive everything from the project). **Work item type** = whatever types you want to trigger from (User Story, Bug, Task).

For the action:

- URL: `https://agent-smith.your-host.example/webhooks/azure-devops`
- Resource details to send: **All**.
- Messages to send: **None**.
- Detailed messages to send: **None** (the framework reads the payload).

Azure DevOps doesn't sign webhook payloads by default. Add a Basic Auth header instead — the orchestrator accepts a token in the `Authorization: Basic ...` header and rejects anything else:

```yaml
trackers:
  acme-platform:
    type: azure_devops
    # ...
    webhook_secret: ${AZURE_DEVOPS_WEBHOOK_SECRET}
```

In the Service Hook setup, set the **Basic authentication username** to `agent-smith` and the **password** to whatever you put in `AZURE_DEVOPS_WEBHOOK_SECRET`. The framework hashes it and compares.

Verify the wiring with a test work item: tag it `TodoList`, set status to `Active`. Within a second the orchestrator log should show `webhook received, ticket TID-4471, project azuredevops-todolist`.

## Jira

In Jira Cloud: **System → System Webhooks → Create a Webhook**.

- URL: `https://agent-smith.your-host.example/webhooks/jira`
- Events: **Issue created**, **Issue updated**.
- JQL filter: `project = TL` (or whatever your project key is) — narrows webhooks to just the project Agent Smith manages.
- Secret: paste your `JIRA_WEBHOOK_SECRET` value. Jira HMACs the body with this and sends it in `X-Atlassian-Webhook-Identifier`.

```yaml
trackers:
  acme-jira:
    type: jira
    # ...
    webhook_secret: ${JIRA_WEBHOOK_SECRET}
```

For Jira Server / Data Center the webhook UI is similar but lives under **System → Webhooks**.

## GitHub

Either a per-repo webhook or a single org-level webhook.

**Per-repo** (for one or two repos): **Repo Settings → Webhooks → Add webhook**.

**Org-level** (recommended when you have many repos): **Org Settings → Webhooks → Add webhook**.

Either way:

- Payload URL: `https://agent-smith.your-host.example/webhooks/github`
- Content type: **application/json**
- Secret: paste your `GITHUB_WEBHOOK_SECRET` value.
- Events: **Issues** (state changes + label adds) and optionally **Issue comments** (if you want comment-driven commands like `/agent-smith fix`).

```yaml
trackers:
  acme-issues:
    type: github
    # ...
    webhook_secret: ${GITHUB_WEBHOOK_SECRET}
```

GitHub HMACs the body with the secret and sends it in `X-Hub-Signature-256`. The framework verifies.

## GitLab

**Project Settings → Webhooks** (or for group-wide: **Group Settings → Webhooks**).

- URL: `https://agent-smith.your-host.example/webhooks/gitlab`
- Trigger: **Issues events**.
- Secret token: paste your `GITLAB_WEBHOOK_SECRET` value.

```yaml
trackers:
  acme-gitlab:
    type: gitlab
    # ...
    webhook_secret: ${GITLAB_WEBHOOK_SECRET}
```

GitLab sends the secret in the `X-Gitlab-Token` header, plain text (not HMAC). The framework string-compares.

## Reachability

Webhooks need a publicly-reachable URL for the orchestrator. Three common shapes:

- **Public ingress** — orchestrator behind your standard ingress / load balancer. TLS terminates there.
- **Cloudflare Tunnel / ngrok** — for development or for trackers you don't want to expose your network to. Free Cloudflare Tunnel works fine.
- **VPN / private link** — for Azure DevOps Server or Jira Server inside a corporate network, with the tracker and the orchestrator on the same VPN.

If you can't reach the orchestrator from the tracker, use [polling](polling.md) instead.

## What the framework does on receipt

1. Verify the secret (HMAC for GitHub / Jira, token compare for GitLab, basic-auth for Azure DevOps).
2. Parse the payload, extract the ticket id and the changed fields.
3. Decide if this event matters: did the status change to one of `trigger_statuses`? Did a `pipeline_from_label` label get added? Did a `/agent-smith` comment land? If none of the above, return 200 OK and stop.
4. If the event matters: claim the ticket (SETNX in Redis to prevent webhook+poll racing), enqueue a `PipelineRequest`, return 200 OK to the tracker.
5. A consumer dequeues the request and runs the pipeline.

Webhook responses are always 200 OK if the framework received the payload correctly, even when the event was filtered out. That tells the tracker not to retry. Errors during the run itself land in the ticket as a comment.

## Next

- [Polling](polling.md) — the fallback when webhooks aren't an option.
- [Labels](labels.md) — `pipeline_from_label` and the `agent-smith:*` lifecycle labels.
- [Host it](../host-it/docker-compose.md) — for the public URL, you need a real host. docker-compose + a reverse proxy is the easiest path.
