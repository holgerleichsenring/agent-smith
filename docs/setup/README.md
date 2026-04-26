# Integration Setup Guides

Step-by-step guides for connecting Agent Smith to chat platforms and ticket-event sources.

## Chat Platforms

| Platform | Guide | Status |
|----------|-------|--------|
| Slack | [Slack Setup](slack.md) | Production-ready |
| Teams | [Teams Setup](teams.md) | Beta |

## Ticket Ingress

Tickets enter Agent Smith via one (or both) of two paths. Both feed the same `TicketClaimService` and the same lifecycle.

| Path | Latency | Network requirement | Setup |
|------|---------|---------------------|-------|
| **Webhook** | Sub-second | Public HTTPS endpoint reachable from the platform | Per-platform setup in the platform UI |
| **Polling** | `interval_seconds ± jitter` (default 60s) | Outbound only — no inbound traffic | One YAML stanza per project |

**Pick webhooks** when you have a reachable endpoint and want minimum latency.
**Pick polling** when you run in a private network with no inbound HTTP, or want a self-healing safety net.
**Use both** for redundancy — claims are idempotent, the first one wins.

Detailed comparison: [Polling vs Webhooks](polling-vs-webhooks.md).

### Webhook Setup

| Platform | Guide | Trigger config | Lifecycle labels |
|----------|-------|:--------------:|:----------------:|
| GitHub | [GitHub Webhooks](webhooks/github.md) | `github_trigger` | Yes |
| GitLab | [GitLab Webhooks](webhooks/gitlab.md) | `gitlab_trigger` | Yes |
| Azure DevOps | [Azure DevOps Webhooks](webhooks/azure-devops.md) | `azuredevops_trigger` | Yes |
| Jira | [Jira Webhooks](webhooks/jira.md) | `jira_trigger` | Yes (label-mode) |

### Polling Setup

| Platform | Guide | Status |
|----------|-------|--------|
| GitHub | [Polling Setup](polling.md) | Supported |
| GitLab | [Polling Setup](polling.md) | Supported |
| Azure DevOps | [Polling Setup](polling.md) | Supported |
| Jira | [Polling Setup](polling.md) | Supported (label-mode) |

## Label & Tag Triggers

How user-added labels (`agent-smith`, `security-review`, etc.) map to pipelines — same model across all four platforms.

| Topic | Guide |
|-------|-------|
| [Label-Based Triggers](label-triggers.md) | Per-platform `pipeline_from_label` config and matching rules |

## Background Reading

- [Ticket Lifecycle](../concepts/ticket-lifecycle.md) — Pending → Enqueued → InProgress → Done/Failed, who owns each transition, what survives crashes
- [Webhook Configuration Reference](../configuration/webhooks.md) — secrets, claim flow, HTTP response codes, PR comment commands
- [agentsmith.yml Reference](../configuration/agentsmith-yml.md) — full config schema with trigger/polling/queue sections
- [Chat Gateway Architecture](../deployment/chat-gateway.md) — how the Slack/Teams Dispatcher works
