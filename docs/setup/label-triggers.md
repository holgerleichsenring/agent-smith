# Label-Based Triggers

Agent Smith starts pipelines when configured labels are added to issues, work items, or merge requests. All four platforms (GitHub, GitLab, Azure DevOps, Jira) support the same trigger model since p0084.

The trigger fires via either path:

- **Webhook** (push from the platform) — sub-second latency, requires reachable endpoint.
- **Polling** (Agent Smith pulls eligible tickets) — interval-based, no inbound network needed.

Both feed the same `TicketClaimService`. See [Polling vs Webhooks](polling-vs-webhooks.md) for the choice.

## How It Works

1. A user adds a configured trigger label to a ticket.
2. Webhook delivery (or the next poll cycle) surfaces the labelled ticket to Agent Smith.
3. `TicketClaimService` runs pre-checks, acquires a SETNX claim-lock, and atomically transitions the ticket from `Pending` to `Enqueued` (writing the `agent-smith:enqueued` lifecycle label).
4. The pipeline mapped to the trigger label runs from a Redis-backed queue.
5. Lifecycle labels reflect progress on the ticket itself: `agent-smith:in-progress`, then `agent-smith:done` or `agent-smith:failed`.

For the full state machine and recovery semantics, see [Ticket Lifecycle](../concepts/ticket-lifecycle.md).

## Platform Support

| Platform | Trigger config key | Webhook | Polling | Lifecycle labels |
|----------|--------------------|:-------:|:-------:|:---:|
| **GitHub** | `github_trigger` | Yes | Yes | Yes |
| **GitLab** | `gitlab_trigger` | Yes | Planned | Yes |
| **Azure DevOps** | `azuredevops_trigger` | Yes | Planned | Yes |
| **Jira** | `jira_trigger` | Yes | Planned | Yes (label-mode) |

All four use the same `WebhookTriggerConfig` shape (`pipeline_from_label`, `default_pipeline`, `trigger_statuses`, `done_status`); Jira extends it with `JiraTriggerConfig` (assignee gating).

## Trigger Config: Common Shape

```yaml
projects:
  my-api:
    github_trigger:                           # or gitlab_trigger / azuredevops_trigger / jira_trigger
      pipeline_from_label:
        agent-smith: fix-bug
        security-review: security-scan
        legal-review: legal-analysis
      default_pipeline: fix-bug               # fallback when no label matches
      trigger_statuses: []                    # empty = all states allowed
      done_status: "In Review"                # post-PR transition (Jira native, label elsewhere)
```

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `pipeline_from_label` | map | `{}` | Trigger label → pipeline name |
| `default_pipeline` | string | `fix-bug` | Used when label doesn't match any entry |
| `trigger_statuses` | list | `[]` | Allowed ticket states (empty = all) |
| `done_status` | string | `"In Review"` | Status set on ticket after PR creation |
| `comment_keyword` | string? | none | Optional keyword that re-triggers on comment |

**Matching rules:**

- Labels are checked in config order — first match wins.
- Multiple labels on a ticket: the first one matching a config key applies.
- No label matches: `default_pipeline` runs.
- Case-insensitive.

## Per-Platform Examples

### GitHub

```yaml
projects:
  my-api:
    tickets:
      type: GitHub
      url: https://github.com/org/my-api
    github_trigger:
      pipeline_from_label:
        agent-smith: fix-bug
        security-review: security-scan
      default_pipeline: fix-bug
```

Adding the `agent-smith` label to issue #42 triggers `fix-bug`. Adding `security-review` triggers `security-scan`. See [GitHub Webhook Setup](webhooks/github.md).

### GitLab

```yaml
projects:
  my-api:
    tickets:
      type: GitLab
      project: my-org/my-api
    gitlab_trigger:
      pipeline_from_label:
        agent-smith: fix-bug
        security-review: security-scan
      default_pipeline: fix-bug
```

See [GitLab Webhook Setup](webhooks/gitlab.md).

### Azure DevOps

```yaml
projects:
  my-api:
    tickets:
      type: AzureDevOps
      organization: my-org
      project: my-project
    azuredevops_trigger:
      pipeline_from_label:                    # tags, in AzDO terminology
        agent-smith: fix-bug
        security-review: security-scan
      default_pipeline: fix-bug
```

See [Azure DevOps Webhook Setup](webhooks/azure-devops.md).

### Jira

Jira extends the common shape with assignee gating:

```yaml
projects:
  my-api:
    tickets:
      type: Jira
      url: https://your-org.atlassian.net
    jira_trigger:
      assignee_name: "Agent Smith"
      trigger_statuses: ["Open", "Active"]
      done_status: "In Review"
      pipeline_from_label:
        bug: fix-bug
        feature: add-feature
        security-review: security-scan
        legal: legal-analysis
      default_pipeline: fix-bug
```

The trigger fires only when the issue is assigned to `assignee_name` AND its status is in `trigger_statuses` AND it has a label in `pipeline_from_label`. See [Jira Webhook Setup](webhooks/jira.md).

## Lifecycle Labels Are Separate

The trigger labels above (`agent-smith`, `security-review`, etc.) are user-driven — operators or automations add them to start work. They are distinct from the framework-managed lifecycle labels (`agent-smith:pending`, `agent-smith:enqueued`, etc.) that Agent Smith writes during the claim flow. Trigger labels remain on the ticket; lifecycle labels are added/removed atomically per state transition.

If you see both an `agent-smith` trigger label AND an `agent-smith:in-progress` lifecycle label, that's expected — the trigger said "this ticket should be worked", the lifecycle says "currently being worked".

## Idempotency

Re-delivery of a webhook (or a polling cycle that picks up the same ticket twice) cannot start two pipelines. The SETNX claim-lock plus atomic transitioner ensures the second attempt sees the ticket in `Enqueued` (or later) and returns `AlreadyClaimed`. Safe to replay events.

## Related

- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — what happens after the trigger fires
- [Webhook Configuration](../configuration/webhooks.md) — secrets, signatures, response codes
- [Polling Setup](polling.md) — alternative ingress
- [Polling vs Webhooks](polling-vs-webhooks.md) — choosing the right path
