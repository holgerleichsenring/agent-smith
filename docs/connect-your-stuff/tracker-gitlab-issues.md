# Tracker: GitLab Issues

Use this when your tickets live in GitLab Issues. The example is the fictional `TodoList` product on `gitlab.com/acme-org`.

## The whole config

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

agents:
  default-claude:
    type: claude
    models:
      scout:   { model: claude-haiku-4-5-20251001 }
      primary: { model: claude-sonnet-4-6 }
      planning:      { model: claude-sonnet-4-6 }
      summarization: { model: claude-haiku-4-5-20251001 }

repos:
  todolist-api:
    type: gitlab
    url: https://gitlab.com/acme-org/todolist-api
    auth: gitlab_token
  todolist-worker:
    type: gitlab
    url: https://gitlab.com/acme-org/todolist-worker
    auth: gitlab_token
  todolist-web:
    type: gitlab
    url: https://gitlab.com/acme-org/todolist-web
    auth: gitlab_token

trackers:
  acme-gitlab:
    type: gitlab
    url: https://gitlab.com
    group: acme-org                    # GitLab top-level group
    auth: gitlab_token
    open_states: [opened]
    done_status: closed
    polling:
      enabled: false                   # use webhooks

projects:
  gitlab-todolist:
    agent: default-claude
    tracker: acme-gitlab
    repos: [todolist-api, todolist-worker, todolist-web]
    gitlab_trigger:
      project_resolution:
        strategy: tag                  # GitLab labels are scoped per-group
        value: TodoList
      trigger_statuses: [opened]
      done_status: closed
      pipeline_from_label:
        agent-smith:init:               init-project
        agent-smith:bug:                fix-bug
        agent-smith:feature:            add-feature
        agent-smith:security-scan:      security-scan

skills:
  source: default
  version: v3.0.1

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
  gitlab_token:   ${GITLAB_TOKEN}
```

GitLab-specific things to notice:

- **`group`** — GitLab's container model is groups (top-level) and subgroups (nested). For a single team setup, the top-level group is enough; for an org with many teams, the tracker can also be scoped to a subgroup (`group: acme-org/team-platform`).
- **`open_states: [opened]`** — GitLab uses `opened` (not `open`). The MR terminology likewise — pull requests are merge requests, and Agent Smith opens MRs when the tracker type is `gitlab`.
- **GitLab self-hosted** — change `url` to your self-hosted GitLab URL (`https://gitlab.acme.com`). Everything else stays the same.

## Authentication

Generate a Personal Access Token at `User Settings → Access Tokens` with scopes:

- `api` — full API access (covers reading issues, creating MRs, transitioning issues).
- `read_repository` and `write_repository` — for the git clone + push.

```bash
export GITLAB_TOKEN=glpat-...
```

For org-scoped automation, prefer a Group Access Token instead of a personal one — it doesn't disappear when the user leaves.

## How tickets reach Agent Smith

- **Webhook** (preferred). GitLab posts on issue events. Set up in [Webhooks: GitLab](../trigger-it/webhooks.md#gitlab).
- **Polling**. Set `polling.enabled: true` for self-hosted GitLab on a network where webhooks can't reach the orchestrator.
- **Manual CLI**. `agent-smith fix "acme-org/todolist-api#54 in gitlab-todolist"`.

## What gets written back to the ticket

When a run finishes:

- Issue state → `closed`.
- A new comment with the MR URLs and the run id.
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.

## Next

- [Repos: multi-repo](repos-multi.md).
- [Webhooks: GitLab](../trigger-it/webhooks.md#gitlab).
- [AI providers](ai-providers.md).
- [Host it](../host-it/docker-compose.md).
