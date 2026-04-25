# Webhook Configuration

Agent Smith receives platform events via webhooks. Two distinct flows go through the receiver:

- **Ticket triggers** (issue/work-item labelled, assigned, etc.) — these enter `TicketClaimService` and follow the [ticket lifecycle](../concepts/ticket-lifecycle.md). All four platforms supported since p95b.
- **PR comment commands and dialogue answers** — free-form path, fire-and-forget in-process. Currently GitHub-only with GitLab/AzDO additions in p59b/c.

Polling is the alternative ingress for ticket triggers. See [Polling vs Webhooks](../setup/polling-vs-webhooks.md).

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

```yaml
webhooks:
  github_secret: ${GITHUB_WEBHOOK_SECRET}
  gitlab_secret: ${GITLAB_WEBHOOK_SECRET}
  jira_secret: ${JIRA_WEBHOOK_SECRET}        # optional
```

Each is used to verify the corresponding platform's signature header. Azure DevOps uses Basic auth credentials configured per-subscription in the platform UI.

!!! tip "Development mode"
    Empty secret = signature verification skipped. Useful for local ngrok testing; never use in production.

## Endpoints

```
POST /webhook         # GitHub, GitLab, Azure DevOps  (platform auto-detected from headers)
POST /webhook/jira    # Jira (no reliable event-type header — dedicated endpoint)
GET  /health          # Liveness check
```

The `X-GitHub-Event`, `X-Gitlab-Event`, etc. headers route to the right handler. `IWebhookHandler.CanHandle(platform, eventType)` selects the matching handler at dispatch time.

## Trigger Configuration

The trigger config (`pipeline_from_label`, `default_pipeline`, `done_status`, ...) lives per project under `github_trigger`/`gitlab_trigger`/`azuredevops_trigger`/`jira_trigger`. See [Label-Based Triggers](../setup/label-triggers.md) for the full shape and per-platform examples.

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

### GitHub

1. Repository Settings > Webhooks > Add webhook
2. **Payload URL:** `https://your-host/webhook`
3. **Content type:** `application/json`
4. **Secret:** value of `GITHUB_WEBHOOK_SECRET`
5. **Events:** select **Issues** (for label triggers), **Issue comments** + **Pull request review comments** (for PR commands)

Detailed walkthrough: [GitHub Webhook Setup](../setup/webhooks/github.md).

### GitLab

1. Project Settings > Webhooks
2. **URL:** `https://your-host/webhook`
3. **Secret token:** value of `GITLAB_WEBHOOK_SECRET`
4. **Triggers:** **Issues events**, **Comments**

Detailed walkthrough: [GitLab Webhook Setup](../setup/webhooks/gitlab.md).

### Azure DevOps

1. Project Settings > Service Hooks > Create subscription
2. **Service:** Web Hooks
3. **Trigger:** Work item updated
4. **URL:** `https://your-host/webhook`
5. **HTTP Headers:** Basic auth credentials

Detailed walkthrough: [Azure DevOps Webhook Setup](../setup/webhooks/azure-devops.md).

### Jira

1. System Settings > WebHooks (Cloud: Apps > Webhooks)
2. **URL:** `https://your-host/webhook/jira`
3. **Events:** Issue updated, Comment created
4. **Secret (optional):** value of `JIRA_WEBHOOK_SECRET`

Detailed walkthrough: [Jira Webhook Setup](../setup/webhooks/jira.md).

## Idempotency Guarantee

Webhook redelivery is safe. The first delivery wins the SETNX claim-lock and transitions `Pending → Enqueued`; the second delivery sees the ticket in `Enqueued` (or further along) and returns `AlreadyClaimed` (HTTP 200). No duplicate pipeline runs.

This makes Agent Smith's webhook receiver tolerant of platform retry policies, network glitches, and operator-triggered redeliveries.

## Related

- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — what happens after the claim succeeds
- [Label-Based Triggers](../setup/label-triggers.md) — trigger config shapes per platform
- [Polling Setup](../setup/polling.md) — alternative ingress
- [Polling vs Webhooks](../setup/polling-vs-webhooks.md) — choosing the path
- [PR Comments](../integrations/pr-comments.md) — free-form trigger path
