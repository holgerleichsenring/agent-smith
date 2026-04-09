# Webhook Configuration

Agent Smith receives events from Git platforms via webhooks. Webhooks are used for issue-based triggers (labeling), PR comment commands, and dialogue answers.

## Supported Platforms

| Platform | Event Types | Signature Method | Status |
|----------|-------------|------------------|--------|
| GitHub | `issues`, `issue_comment`, `pull_request`, `pull_request_review_comment` | HMAC-SHA256 | Supported |
| GitLab | `merge_request` (label events) | Token header | Supported |
| Azure DevOps | Work item updates | Basic auth | Supported |

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

## Webhook Endpoint

Agent Smith exposes a single webhook endpoint:

```
POST /webhook
```

The `X-GitHub-Event` header (or equivalent for other platforms) determines which handler processes the event. Handlers are registered via dependency injection and selected by the `IWebhookHandler.CanHandle()` method.

See also: [PR Comment Integration](../integrations/pr-comments.md) for command syntax and usage.
