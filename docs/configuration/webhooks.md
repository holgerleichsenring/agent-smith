# Webhook Configuration

Agent Smith receives events from Git platforms via webhooks. Webhooks are used for issue-based triggers (labeling), PR comment commands, and dialogue answers.

## Supported Platforms

| Platform | Event Types | Signature Method | Status |
|----------|-------------|------------------|--------|
| GitHub | `issues`, `issue_comment`, `pull_request`, `pull_request_review_comment` | HMAC-SHA256 | Supported |
| GitLab | `merge_request` (label events) | Token header | Supported |
| Azure DevOps | Work item updates | Basic auth | Supported |
| Jira | `issue_updated`, `comment_created` | HMAC (optional) | Supported |

PR comment commands (`/agent-smith`, `/approve`, `/reject`) are currently GitHub-only. GitLab and Azure DevOps support is planned.

## Webhook Secret

```yaml
webhooks:
  github_secret: ${GITHUB_WEBHOOK_SECRET}
```

The secret is used for HMAC-SHA256 signature verification of incoming GitHub webhooks. The `X-Hub-Signature-256` header is validated against this secret.

!!! tip "Development mode"
    If `github_secret` is empty or not set, signature verification is skipped. This is useful for local development but must never be used in production.

## PR Comment Commands

Enable PR comment commands per project:

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

## GitHub Webhook Setup

1. Repository Settings > Webhooks > Add webhook
2. Set Payload URL to `https://your-host/webhook`
3. Set Content type to `application/json`
4. Enter the webhook secret
5. Select events: **Issue comments**, **Pull request review comments**, and optionally **Issues** and **Pull requests** for label-based triggers

## Jira Webhook Setup

1. Jira Settings > System > WebHooks (Cloud: Apps > Webhooks)
2. Set URL to `https://your-host/webhook/jira`
3. Enable events: **Issue updated**, **Comment created**
4. Optionally set a secret (matched against `JIRA_WEBHOOK_SECRET`)

### Jira Trigger Configuration

```yaml
projects:
  my-api:
    jira_trigger:
      assignee_name: "Agent Smith"
      secret: ${JIRA_WEBHOOK_SECRET}       # optional
      trigger_statuses: ["Open", "Active"]  # only trigger in these statuses
      done_status: "In Review"              # transition after PR creation
      pipeline_from_label:                  # label → pipeline mapping
        bug: fix-bug
        feature: implement-feature
        security-review: security-scan
      default_pipeline: fix-bug             # fallback if no label matches
      comment_keyword: "@agent-smith"       # optional: re-trigger via comment
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `assignee_name` | string | `Agent Smith` | Jira display name that triggers the webhook |
| `secret` | string | — | Optional HMAC secret for signature verification |
| `trigger_statuses` | list | `[Open]` | Issue must be in one of these statuses to trigger |
| `done_status` | string | `In Review` | Target status after PR is created |
| `pipeline_from_label` | map | — | Maps Jira labels to pipeline names (config order = priority) |
| `default_pipeline` | string | `fix-bug` | Pipeline when no label matches |
| `comment_keyword` | string | — | Keyword in comments that triggers a pipeline run |

### Trigger Flow

1. Issue assigned to configured user → webhook fires
2. Agent Smith checks: assignee match + label match + status in whitelist
3. Pipeline runs, creates PR
4. Ticket transitions to `done_status`
5. If review fails and ticket is moved back to "Open", it can be re-triggered
6. Alternatively, a comment containing the keyword re-triggers immediately

## Webhook Endpoints

Agent Smith exposes platform-specific webhook endpoints:

```
POST /webhook         # GitHub, GitLab, Azure DevOps (auto-detected via headers)
POST /webhook/jira    # Jira (event type extracted from payload body)
```

The `X-GitHub-Event` header (or equivalent for other platforms) determines which handler processes the event. Jira uses a dedicated endpoint because it has no reliable event-type header. Handlers are registered via dependency injection and selected by the `IWebhookHandler.CanHandle()` method.

See also: [PR Comment Integration](../integrations/pr-comments.md) for command syntax and usage.
