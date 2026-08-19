# Tracker: Azure DevOps Boards

Use this when your tickets live in Azure DevOps work items and your repos are in Azure DevOps Git. The example here is the fictional `TodoList` product in the `Platform` project on `acme-org`.

## What you're wiring

Five things, in this order, because each one references the one before it:

1. **Secrets** — the env-var names for your Azure DevOps PAT and your AI provider key.
2. **An agent** — which LLM, which model per role.
3. **Repositories** — the Azure DevOps Git repos the pipelines clone and push to.
4. **A tracker** — the (organization, project) pair whose work items you read and write back.
5. **A project** — the wiring: this agent, this tracker, these repos, and how a work item routes here.

## In the studio

Open the dashboard, switch the rail to **Configuration**, and work down the catalogs.

**Secrets.** New secret, name it `azure_devops_token`. You're registering the *name* — the value stays in the environment of the server process (or your k8s Secret), and the studio never sees it. Do the same for your provider key.

**Agents.** New agent, id `azure-openai-default`, provider `azure_openai`. Fill the endpoint and api version, pick the key secret from the dropdown, then set a model per role — a cheap one for `scout`, the good one for `primary` and `coding`. If you want dollar figures on your runs rather than just token counts, add the pricing section while you're in there.

**Repositories.** One entry per repo: the clone URL, and `azure_devops_token` as the auth. If you'd rather not list them one by one, add a **Connection** instead — organization plus project plus auth — and a project can then pull repos from that scope by name or wildcard.

**Trackers.** New tracker, type `azure_devops`. The form switches to the Azure fields once you pick the type: organization, project, URL, auth secret. Then the workflow, which the tracker owns for every project routed to it:

- **Open states** — the work-item states Agent Smith treats as eligible. Anything else is ignored.
- **Done status** — where a finished run moves the ticket.
- **Failed status** — where a failed run parks it. Leave it empty and the status stays put.
- **Needs-clarification status** — where a ticket goes when the agent has questions it won't guess at.

![Editing a tracker in the studio](../assets/screenshots/config-tracker-drawer.png)

**Projects.** New project. Pick the agent and the tracker from the dropdowns, tick the repos, and set the resolution strategy — for Azure DevOps that's `tag`, `area-path`, or `repo`. Tag is the common one: tag a work item `TodoList` and it routes to this project. The wiring preview at the bottom of the drawer draws what you've built, and **Create** stays disabled until every reference resolves.

![The New Project drawer](../assets/screenshots/config-new-project.png)

That's the whole wiring. Two environment variables and Agent Smith can claim a work item and open a pull request end to end.

## The same thing as YAML

The CLI reads this shape directly, and a server takes it through `agent-smith config import`. It's also what **Export agentsmith.yml** gives you back.

<details>
<summary>The full config for the example above</summary>

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

</details>

Because the tracker carries the workflow, a project routed to it only has to declare how tickets match it:

```yaml
projects:
  azuredevops-todolist:
    agent: azure-openai-default
    tracker: acme-platform
    repos: [todolist-api, todolist-worker, todolist-web, todolist-docs]
    resolution:
      tag: TodoList                    # or: area_path: AcmeMain/Platform / repo: <clone url>
```

The explicit `azuredevops_trigger:` block still works and overrides the tracker field by field — reach for it when one project needs its own `comment_keyword` or a different label map.

## Authentication

Generate a Personal Access Token in Azure DevOps with these scopes:

- **Code** — Read & Write (clone, push, open PRs).
- **Work Items** — Read & Write (read tickets, update status, add comments, add/remove labels).

Set it in the environment:

```bash
export AZURE_DEVOPS_TOKEN=...
```

The token rotates whenever you rotate it in Azure DevOps. Agent Smith reads it once at startup, so restart the orchestrator after a rotation — the studio holds the *name* of the secret, not its value, so there's nothing to change there.

## How tickets reach Agent Smith

Three ways, pick one:

- **Webhook** (preferred). Azure DevOps posts to Agent Smith on work-item updates. The server listens on port 8081; point the service hook at `POST /webhook` (the platform is auto-detected from the payload). Verification is a Basic-auth header checked against the `AZDO_WEBHOOK_SECRET` environment variable on the server process — there is no secret key in the config. Set up in [Webhooks: Azure DevOps](../trigger-it/webhooks.md#azure-devops). Leave polling off on the tracker.
- **Polling**. Agent Smith asks the tracker every `interval_seconds` what's new. Use this when you can't set up a webhook (NAT, on-prem tracker, fast iteration). Turn it on in the tracker's polling section and set the interval there; the running server picks the change up without a restart.
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
