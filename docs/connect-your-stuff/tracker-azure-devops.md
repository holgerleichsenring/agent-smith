# Tracker: Azure DevOps Boards

Use this when your tickets live in Azure DevOps work items and your repos are in Azure DevOps Git. The example here is the fictional `TodoList` product in the `Platform` project on `acme-org`.

## The whole config

Drop this into `agentsmith.yml`. Substitute the URLs, the project / repo names, and your AI provider block. The rest is mechanical.

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json
#
# Catalog-first schema (p0139). Project-resolution-by-tag (p0140a).

deployment:
  registry: holgerleichsenring
  version: 0.108.0

agents:
  azure-openai-default:
    type: azure_openai
    endpoint: https://oai-acme-dev.openai.azure.com
    api_version: 2025-01-01-preview
    cache:
      is_enabled: true
      strategy: automatic
    retry:
      max_retries: 5
      initial_delay_ms: 4000
      backoff_multiplier: 2.0
      max_delay_ms: 60000
    models:
      scout:   { model: gpt-4.1-mini, deployment: gpt-4o-mini-deployment, max_tokens: 4096 }
      primary: { model: gpt-4.1,      deployment: gpt4-1-deployment,     max_tokens: 8192 }
      planning:      { model: gpt-4.1,      deployment: gpt4-1-deployment,     max_tokens: 4096 }
      summarization: { model: gpt-4.1-mini, deployment: gpt-4o-mini-deployment, max_tokens: 2048 }

repos:
  todolist-api:
    type: azure_devops
    url: https://dev.azure.com/acme-org/Platform/_git/TodoList.Api
    auth: azure_devops_token
  todolist-worker:
    type: azure_devops
    url: https://dev.azure.com/acme-org/Platform/_git/TodoList.Worker
    auth: azure_devops_token
  todolist-web:
    type: azure_devops
    url: https://dev.azure.com/acme-org/Platform/_git/TodoList.Web
    auth: azure_devops_token
  todolist-docs:
    type: azure_devops
    url: https://dev.azure.com/acme-org/Platform/_git/TodoList.Docs
    auth: azure_devops_token

trackers:
  acme-platform:
    type: azure_devops
    url: https://dev.azure.com/acme-org
    organization: acme-org
    project: Platform
    auth: azure_devops_token
    open_states:  [New, Active]
    done_status:  Resolved
    polling:
      enabled: true
      interval_seconds: 60
      jitter_percent: 10

projects:
  azuredevops-todolist:
    agent: azure-openai-default
    tracker: acme-platform
    repos:
      - todolist-api
      - todolist-worker
      - todolist-web
      - todolist-docs
    azuredevops_trigger:
      project_resolution:
        strategy: tag
        value: TodoList
      trigger_statuses: [New, Active]
      done_status: Resolved
      pipeline_from_label:
        agent-smith:init:               init-project
        agent-smith:bug:                fix-bug
        agent-smith:feature:            add-feature
        agent-smith:security-scan:      security-scan
        agent-smith:api-security-scan:  api-security-scan

secrets:
  azure_openai_api_key: ${AZURE_OPENAI_API_KEY}
  azure_devops_token:   ${AZURE_DEVOPS_TOKEN}
```

That's the entire wiring. Set the two env vars and Agent Smith can claim a ticket and open a PR end to end.

## What each block does

**`deployment`** — one registry + version pin. It feeds both the orchestrator container and the sandbox-agent image; there is nothing else to pin.

**`agents.azure-openai-default`** — the AI provider Agent Smith calls. Catalog key (`azure-openai-default`) is referenced from `projects.X.agent`. Type can be anything from the [providers page](ai-providers.md); the example uses `azure_openai` because Azure DevOps shops usually already have an Azure subscription. The `models` block picks a model per role: `scout` (cheap, used to map the codebase), `primary` (the good one, used for the actual code), `planning`, `summarization`.

**`repos.todolist-*`** — every Azure DevOps Git repo gets one entry. The `url` is the clone URL; `auth: azure_devops_token` says "use the secret named `azure_devops_token`". You can have repos in here that aren't part of every project — projects pick which ones they want.

**`trackers.acme-platform`** — one tracker per (organization × project) pair. `open_states` is the list of work-item states Agent Smith treats as eligible (anything not in here is ignored). `done_status` is what Agent Smith moves the ticket to when a run finishes. The tracker owns the workflow: it can also carry `failed_status` (where a failed run parks the ticket; without it the status stays put), `trigger_statuses` (falls back to `open_states` when unset), and `pipeline_from_label` — every project routed to the tracker inherits them. `polling` is per-tracker and is the no-webhook fallback — see [Polling](../trigger-it/polling.md). For real production use, set up [webhooks](../trigger-it/webhooks.md) and leave `polling.enabled: false`.

**`projects.azuredevops-todolist`** — the wiring. Picks one agent, one tracker, a list of repos. The `azuredevops_trigger` block is Azure-DevOps-specific.

**`project_resolution.strategy: tag`** with `value: TodoList` — when a work item gets tagged `TodoList`, Agent Smith routes it to this project. Other strategies are `area-path` (route by work-item area path) and `repo` (route by referenced repo). Documented in [Project resolution](../reference/configuration/project-resolution.md).

**`pipeline_from_label`** — which framework label triggers which pipeline. Labels are matched in declaration order; first match wins. The framework reserves the `agent-smith:*` prefix for lifecycle labels and won't match against those when picking a pipeline. Full table on the [Labels page](../trigger-it/labels.md).

**Skills** — no block needed. Skills ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

## Terse form: let the tracker own the workflow

Because the tracker block carries the workflow, a project routed to it only has to declare how tickets are matched to it — its *resolution*:

```yaml
trackers:
  acme-platform:
    type: azure_devops
    url: https://dev.azure.com/acme-org
    organization: acme-org
    project: Platform
    auth: azure_devops_token
    open_states: [New, Active]
    done_status: Resolved
    failed_status: New
    pipeline_from_label:
      agent-smith:bug:     fix-bug
      agent-smith:feature: add-feature

