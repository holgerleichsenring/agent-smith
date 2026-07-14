# Tracker: Jira

Use this when your tickets live in Jira issues. The example here is the fictional `TodoList` product on `acme.atlassian.net`, in the Jira project `TL`.

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
    type: github
    url: https://github.com/acme-org/todolist-api
    auth: github_token
  todolist-worker:
    type: github
    url: https://github.com/acme-org/todolist-worker
    auth: github_token
  todolist-web:
    type: github
    url: https://github.com/acme-org/todolist-web
    auth: github_token

trackers:
  acme-jira:
    type: jira
    url: https://acme.atlassian.net
    project: TL
    auth: jira_token
    auth_email: agent-smith@acme.org    # Jira API needs email + token
    open_states: [Open, In Progress, To Do]
    done_status: Done
    close_transition_name: Done        # the transition Jira calls to reach done_status
    label_mode: true                   # Jira tags ≡ labels; see lifecycle labels below
    polling:
      enabled: false                   # use the webhook path; see Trigger it

projects:
  jira-todolist:
    agent: default-claude
    tracker: acme-jira
    repos: [todolist-api, todolist-worker, todolist-web]
    jira_trigger:
      secret: ${JIRA_WEBHOOK_SECRET}   # webhook shared secret — Jira is the one tracker where this lives in config
      project_resolution:
        strategy: tag
        value: todolist                # Jira labels are lowercase
      trigger_statuses: [Open, In Progress, To Do]
      done_status: Done
      pipeline_from_label:
        agent-smith-init:               init-project
        agent-smith-bug:                fix-bug
        agent-smith-feature:            add-feature
        agent-smith-security-scan:      security-scan

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
  github_token:   ${GITHUB_TOKEN}
  jira_token:     ${JIRA_API_TOKEN}
```

Jira-specific things to notice:

- **`auth_email`** — Jira's REST API authenticates with an email plus an API token, not a token alone. The email is the account the agent acts as (you'll see it in the issue history).
- **`close_transition_name`** — Jira doesn't expose a "set status" API. You move an issue between statuses by *transitioning* it, and transitions have names defined per project workflow. Set this to the transition that lands on your `done_status`. If you don't know it, look at the workflow diagram in Jira Settings → Issue Types → Workflows.
- **`label_mode: true`** — colons aren't allowed in Jira labels, so the framework lifecycle labels use dashes (`agent-smith-bug` instead of `agent-smith:bug`).
- **`jira_trigger.secret`** — the webhook shared secret. Jira is the exception among the trackers: GitHub / GitLab / Azure DevOps verify webhooks from server environment variables, but for Jira the secret sits in config, per project, under `jira_trigger`.
- **`lifecycle_status_names`** — by default the run lifecycle (pending / enqueued / in-progress / done / failed) is carried as labels. Add a `lifecycle_status_names:` map on the tracker to project it onto native Jira workflow statuses instead; labels remain the always-available carrier.
- **`endpoints:`** — an override block on the tracker for individual REST paths, for the day Atlassian moves one. You should never need it until you do.
- **`polling.enabled: false`** — Atlassian Cloud webhooks are reliable; use them. Polling is per-tracker and is the fallback for Jira Server / Data Center behind a firewall.

The tracker owns the workflow: `open_states`, `done_status`, `failed_status` (where a failed run parks the issue), `trigger_statuses` (falls back to `open_states`) and `pipeline_from_label` can all live on the tracker block, inherited by every project routed to it. A project then only declares its resolution:

```yaml
projects:
  jira-todolist:
    agent: default-claude
    tracker: acme-jira
    repos: [todolist-api, todolist-worker, todolist-web]
    resolution:
      tag: todolist
    jira_trigger:
      secret: ${JIRA_WEBHOOK_SECRET}
```

The explicit `jira_trigger:` block from the full config still works and overrides the tracker field-by-field.

## Authentication

Create an API token at `id.atlassian.com/manage-profile/security/api-tokens`. Set in the environment:

```bash
export JIRA_API_TOKEN=...
```

The token is scoped to the account that owns it. Make sure that account has permission to comment, transition, and label-edit issues in the project.

## How tickets reach Agent Smith

- **Webhook** (preferred). Jira Cloud posts to Agent Smith on issue updates. The server listens on port 8081; point the webhook at `POST /webhook/jira` (or the generic `POST /webhook` — the platform is auto-detected). The shared secret is checked against `jira_trigger.secret`. Set up in [Webhooks: Jira](../trigger-it/webhooks.md#jira).
- **Polling**. For Jira Server / Data Center behind a firewall. Set `polling.enabled: true` and `interval_seconds: 60` (or more — Jira's API rate limits get strict).
- **Manual CLI**. `agent-smith fix --ticket TL-54 --project jira-todolist` — note the project-prefixed issue key, that's Jira's native shape.

## What gets written back to the ticket

The database is the system of record; the issue status and labels are a best-effort projection of it.

When a run finishes:

- Issue transitions via `close_transition_name` to `done_status`.
- A new comment with the PR URLs and the run id.
- The `agent-smith-done` label gets added; `agent-smith-in-progress` removed.
- PRs whose verification came back red are opened as **drafts**.

When a run fails, the issue moves to `failed_status` if configured (otherwise the status stays), the `agent-smith-failed` label gets added, and a comment carries the error.

The label-mode flag determines the lifecycle label format (`agent-smith-done` vs `agent-smith:done`). Jira shops always set it to `true`.

When an issue is too thin to act on (title-only, or the planner needs a decision), Agent Smith doesn't guess: it posts its open questions as an issue comment and parks the issue in `needs_clarification_status` (settable on the tracker or the project). Answering resumes the run — see [Spec dialogue](../how-it-works/spec-dialogue.md).

## Next

- [Repos: multi-repo](repos-multi.md) — TodoList wired across three GitHub repos with one Jira project as the tracker.
- [Webhooks: Jira](../trigger-it/webhooks.md#jira) — exact URL and verification.
- [AI providers](ai-providers.md) — if you don't want Claude.
- [Host it](../host-it/docker-compose.md).
