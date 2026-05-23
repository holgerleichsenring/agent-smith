# Tracker: GitHub Issues

Use this when your tickets are GitHub Issues. The simplest setup overall — same provider for code and tickets, one token, one webhook secret. The example is the fictional `TodoList` product on `github.com/acme-org`.

## The whole config

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
    organization: acme-org
    auth: github_token
    open_states: [open]
    done_status: closed

projects:
  github-todolist:
    agent: default-openai
    tracker: acme-issues
    repos: [todolist-api, todolist-worker, todolist-web]
    github_trigger:
      project_resolution:
        strategy: repo                 # which repo the issue was filed in
        value: todolist-api            # the "primary" repo for cross-repo issues
      trigger_statuses: [open]
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
  openai_api_key: ${OPENAI_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

GitHub-specific things to notice:

- **No `project:` field on the tracker** — GitHub doesn't have an Azure-DevOps-style project concept. Issues belong to a repo, not a project. The `organization` field scopes which org Agent Smith reads issues from.
- **`open_states: [open]` + `done_status: closed`** — GitHub Issues are binary, open or closed. There's no in-between state.
- **`project_resolution.strategy: repo`** — when a GitHub Issue gets the right label, Agent Smith routes it. The `value` field names the "primary" repo for the project (which is where issues get filed for cross-repo changes). The agent still touches all repos in `projects.X.repos` during the run; the resolution is about *which project to wake up*, not which repo to change.
- **Polling not listed** — GitHub webhooks are reliable enough that polling isn't usually needed. If you do need it (private GitHub Enterprise on a restricted network), add `polling: { enabled: true, interval_seconds: 60 }` to the tracker block.

## Authentication

Use a fine-grained Personal Access Token or a GitHub App. Token scopes:

- **Repository permissions** — Contents (Read & Write), Pull requests (Read & Write), Issues (Read & Write).
- **Webhook permissions** (if you use a GitHub App) — Repository hooks (Read & Write).

```bash
export GITHUB_TOKEN=ghp_...           # PAT, prefixed ghp_
# or, for a GitHub App:
export GITHUB_APP_PRIVATE_KEY=$(cat private-key.pem)
export GITHUB_APP_ID=...
```

The config above uses the PAT path. For GitHub Apps, set `auth: github_app` in the tracker block and the framework uses the app credentials.

## How tickets reach Agent Smith

- **Webhook** (preferred). One webhook per repo, or one org-level webhook. See [Webhooks: GitHub](../trigger-it/webhooks.md#github).
- **Manual CLI**. `agent-smith fix "acme-org/todolist-api#54 in github-todolist"` — issue references in GitHub's native form.

## What gets written back to the ticket

When a run finishes:

- Issue gets closed (state → `closed`).
- A new comment with the PR URLs and the run id.
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.

When the PR opens, Agent Smith uses the `Closes #54` linkage so GitHub auto-closes the issue if the PR merges. That's belt-and-suspenders — the framework also closes the issue explicitly via the API in the `WriteRunResult` step.

## Cross-repo issues

GitHub Issues belong to one repo, but a TodoList ticket may need changes in all three repos (`todolist-api`, `todolist-worker`, `todolist-web`). The convention: file the issue in the "primary" repo (the one named under `project_resolution.value`). The Agent Smith run touches every repo it needs to, opens one PR per repo, and cross-links them in each PR's body.

## Next

- [Repos: multi-repo](repos-multi.md).
- [Webhooks: GitHub](../trigger-it/webhooks.md#github).
- [AI providers](ai-providers.md).
- [Host it](../host-it/docker-compose.md).
