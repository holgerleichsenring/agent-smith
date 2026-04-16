# Integration Setup Guides

Step-by-step guides for connecting Agent Smith to chat platforms and external services.

## Chat Platforms

| Platform | Guide | Status |
|----------|-------|--------|
| Slack | [Slack Setup](slack.md) | Production-ready |
| Teams | [Teams Setup](teams.md) | Beta |

## Webhook Setup

| Platform | Guide | Status |
|----------|-------|--------|
| GitHub | [GitHub Webhooks](webhooks/github.md) | Supported |
| GitLab | [GitLab Webhooks](webhooks/gitlab.md) | Supported |
| Azure DevOps | [Azure DevOps Webhooks](webhooks/azure-devops.md) | Supported |
| Jira | [Jira Webhooks](webhooks/jira.md) | Supported (full lifecycle) |

## Label & Tag Triggers

| Topic | Guide | Status |
|-------|-------|--------|
| [Label-based Triggers](label-triggers.md) | How labels/tags map to pipelines | Jira: full, others: hardcoded (p84) |

## Related

- [Webhook Configuration Reference](../configuration/webhooks.md) — secrets, PR comment commands, endpoint details
- [Chat Gateway Architecture](../deployment/chat-gateway.md) — how the Dispatcher works internally
