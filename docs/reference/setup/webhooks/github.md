# GitHub Webhook Setup

Connect GitHub to Agent Smith to trigger pipelines from issue labels and PR comments.

## Prerequisites

- Agent Smith running in server mode (`agent-smith server --port 8081`)
- Public URL reachable from GitHub (e.g. via ngrok: `ngrok http 8081`)
- GitHub repository with admin access

## Supported Events

| Event | Handler | What it does |
|-------|---------|--------------|
| `issues` (labeled) | GitHubIssueWebhookHandler | Triggers default pipeline when `agent-smith` label is added to an issue |
| `issue_comment` (created) | GitHubPrCommentWebhookHandler | PR comment commands: `/agent-smith fix-bug`, `/approve`, `/reject` |
| `pull_request_review_comment` (created) | GitHubPrCommentWebhookHandler | Same as above, but on review comments |

## Step-by-Step Setup

### 1. Create Webhook

1. Go to **Repository Settings > Webhooks > Add webhook**
2. **Payload URL:** `https://your-host/webhook`
3. **Content type:** `application/json`
4. **Secret:** Enter a strong secret (will be used for signature verification)
5. **Events:** Select "Let me select individual events", then check:
    - **Issues** (for label-based triggers)
    - **Issue comments** (for PR comment commands)
    - **Pull request review comments** (for inline PR comment commands)

### 2. Configure Agent Smith

Set the webhook secret as environment variable:

```bash
export GITHUB_WEBHOOK_SECRET="your-secret-here"
```

### 3. Configure Issue Trigger (optional)

Currently, the GitHub issue handler triggers when the `agent-smith` label is added to an issue. The pipeline is the project's default pipeline.

!!! note "Label-to-pipeline mapping"
    Configurable label-to-pipeline mapping (like Jira's `pipeline_from_label`) is planned for GitHub in p0084.

### 4. Configure PR Comment Commands

PR comment commands allow repo members to trigger pipelines directly from pull requests:

```yaml
projects:
  my-api:
    pr_commands:
      enabled: true
      require_member: true
      allowed_pipelines:
        - fix-bug
        - security-scan
```

### 5. Verify

1. Add the `agent-smith` label to a test issue
2. Check Agent Smith logs for: `GitHub issue labeled: fix #N in repo-name`
3. Or comment `/agent-smith fix-bug` on a PR

## Signature Verification

GitHub sends an `X-Hub-Signature-256` header with every webhook delivery. Agent Smith validates this using the `GITHUB_WEBHOOK_SECRET` environment variable.

If no secret is configured, signature verification is skipped (development only).

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Webhook returns 401 | Check `GITHUB_WEBHOOK_SECRET` matches the secret in GitHub |
| Label event ignored | Verify the label name is exactly `agent-smith` (case-insensitive) |
| PR comment ignored | Check `author_association` — only OWNER, MEMBER, COLLABORATOR, CONTRIBUTOR are trusted |
| No events received | Verify the webhook URL is reachable and events are selected |
