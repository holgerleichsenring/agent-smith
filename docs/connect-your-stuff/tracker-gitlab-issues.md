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

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
  gitlab_token:   ${GITLAB_TOKEN}
```

GitLab-specific things to notice:

- **`group`** — GitLab's container model is groups (top-level) and subgroups (nested). For a single team setup, the top-level group is enough; for an org with many teams, the tracker can also be scoped to a subgroup (`group: acme-org/team-platform`).
- **`open_states: [opened]`** — GitLab uses `opened` (not `open`). The MR terminology likewise — pull requests are merge requests, and Agent Smith opens MRs when the tracker type is `gitlab`.
- **GitLab self-managed** — change `url` to your self-managed GitLab URL (`https://gitlab.acme.com`). Everything else stays the same: the API base URL is derived from the repo URL's own scheme and authority, so self-managed instances work without extra config. The `GITLAB_URL` environment variable exists only as an optional override for sub-path installs (e.g. `https://tools.acme.com/gitlab`).

The tracker owns the workflow: `open_states`, `done_status`, `failed_status`, `trigger_statuses` (falls back to `open_states`) and `pipeline_from_label` can all sit on the tracker block, inherited by every project routed to it. A project then only declares its resolution:

```yaml
trackers:
  acme-gitlab:
    type: gitlab
    url: https://gitlab.com
    group: acme-org
    auth: gitlab_token
    open_states: [opened]
    done_status: closed
    pipeline_from_label:
      agent-smith:bug:     fix-bug
      agent-smith:feature: add-feature

projects:
  gitlab-todolist:
    agent: default-claude
    tracker: acme-gitlab
    repos: [todolist-api, todolist-worker, todolist-web]
    resolution:
      tag: TodoList
```

The explicit `gitlab_trigger:` block from the full config still works and overrides the tracker field-by-field.

Skills need no configuration: they ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

## Authentication

Generate a Personal Access Token at `User Settings → Access Tokens` with scopes:

- `api` — full API access (covers reading issues, creating MRs, transitioning issues).
- `read_repository` and `write_repository` — for the git clone + push.

```bash
export GITLAB_TOKEN=glpat-...
```

For org-scoped automation, prefer a Group Access Token instead of a personal one — it doesn't disappear when the user leaves.

## How tickets reach Agent Smith

- **Webhook** (preferred). GitLab posts on issue events. The server listens on port 8081; point the webhook at `POST /webhook/gitlab` (or the generic `POST /webhook` — the platform is auto-detected from the headers). Verification compares the `X-Gitlab-Token` header against the `GITLAB_WEBHOOK_TOKEN` environment variable on the server process — there is no secret key in the config. Set up in [Webhooks: GitLab](../trigger-it/webhooks.md#gitlab).
- **Polling**. Set `polling.enabled: true` (per-tracker) for self-managed GitLab on a network where webhooks can't reach the orchestrator.
- **Manual CLI**. `agent-smith fix --ticket 54 --project gitlab-todolist`.

## What gets written back to the ticket

The database is the system of record; the issue state and labels are a best-effort projection of it.

When a run finishes:

- Issue state → `closed`.
- A new comment with the MR URLs and the run id.
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.
- MRs whose verification came back red are opened as **drafts**.

When a run fails, the issue moves to `failed_status` if configured (otherwise it stays `opened`), the `agent-smith:failed` label gets added, and a comment carries the error. The full run lifecycle (pending / enqueued / in-progress / done / failed) is carried as `agent-smith:*` labels — the same parity all four trackers share; a `lifecycle_status_names:` map lets a tracker project those onto native states instead, with labels remaining the always-available carrier.

When an issue is too thin to act on (title-only, or the planner needs a decision), Agent Smith doesn't guess: it posts its open questions as an issue comment and parks the issue in `needs_clarification_status` (settable on the tracker or the project). Answering resumes the run — see [Spec dialogue](../how-it-works/spec-dialogue.md).

## Next

- [Repos: multi-repo](repos-multi.md).
- [Webhooks: GitLab](../trigger-it/webhooks.md#gitlab).
- [AI providers](ai-providers.md).
- [Host it](../host-it/docker-compose.md).
