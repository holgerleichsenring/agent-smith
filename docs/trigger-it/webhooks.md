# Trigger: webhooks

What you want for production. Your tracker POSTs to Agent Smith on every ticket update, Agent Smith filters down to the events that matter (status change, label add, comment with a command) and starts a run.

The server listens on `POST /webhook` (port 8081) and detects the platform from the request's headers and payload. Explicit per-platform routes exist too — `POST /webhook/github`, `/webhook/gitlab`, `/webhook/jira` — they all land in the same handler. For docker-compose / k8s deploys this is exposed on the server service; for the CLI binary it's not (the CLI is one-shot).

All examples use the fictional `TodoList` project on `acme-org`. Substitute your URLs.

## Azure DevOps

In Azure DevOps: **Project Settings → Service hooks → Create subscription → Web Hooks**.

Pick the trigger:

- Event: **Work item updated** (status changes and tag edits).
- A second subscription with the event **Work item commented on**, if you want comments carrying the project's `comment_keyword` to start runs. Azure DevOps delivers a comment only through this event; the comment text is read from `System.History`.
- Filters: **Area Path** = the area path your project lives under (or leave blank to receive everything from the project). **Work item type** = whatever types you want to trigger from (User Story, Bug, Task).

For the action:

- URL: `https://agent-smith.your-host.example/webhook`
- Resource details to send: **All**.
- Messages to send: **None**.
- Detailed messages to send: **None** (the framework reads the payload).

Azure DevOps sends no platform header by default, so point these subscriptions at `POST /webhook`: the server recognises Azure DevOps from the payload itself (its `publisherId` and `eventType`). The per-platform routes (`/webhook/github`, `/webhook/gitlab`) never read a payload that way.

If you also subscribe to **Pull request updated**, filter it to **Source branch updated**: every `git.pullrequest.updated` delivery starts a PR review, whatever changed.

> **Set `AZDO_WEBHOOK_SECRET`.** With it unset, every delivery to `/webhook` that reads as Azure DevOps is accepted unverified.

Azure DevOps doesn't sign webhook payloads by default. Add a Basic Auth header instead — set the **Basic authentication** password in the Service Hook setup, and give the server the same value via the `AZDO_WEBHOOK_SECRET` environment variable:

```bash
# on the server process (compose env, k8s Secret)
AZDO_WEBHOOK_SECRET=...
```

The server compares the `Authorization` header against it and rejects mismatches. If the variable is unset, requests without the header pass — set it.

Verify the wiring with a test work item: tag it `TodoList` and the trigger label, set status to `Active`. Within a second the server log shows the delivery and the project it resolved to, and the run appears in the dashboard.

## Jira

In Jira Cloud: **System → System Webhooks → Create a Webhook**.

