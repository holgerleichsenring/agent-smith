# Tracker: GitHub Issues

Use this when your tickets are GitHub Issues. The simplest setup overall — same provider for code and tickets, one token, one webhook secret. The example is the fictional `TodoList` product on `github.com/acme-org`.

## In the studio

Configuration for a server lives in the database and you edit it in the dashboard: switch the left rail to **Configuration** and work down the catalogs. The order matters, because each entry references the one before it: **Secrets** (names only), then **Agents**, then **Repositories**, then a **Tracker**, then a **Project** that wires them together. References are picked from dropdowns, so a project can't point at something that doesn't exist, and the drawer keeps **Create** disabled until every reference resolves.

GitHub specifics: pick type `github` on the tracker and the form asks for the repository URL (the repo whose issues are the tickets) and the auth secret. Trigger statuses and open states stay empty for GitHub, since issues are open or closed and the routing happens on labels. The "Default pipeline" field sets what an issue runs when no label in the tracker's label map matched.

The full tour is on [The Config studio](../configure-it/config-studio.md); what follows is the same wiring written as YAML, which is what the CLI reads directly and what `agent-smith config import` takes.

## The same thing as YAML


```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

agents:
  default-openai:
    type: openai
    models:
      scout:   { model: gpt-4.1-mini }
      primary: { model: gpt-4.1 }
      planning:      { model: gpt-4.1 }
      summarization: { model: gpt-4.1-mini }

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
  acme-issues:
    type: github
    url: https://github.com/acme-org/todolist-api    # the repo whose issues are the tickets
    auth: github_token
    open_states: [open]
    done_status: closed
    default_pipeline: code

projects:
  github-todolist:
    agent: default-openai
    tracker: acme-issues
    repos: [todolist-api, todolist-worker, todolist-web]
    github_trigger:
      project_resolution:
        strategy: tag                  # an issue labelled todolist routes here
        value: todolist
      trigger_statuses: [open]
      done_status: closed
      pipeline_from_label:
        agent-smith:bug:                code
        agent-smith:feature:            code
        agent-smith:security-scan:      security-scan

secrets:
  openai_api_key: ${OPENAI_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

GitHub-specific things to notice:

- **`url` on the tracker is a repository** — GitHub has no Azure-DevOps-style project concept. Issues belong to a repo, so the tracker names the one repo whose issues Agent Smith reads and writes.
- **`open_states: [open]` + `done_status: closed`** — GitHub Issues are binary, open or closed. There's no in-between state.
- **`project_resolution.strategy: tag`** — a label on the issue picks the project. The agent still touches all repos in `projects.X.repos` during the run; the resolution is about *which project to wake up*, not which repo to change. The `repo` strategy (route by the repo the issue was filed in) only works for a project with exactly one repo; see [Project resolution](../reference/configuration/project-resolution.md).
- **`work_item_kinds` does nothing here** — GitHub issues have no type to pick, so the tracker ignores it.

The tracker owns the workflow: `open_states`, `done_status`, `failed_status`, `trigger_statuses` (falls back to `open_states`) and `pipeline_from_label` can all sit on the tracker block, inherited by every project routed to it. A project then only declares its resolution:

```yaml
trackers:
  acme-issues:
    type: github
    url: https://github.com/acme-org/todolist-api
    auth: github_token
    open_states: [open]
    done_status: closed
    pipeline_from_label:
      agent-smith:bug:     code
      agent-smith:feature: code
projects:
  github-todolist:
    agent: default-openai
    tracker: acme-issues
    repos: [todolist-api, todolist-worker, todolist-web]
    resolution:
      tag: todolist
```

The explicit `github_trigger:` block from the full config works too and overrides the tracker field by field — use it when one project needs its own `comment_keyword` or a different label map.

Skills need no configuration: they ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

## Authentication

Use a fine-grained Personal Access Token with these repository permissions: Contents (Read & Write), Pull requests (Read & Write), Issues (Read & Write).

```bash
export GITHUB_TOKEN=ghp_...
```

The GitHub tracker reads `GITHUB_TOKEN` from the server's environment directly; the `github_token` secret name in the config is the reference the studio tracks.

## How tickets reach Agent Smith

- **Webhook** (preferred). One webhook per repo, or one org-level webhook. The server listens on port 8081; point the webhook at `POST /webhook/github` (or the generic `POST /webhook` — the platform is auto-detected from the headers). Verification is HMAC via the `X-Hub-Signature-256` header, checked against the `GITHUB_WEBHOOK_SECRET` environment variable on the server process — there is no secret key in the config. See [Webhooks: GitHub](../trigger-it/webhooks.md#github).
- **Polling**. Add `polling: { enabled: true, interval_seconds: 60 }` to the tracker when webhooks can't reach the server.
- **Manual CLI**. `agent-smith code --ticket 54 --project github-todolist`.

## What gets written back to the ticket

The database is the system of record; the issue state and labels are a best-effort projection of it.

When a run finishes:

- Issue gets closed (state → `closed`).
- A new comment with the PR URLs and the run id.
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.
- PRs whose verification came back red are opened as **drafts**.

When a run fails, the issue moves to `failed_status` if configured (with GitHub's binary open/closed model that usually means it stays `open`), the `agent-smith:failed` label gets added, and a comment carries the error. The run lifecycle (pending / enqueued / in-progress / done / failed, plus waiting and shortfall) is carried as `agent-smith:*` labels, the same as on every tracker. A `label_names:` map on the tracker renames them if your repo already uses its own words.

When the PR opens, Agent Smith uses the `Closes #54` linkage so GitHub auto-closes the issue if the PR merges. That's belt-and-suspenders — the framework also closes the issue explicitly via the API in the `WriteRunResult` step.

When an issue is too thin to act on (title-only, or the run needs a decision), Agent Smith doesn't guess: it posts its open questions as an issue comment and parks the issue in `needs_clarification_status` (settable on the tracker or the project). Answering resumes the run — see [Spec dialogue](../how-it-works/spec-dialogue.md).

## Cross-repo issues

GitHub Issues belong to one repo, but a TodoList ticket may need changes in all three repos (`todolist-api`, `todolist-worker`, `todolist-web`). The convention: file the issue in the repo the tracker points at, and label it for the project. The Agent Smith run touches every repo it needs to, opens one PR per repo, and cross-links them in each PR's body.

## Next

- [Repos: multi-repo](repos-multi.md).
- [Webhooks: GitHub](../trigger-it/webhooks.md#github).
- [AI providers](ai-providers.md).
- [Host it](../host-it/docker-compose.md).
