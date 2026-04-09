# PR Comment Integration

Agent Smith can be triggered and controlled directly from pull request comments. Two scenarios share the same webhook infrastructure.

## Scenario A: Start a New Job

Write a comment on any PR to start a pipeline:

```
/agent-smith fix                         # fix-bug pipeline for this PR
/agent-smith fix #123 in my-api          # fix-bug for a specific ticket
/agent-smith security-scan               # security scan for this PR
/agent-smith review                      # PR review pipeline
/agent-smith help                        # list available commands
```

The short alias `/as` is also supported (`/as fix`, `/as security-scan`).

Without parameters (`/agent-smith fix`), the PR description and comments are used directly as context -- no separate ticket needed.

## Scenario B: Control a Running Job

When Agent Smith posts a question in the PR (via [Interactive Dialogue](../concepts/interactive-dialogue.md)), respond with:

```
/approve                                 # confirm (yes)
/approve Please rename the branch        # confirm with comment
/reject                                  # reject (no)
/reject The naming convention is wrong   # reject with reason
```

Commands are case-insensitive. The answer is forwarded to the running job via Redis, and the pipeline continues.

## Webhook Setup (GitHub)

1. Go to **Repository Settings** > **Webhooks** > **Add webhook**
2. **Payload URL:** `https://your-agent-smith-host/webhook`
3. **Content type:** `application/json`
4. **Secret:** a strong random string (same as `GITHUB_WEBHOOK_SECRET` env var)
5. **Events:** select "Issue comments" and "Pull request review comments"

The webhook handler responds to two GitHub event types:

| Event | Action | Meaning |
|-------|--------|---------|
| `issue_comment` | `created` | Comment on a PR (GitHub treats PRs as issues) |
| `pull_request_review_comment` | `created` | Inline code comment on a PR |

## Security

### Signature Verification

All incoming webhooks are verified using HMAC-SHA256. The `X-Hub-Signature-256` header must match the configured secret:

```yaml
webhooks:
  github_secret: ${GITHUB_WEBHOOK_SECRET}
```

In development mode (no secret configured), signature verification is skipped.

### Access Control

```yaml
projects:
  my-api:
    pr_commands:
      enabled: true
      require_member: true         # only repo members can issue commands
      allowed_pipelines:           # restrict which pipelines can be started
        - fix-bug
        - security-scan
        - pr-review
```

- **`require_member: true`** -- checks `author_association` in the webhook payload. Only repository members, collaborators, and owners can execute commands.
- **`allowed_pipelines`** -- limits which pipelines can be triggered via PR comments. Commands for unlisted pipelines are rejected.
- **Duplicate protection** -- if a job is already running for the PR, a second `/agent-smith` command is rejected with a message to wait.

## How It Works

```
GitHub PR Comment
    |
    v
POST /webhook (HMAC-SHA256 verified)
    |
    v
GitHubPrCommentWebhookHandler
    |
    v
CommentIntentParser  (regex: /agent-smith, /approve, /reject)
    |
    v
CommentIntentRouter
    |
    +-- NewJob?     --> IJobEnqueuer --> container/K8s job starts
    +-- Approve?    --> Redis job:{id}:in --> running job continues
    +-- Reject?     --> Redis job:{id}:in --> running job aborts
    +-- Help?       --> reply with command list
    +-- Unknown?    --> ignored (not every comment is a command)
```

All acknowledgments and status updates are posted back as PR comments.

## Platform Support

| Platform | Status |
|----------|--------|
| GitHub | Supported (p59) |
| GitLab | Planned (p59b) |
| Azure DevOps | Planned (p59c) |

See also: [Webhook Configuration](../configuration/webhooks.md) for the full configuration reference.
