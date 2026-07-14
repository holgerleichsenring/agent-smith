# Webhook Configuration

Agent Smith receives platform events via webhooks. Two distinct flows go through the receiver:

- **Ticket triggers** (issue/work-item labelled, assigned, etc.) — these enter `TicketClaimService` and follow the [ticket lifecycle](../concepts/ticket-lifecycle.md). All four platforms supported since p0095b.
- **PR comment commands and dialogue answers** — free-form path, fire-and-forget in-process. GitHub (p0059), GitLab (p0059b), and Azure DevOps (p0059c) all supported.

Polling is the alternative ingress for ticket triggers. See [Polling](../../trigger-it/polling.md).

## Ticket Trigger Flow

When a webhook arrives for a ticket event:

```
Platform webhook
      │ POST /webhook
      ▼
WebhookSignatureVerifier  (rejects with 401 on bad signature)
      │
      ▼
Platform-specific handler  (GitHubIssueWebhookHandler, JiraAssigneeWebhookHandler, ...)
      │ returns WebhookResult { ProjectName, TicketId, Pipeline, Platform }
      ▼
WebhookRequestProcessor.RouteToClaimServiceAsync
      │
      ▼
TicketClaimService.ClaimAsync
      │ pre-checks → SETNX claim-lock → status read → atomic transition → enqueue
      ▼
IRedisJobQueue (RPUSH agentsmith:queue:jobs)
      │
      ▼  (asynchronous — pipeline runs in PipelineQueueConsumer on some pod)
HTTP response to platform
```

The HTTP response from the receiver indicates the **claim outcome**, not pipeline completion:

| `ClaimResult.Outcome` | HTTP status | Body |
|-----------------------|:-----------:|------|
| `Claimed` | 202 | `Accepted: {ticket} in {project}` |
| `AlreadyClaimed` | 200 | `Already claimed: {ticket}` (idempotent — safe to retry) |
| `Rejected` | 200 | `Rejected: UnknownProject` (or `UnknownPipeline` / `PipelineNotLabelTriggered`) |
| `Failed` | 500 | `Claim failed: {error}` (the platform's retry kicks in) |

## Supported Platforms

| Platform | Trigger events | PR comment commands | Signature method |
|----------|----------------|:-------------------:|------------------|
| GitHub | `issues` (labeled) | Yes | HMAC-SHA256 (`X-Hub-Signature-256`) |
| GitLab | `Issue Hook` (labeled) | Yes | Token header (`X-Gitlab-Token`) |
| Azure DevOps | `workitem.updated` | Yes | Basic auth |
| Jira | `issue_updated`, `comment_created` | No (planned) | HMAC (optional) |

## Webhook Secrets

Webhook secrets are **environment variables on the server process** — there is no `webhooks:` block in `agentsmith.yml`:

| Env var | Platform | Verification |
|---------|----------|--------------|
| `GITHUB_WEBHOOK_SECRET` | GitHub | HMAC (`X-Hub-Signature-256`) |
| `GITLAB_WEBHOOK_TOKEN` | GitLab | Token compare (`X-Gitlab-Token`) |
| `AZDO_WEBHOOK_SECRET` | Azure DevOps | Basic auth |

Jira is the exception: its secret lives in config, per project, under `projects.<name>.jira_trigger.secret`.

!!! tip "Development mode"
    Empty secret = signature verification skipped. Useful for local ngrok testing; never use in production.

## Endpoints

The webhook receiver listens on port **8081**:

```
POST /webhook           # platform auto-detected from headers/payload
POST /webhook/github    # explicit GitHub endpoint
POST /webhook/gitlab    # explicit GitLab endpoint
POST /webhook/jira      # explicit Jira endpoint
GET  /health            # liveness check
```

On the generic `/webhook` endpoint, the `X-GitHub-Event`, `X-Gitlab-Event`, etc. headers (and payload shape) route to the right handler; the explicit per-platform endpoints skip detection. `IWebhookHandler.CanHandle(platform, eventType)` selects the matching handler at dispatch time.

## Trigger Configuration

The trigger config (`pipeline_from_label`, `default_pipeline`, `done_status`, ...) lives per project under `github_trigger`/`gitlab_trigger`/`azuredevops_trigger`/`jira_trigger`. See [Label-Based Triggers](../../trigger-it/labels.md) for the full shape and per-platform examples.

## PR Comment Commands

Independent of the ticket lifecycle. Comments like `/agent-smith fix-bug` start an ad-hoc pipeline (no claim flow, no lifecycle labels). Configured per project:

```yaml
projects:
  my-api:
    pr_commands:
      enabled: true
      require_member: true
      allowed_pipelines:
        - fix-bug
        - security-scan
        - pr-review
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `enabled` | bool | `false` | Enable `/agent-smith` commands in PR comments |
| `require_member` | bool | `true` | Only allow repo members to issue commands |
| `allowed_pipelines` | list | all | Restrict which pipelines can be started via PR comments |

See [PR Comment Integration](../integrations/pr-comments.md) for command syntax.

## Per-Platform Setup

Per-platform walkthroughs (payload URLs, events to subscribe, secret placement in each platform's UI) live in [Webhooks](../../trigger-it/webhooks.md) — that page is canonical.

## Idempotency Guarantee

Webhook redelivery is safe. The first delivery wins the SETNX claim-lock and transitions `Pending → Enqueued`; the second delivery sees the ticket in `Enqueued` (or further along) and returns `AlreadyClaimed` (HTTP 200). No duplicate pipeline runs.

This makes Agent Smith's webhook receiver tolerant of platform retry policies, network glitches, and operator-triggered redeliveries.

## Related

- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — what happens after the claim succeeds
- [Label-Based Triggers](../../trigger-it/labels.md) — trigger config shapes per platform
- [Polling](../../trigger-it/polling.md) — alternative ingress, and when to choose it
- [Webhooks](../../trigger-it/webhooks.md) — per-platform setup walkthroughs
- [PR Comments](../integrations/pr-comments.md) — free-form trigger path
