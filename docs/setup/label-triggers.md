# Label-Based Triggers

Agent Smith can automatically start pipelines when specific labels or tags are added to issues, work items, or merge requests.

## How It Works

1. A user adds a label/tag to a ticket in the external system
2. The platform sends a webhook to Agent Smith
3. Agent Smith matches the label against configured triggers
4. The corresponding pipeline runs automatically

## Current Platform Support

| Platform | Trigger Mechanism | Label/Tag | Pipeline | Configurable |
|----------|-------------------|-----------|----------|:---:|
| **Jira** | Issue assigned + label | Any label via `pipeline_from_label` | Any pipeline | Yes |
| **GitHub** | Issue labeled | `agent-smith` (hardcoded) | Project default | No (p84) |
| **GitLab** | MR label added | `security-review` (hardcoded) | `security-scan` | No (p84) |
| **Azure DevOps** | Work item tag | `security-review` (hardcoded) | `security-scan` | No (p84) |

!!! info "Jira is fully configurable"
    Jira supports the full lifecycle: configurable labels, status gates, done-status transitions, and comment re-triggers. See [Jira Webhook Setup](webhooks/jira.md) for details.

!!! note "GitHub, GitLab, Azure DevOps"
    These platforms currently use hardcoded label/tag names and pipelines. Configurable label-to-pipeline mapping (matching Jira's `pipeline_from_label` pattern) is planned for **p84**.

## Jira: Full Label-to-Pipeline Mapping

Jira is the only platform that currently supports configurable label-to-pipeline mapping:

```yaml
projects:
  my-api:
    jira_trigger:
      assignee_name: "Agent Smith"
      trigger_statuses: ["Open", "Active"]
      done_status: "In Review"
      pipeline_from_label:
        bug: fix-bug
        feature: implement-feature
        security-review: security-scan
        legal: legal-analysis
      default_pipeline: fix-bug
```

**How matching works:**

- Labels are matched in **config order** (first match wins)
- If an issue has multiple labels, the first one matching a config key is used
- If no label matches, `default_pipeline` is used
- Matching is case-insensitive

## GitHub: Issue Labels

Add the `agent-smith` label to any issue to trigger the project's default pipeline.

```
Issue #42 + label "agent-smith" → default pipeline runs
```

See [GitHub Webhook Setup](webhooks/github.md) for configuration.

## GitLab: MR Labels

Add the `security-review` label to a merge request to trigger the `security-scan` pipeline.

```
MR !15 + label "security-review" → security-scan pipeline runs
```

See [GitLab Webhook Setup](webhooks/gitlab.md) for configuration.

## Azure DevOps: Work Item Tags

Add the `security-review` tag to a work item to trigger the `security-scan` pipeline.

```
Work Item #789 + tag "security-review" → security-scan pipeline runs
```

See [Azure DevOps Webhook Setup](webhooks/azure-devops.md) for configuration.

## Roadmap: Unified Configuration (p84)

Phase p84 will bring the Jira-style configurable triggers to all platforms:

- `pipeline_from_label` mapping for GitHub, GitLab, and Azure DevOps
- Status gates (only trigger in specific issue states)
- Done-status transitions after PR creation
- Comment-based re-triggering

This will make all four platforms behave consistently, with platform-specific defaults for backward compatibility.