- URL: `https://agent-smith.your-host.example/webhook/jira`
- Events: **Issue created**, **Issue updated**. Work starts when an issue is assigned to the Agent Smith user (`assignee_name` on the project's `jira_trigger`, default `Agent Smith`).
- JQL filter: `project = TL` (or whatever your project key is) — narrows webhooks to just the project Agent Smith manages.
- Secret: paste your `JIRA_WEBHOOK_SECRET` value. Once the project carries a secret, every delivery must arrive with a matching `x-hub-signature` — one without it is refused with `401`.

Jira is the one platform where the secret is referenced from the configuration (per project, on the trigger block) rather than read from a server env var directly. On a server that reference is set in the project drawer. A `${NAME}` reference resolves against the `secrets:` map first and then against the environment variable of that name, and a reference that resolves to nothing counts as unconfigured — so a placeholder nobody exported leaves the platform open rather than turning every delivery into a `401`:

```yaml
projects:
  acme-rules:
    # ...
    jira_trigger:
      secret: ${JIRA_WEBHOOK_SECRET}
      # ...
```

Jira Cloud **system** webhooks don't send a signature header at all. If you rely on those, leave the project's secret unset — the platform then falls open and every delivery is accepted; use the JQL filter plus network-level controls to keep the endpoint quiet. Setting a secret and using a webhook that cannot sign gives you a `401` on every delivery.

For Jira Server / Data Center the webhook UI is similar but lives under **System → Webhooks**.

## GitHub

Either a per-repo webhook or a single org-level webhook.

**Per-repo** (for one or two repos): **Repo Settings → Webhooks → Add webhook**.

**Org-level** (recommended when you have many repos): **Org Settings → Webhooks → Add webhook**.

Either way:

- Payload URL: `https://agent-smith.your-host.example/webhook/github`
- Content type: **application/json**
- Secret: paste your `GITHUB_WEBHOOK_SECRET` value.
- Events: **Issues** (state changes + label adds), optionally **Issue comments** (if you want comment-driven triggers via the project's `comment_keyword`), and **Pull requests** plus **Issue comments** if you want [PR commands](../reference/integrations/pr-comments.md) and review labels.

Give the server the same value as an environment variable:

```bash
GITHUB_WEBHOOK_SECRET=...
```

GitHub HMACs the body with the secret and sends it in `X-Hub-Signature-256`. The server verifies.

## GitLab

**Project Settings → Webhooks** (or for group-wide: **Group Settings → Webhooks**).

- URL: `https://agent-smith.your-host.example/webhook/gitlab`
- Trigger: **Issues events**, plus **Comments** if you want issue comments carrying the project's `comment_keyword` to start runs (the same **Comments** event also carries [MR commands](../reference/integrations/pr-comments.md)), and **Merge request events** for review labels. Every **Issues events** delivery reaches the issue handler, which starts a run when the issue sits in a trigger status.
- Secret token: paste your `GITLAB_WEBHOOK_TOKEN` value.

Give the server the same value as an environment variable:

```bash
GITLAB_WEBHOOK_TOKEN=...
```

GitLab sends the token in the `X-Gitlab-Token` header, plain text (not HMAC). The server string-compares.

## Rework a finished ticket

A comment carrying the project's `comment_keyword` on a ticket whose last run finished — the ticket sits in `done_status` or `failed_status`, or the last run could not move it — starts exactly one new attempt. Agent Smith moves the ticket back to the first `trigger_statuses` entry when it sits outside them, clears what held it, and starts the run; the run reads the comment itself and leads its prompt with everything written since the previous attempt started. Ordinary comments start nothing. While a run is working on the ticket, a keyword comment starts no second run: Agent Smith says so on the ticket, and you comment again once that run has finished. A ticket parked as not implementable comes back only through Retry.

## Reachability

Webhooks need a publicly-reachable URL for the orchestrator. Three common shapes:

- **Public ingress** — orchestrator behind your standard ingress / load balancer. TLS terminates there.
- **Cloudflare Tunnel / ngrok** — for development or for trackers you don't want to expose your network to. Free Cloudflare Tunnel works fine.
- **VPN / private link** — for Azure DevOps Server or Jira Server inside a corporate network, with the tracker and the orchestrator on the same VPN.

If you can't reach the orchestrator from the tracker, use [polling](polling.md) instead.

## Pull request review label

A label on a GitHub pull request or GitLab merge request can ask for a review, which runs `security-scan` on that repository. `security-review` always does. To use your own word as well, set `pr_trigger_label` on the owning project's `github_trigger` or `gitlab_trigger`:

```yaml
projects:
  todolist:
    github_trigger:
      pr_trigger_label: needs-review
```

## Pull request comment commands

A [PR comment command](../reference/integrations/pr-comments.md) runs only when its author can write to the repository, and on two platforms the server has to ask. The token it asks with needs more than cloning does:

- **GitLab** — the token behind the repo's `auth` secret, with the `read_api` scope, to read the project's members.
- **Azure DevOps** — the token behind the repo's `auth` secret, with **Identity (Read)** and **Security (Manage)**, to read the author's effective permission on the repository. Azure DevOps offers no read-only security scope.

GitHub needs nothing extra: the payload carries the author's standing. Without the scopes every command on that platform is ignored, since a lookup that fails counts as no write access.

## What the framework does on receipt

1. Detect the platform and verify the secret (HMAC for GitHub and Jira, token compare for GitLab, basic-auth for Azure DevOps). Wherever a secret is configured this is mandatory: a delivery without a valid signature — an absent header included — is refused with `401` before any handler runs. A platform with no secret configured is not verified at all.
2. Parse the payload, extract the ticket id and the changed fields.
3. Decide if this event matters: did the status change to one of `trigger_statuses`? Did a `pipeline_from_label` label get added? Did a comment with the project's `comment_keyword` land? Was a Jira issue assigned to the Agent Smith user? Then find the project the ticket belongs to (see [Project resolution](../reference/configuration/project-resolution.md)). If nothing matches, return 200 and stop.
4. If the event matters: claim the ticket — the claim is a database lease, so a webhook and a poll racing on the same ticket can't double-trigger, and one ticket never has two live runs (that's enforced by construction, not by timing). Then check capacity: if the run's whole footprint doesn't fit right now, it queues in strict FIFO order instead of failing (see [Capacity & queueing](../reference/operations/capacity.md)).
5. The run spawns and executes; every state change lands in the [dashboard](../reference/operations/dashboard.md).

A delivery the framework acted on gets `202`, one it filtered out gets `200`; both tell the tracker not to retry. A signature failure gets `401`. Errors during the run itself land in the ticket as a comment. The full table is on the [webhook reference](../reference/configuration/webhooks.md#ticket-trigger-flow).

## Next

- [Polling](polling.md) — the fallback when webhooks aren't an option.
- [Labels](labels.md) — `pipeline_from_label` and the `agent-smith:*` lifecycle labels.
- [Host it](../host-it/docker-compose.md) — for the public URL, you need a real host. docker-compose + a reverse proxy is the easiest path.
