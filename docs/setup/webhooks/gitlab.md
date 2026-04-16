# GitLab Webhook Setup

Connect GitLab to Agent Smith to trigger pipelines from merge request labels and MR comments.

## Prerequisites

- Agent Smith running in server mode (`agent-smith server --port 8081`)
- Public URL reachable from GitLab (e.g. via ngrok: `ngrok http 8081`)
- GitLab project with Maintainer access

## Supported Events

| Event | Handler | What it does |
|-------|---------|--------------|
| Merge Request Hook (update, label added) | GitLabMrLabelWebhookHandler | Triggers `security-scan` pipeline when `security-review` label is added |
| Note Hook (MR comment) | GitLabMrCommentWebhookHandler | PR comment commands: `/agent-smith fix-bug`, `/approve`, `/reject` |

## Step-by-Step Setup

### 1. Create Webhook

1. Go to **Project Settings > Webhooks**
2. **URL:** `https://your-host/webhook`
3. **Secret token:** Enter a token for verification
4. **Trigger events:** Check:
    - **Merge request events**
    - **Comments** (for MR comment commands)
5. Click **Add webhook**

### 2. Configure Agent Smith

Set the webhook token as environment variable:

```bash
export GITLAB_WEBHOOK_TOKEN="your-token-here"
```

### 3. Verify

1. Add the `security-review` label to a merge request
2. Check Agent Smith logs for: `GitLab MR !N labeled for security review`
3. Or comment `/agent-smith fix-bug` on a merge request

## Signature Verification

GitLab sends the token in the `X-Gitlab-Token` header. Agent Smith compares it against the `GITLAB_WEBHOOK_TOKEN` environment variable.

If no token is configured, verification is skipped (development only).

!!! note "Label-to-pipeline mapping"
    Currently, the GitLab MR handler only triggers `security-scan` for the `security-review` label. Configurable label-to-pipeline mapping is planned for p84.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Webhook returns 401 | Check `GITLAB_WEBHOOK_TOKEN` matches the token in GitLab |
| Label event ignored | Verify the label title is exactly `security-review` (case-insensitive) |
| Only "update" actions trigger | This is by design — only label changes on existing MRs trigger, not MR creation |
