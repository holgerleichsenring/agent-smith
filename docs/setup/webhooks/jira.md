# Jira Webhook Setup

Connect Jira to Agent Smith to trigger pipelines when issues are assigned, and re-trigger via comments.

## Prerequisites

- Agent Smith running in server mode (`agent-smith server --port 8081`)
- Public URL reachable from Jira Cloud (e.g. via ngrok: `ngrok http 8081`)
- Jira project with admin access

## Supported Events

| Event | Handler | What it does |
|-------|---------|--------------|
| `jira:issue_updated` (assignee change) | JiraAssigneeWebhookHandler | Triggers pipeline when issue is assigned to configured user, status is in whitelist, and label determines pipeline |
| `jira:comment_created` | JiraCommentWebhookHandler | Re-triggers pipeline when comment contains configured keyword |

## Step-by-Step Setup

### 1. Create Webhook in Jira

**Jira Cloud:**

1. Go to **Settings > System > WebHooks** (or **Apps > Webhooks**)
2. **URL:** `https://your-host/webhook/jira`
3. **Events:** Enable:
    - **Issue updated**
    - **Issue comment created**
4. **Secret:** Optional — set if you want HMAC signature verification
5. Save

!!! warning "Dedicated endpoint"
    Jira uses `/webhook/jira`, not `/webhook`. This is because Jira does not send an event-type header — the event type is extracted from the payload body.

### 2. Configure Agent Smith

Add a `jira_trigger` block to your project in `agentsmith.yml`:

```yaml
projects:
  my-api:
    source:
      type: GitHub
      url: https://github.com/owner/repo
      auth: github_token
    tickets:
      type: Jira
      url: https://your-org.atlassian.net
      auth: jira_token
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

If using a secret, set the environment variable:

```bash
export JIRA_WEBHOOK_SECRET="your-secret-here"
```

### 3. Configuration Reference

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `assignee_name` | string | `Agent Smith` | Jira display name that triggers the webhook |
| `secret` | string | — | Optional HMAC secret for signature verification |
| `trigger_statuses` | list | `["Open"]` | Issue must be in one of these statuses to trigger |
| `done_status` | string | `In Review` | Target status after PR is created |
| `pipeline_from_label` | map | — | Maps Jira labels to pipeline names (config order = priority) |
| `default_pipeline` | string | `fix-bug` | Pipeline when no label matches |
| `comment_keyword` | string | — | Keyword in comments that triggers a pipeline run |

### 4. Verify

1. Create a Jira issue with label `bug` and status "Open"
2. Assign it to the configured user (e.g. "Agent Smith")
3. Check Agent Smith logs for: `Jira trigger: issue PROJ-123 assigned to 'Agent Smith' -> pipeline 'fix-bug'`

## Trigger Flow

```
Open/Active ──webhook──→ Agent Smith works ──→ PR created ──→ Ticket → "In Review"
    ↑                                                              |
    └──── Human moves back to Open ←── Review not OK ─────────────┘
```

1. Issue assigned to configured user → webhook fires
2. Agent Smith checks: **assignee match** + **status in whitelist** + **label determines pipeline**
3. Pipeline runs, creates PR
4. Ticket transitions to `done_status` (e.g. "In Review")
5. If review fails and ticket is moved back to "Open", next assignment triggers again
6. Alternatively, a comment containing the keyword (e.g. `@agent-smith`) re-triggers immediately

## Comment Re-Trigger

If `comment_keyword` is configured, adding a comment containing that keyword to an issue will trigger the pipeline — provided the issue status is in `trigger_statuses`. This allows re-triggering without the assign/unassign cycle.

Example: Comment `@agent-smith please retry` on an issue in "Open" status.

## Signature Verification

If `secret` is configured, Agent Smith validates the `x-hub-signature` header using HMAC. If no secret is configured, verification is skipped.

!!! tip "Internal networks"
    Skipping the secret is acceptable for internal networks where Jira and Agent Smith are on the same infrastructure.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| Webhook returns 401 | Check `JIRA_WEBHOOK_SECRET` matches the secret configured in Jira |
| Assignee event ignored | Verify `assignee_name` matches the Jira display name exactly (case-insensitive) |
| Status gate blocks trigger | Check issue status is in `trigger_statuses` — names are case-insensitive |
| Comment trigger not working | Verify `comment_keyword` is set and the comment contains the keyword |
| Wrong pipeline selected | Labels are matched in config order (first match wins) — check `pipeline_from_label` order |
| Ticket not transitioning | Verify `done_status` matches an available transition name in your Jira workflow |