projects:
  azuredevops-todolist:
    agent: azure-openai-default
    tracker: acme-platform
    repos: [todolist-api, todolist-worker, todolist-web, todolist-docs]
    resolution:
      tag: TodoList                    # or: area_path: AcmeMain/Platform / repo: <clone url>
```

The explicit `azuredevops_trigger:` block from the full config above still works and overrides the tracker field-by-field — reach for it when one project needs its own `comment_keyword` or a different label map.

## Authentication

Generate a Personal Access Token in Azure DevOps with these scopes:

- **Code** — Read & Write (clone, push, open PRs).
- **Work Items** — Read & Write (read tickets, update status, add comments, add/remove labels).

Set it in the environment:

```bash
export AZURE_DEVOPS_TOKEN=...
```

The token rotates whenever you rotate it in Azure DevOps. Agent Smith reads it once at startup; restart the orchestrator after a rotation.

## How tickets reach Agent Smith

Three ways, pick one:

- **Webhook** (preferred). Azure DevOps posts to Agent Smith on work-item updates. The server listens on port 8081; point the service hook at `POST /webhook` (the platform is auto-detected from the payload). Verification is a Basic-auth header checked against the `AZDO_WEBHOOK_SECRET` environment variable on the server process — there is no secret key in the config. Set up in [Webhooks: Azure DevOps](../trigger-it/webhooks.md#azure-devops). `polling.enabled: false` in the config above.
- **Polling**. Agent Smith asks the tracker every `interval_seconds` what's new. Use this when you can't set up a webhook (NAT, on-prem tracker, fast iteration). `polling.enabled: true` in the config above.
- **Manual CLI**. `agent-smith fix --ticket 54 --project azuredevops-todolist` — explicit, useful for testing the config. See [Trigger from CLI](../trigger-it/cli.md).

## What gets written back to the ticket

The database is the system of record; the work-item status and labels are a best-effort projection of it.

When a run finishes:

- Status transitions to `done_status` (in the example, `Resolved`).
- A new comment with the PR URLs and the run id (e.g. `2026-05-22T14-03-11-9f2a`).
- The `agent-smith:done` label gets added; `agent-smith:in-progress` removed.
- PRs whose verification came back red are opened as **drafts**, so nothing unreviewed looks mergeable.

When a run fails:

- Status moves to `failed_status` if configured; otherwise it stays where it is.
- The `agent-smith:failed` label gets added.
- A new comment with the failed-step name and the error message.

By default the run lifecycle is carried as `agent-smith:*` labels. The tracker can opt into native state transitions instead via a `lifecycle_status_names:` map (pending / enqueued / in-progress / done / failed → your work-item state names); labels remain the always-available carrier.

When a ticket is too thin to act on (title-only, or the planner needs a decision), Agent Smith doesn't guess: it posts its open questions as a work-item comment and parks the ticket in `needs_clarification_status` (settable on the tracker or the project). Answering the questions resumes the run — see [Spec dialogue](../how-it-works/spec-dialogue.md).

## Next

- [Repos: multi-repo](repos-multi.md) — wire all four TodoList repos as one project (the config above is already multi-repo; the page explains the model).
- [Webhooks: Azure DevOps](../trigger-it/webhooks.md#azure-devops) — the URL shape, the payload, secret verification.
- [AI providers](ai-providers.md) — if you want Claude or local Ollama instead of Azure OpenAI.
- [Host it](../host-it/docker-compose.md) — moving from a CLI smoke test to a real deployment.
