# Tracker: GitLab Issues

Use this when your tickets live in GitLab Issues. The example is the fictional `TodoList` product on `gitlab.com/acme-org`.

## In the studio

Configuration for a server lives in the database and you edit it in the dashboard: switch the left rail to **Configuration** and work down the catalogs. The order matters, because each entry references the one before it: **Secrets** (names only), then **Agents**, then **Repositories**, then a **Tracker**, then a **Project** that wires them together. References are picked from dropdowns, so a project can't point at something that doesn't exist, and the drawer keeps **Create** disabled until every reference resolves.

GitLab specifics: pick type `gitlab` on the tracker and the form asks for the project path (the GitLab project whose issues are the tickets), an optional base URL and the auth secret, plus the label and state vocabulary GitLab uses. The "Default pipeline" field sets what an issue runs when no label in the tracker's label map matched.

The full tour is on [The Config studio](../configure-it/config-studio.md); what follows is the same wiring written as YAML, which is what the CLI reads directly and what `agent-smith config import` takes.

## The same thing as YAML


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
    project: acme-org/todolist-api     # the GitLab project whose issues are the tickets
    auth: gitlab_token
    open_states: [opened]
    done_status: closed
    default_pipeline: code
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
        agent-smith:bug:                code
        agent-smith:feature:            code
        agent-smith:security-scan:      security-scan

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
  gitlab_token:   ${GITLAB_TOKEN}
```

GitLab-specific things to notice:

- **`project`** — the tracker is one GitLab project's issue list, named by its full path. A subgroup is just part of the path (`acme-org/team-platform/todolist-api`).
- **Groups belong to connections** — to discover repos under a group or subgroup, add a `connections:` entry with `group: acme-org` (or `acme-org/team-platform`). A repo discovered there is named by its path relative to that group, so `team-platform/todolist-api` and `team-billing/todolist-api` stay two repos, and a wildcard like `acme/team-platform/*` matches against that path. See [Repos: multi-repo](repos-multi.md).
- **`open_states: [opened]`** — GitLab uses `opened` (not `open`). The MR terminology likewise — pull requests are merge requests, and Agent Smith opens MRs when the tracker type is `gitlab`.
- **`work_item_kinds` does nothing here** — GitLab issues have no type this setting could pick, so the tracker ignores it.
- **GitLab self-managed** — for repositories the API base URL is derived from each repo URL's own scheme and authority, so they need no extra config. The issue tracker is different: it talks to `https://gitlab.com` unless the `GITLAB_URL` environment variable says otherwise, so a self-managed instance sets `GITLAB_URL=https://gitlab.acme.com` on the server process. `GITLAB_URL` is also how a sub-path install (`https://tools.acme.com/gitlab`) is reached.

The tracker owns the workflow: `open_states`, `done_status`, `failed_status`, `trigger_statuses` (falls back to `open_states`) and `pipeline_from_label` can all sit on the tracker block, inherited by every project routed to it. A project then only declares its resolution:

```yaml
trackers:
  acme-gitlab:
    type: gitlab
    project: acme-org/todolist-api
    auth: gitlab_token
    open_states: [opened]
    done_status: closed
    pipeline_from_label:
      agent-smith:bug:     code
      agent-smith:feature: code
projects:
  gitlab-todolist:
    agent: default-claude
    tracker: acme-gitlab
    repos: [todolist-api, todolist-worker, todolist-web]
    resolution:
      tag: TodoList
```

The explicit `gitlab_trigger:` block from the full config works too and overrides the tracker field by field.

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
- **Manual CLI**. `agent-smith code --ticket 54 --project gitlab-todolist`.

## What gets written back to the ticket

The database is the system of record; the issue state and labels are a best-effort projection of it.

When a run finishes:

- Issue state → `closed`.
- A new comment with the MR URLs and the run id.
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.
- MRs whose verification came back red are opened as **drafts**.

When a run fails, the issue moves to `failed_status` if configured (otherwise it stays `opened`), the `agent-smith:failed` label gets added, and a comment carries the error. The run lifecycle (pending / enqueued / in-progress / done / failed, plus waiting and shortfall) is carried as `agent-smith:*` labels, the same as on every tracker. A `label_names:` map on the tracker renames them if your project already uses its own words.

When an issue is too thin to act on (title-only, or the run needs a decision), Agent Smith doesn't guess: it posts its open questions as an issue comment and parks the issue in `needs_clarification_status` (settable on the tracker or the project). Answering resumes the run — see [Spec dialogue](../how-it-works/spec-dialogue.md).

## Next

- [Repos: multi-repo](repos-multi.md).
- [Webhooks: GitLab](../trigger-it/webhooks.md#gitlab).
- [AI providers](ai-providers.md).
- [Host it](../host-it/docker-compose.md).
