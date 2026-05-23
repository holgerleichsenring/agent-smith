# Repos: multi-repo

One product, multiple repos. One ticket, one run, multiple pull requests. This is what made me build Agent Smith the way it is — most real products I've worked on span two or three repos and a "fix the auth flow" ticket touches all of them.

## What multi-repo means here

A multi-repo project is one entry under `projects:` with more than one repo in its `repos:` list. When a ticket targeted at that project comes in, Agent Smith:

- Spawns **one sandbox per repo**, each with its own toolchain image (a .NET repo gets `dotnet/sdk:8.0`, a Node repo gets `node:20`, a Python repo gets `python:3.12`).
- Clones each repo into its sandbox at `/work`.
- Cuts a branch named `agentsmith/ticket-{N}` in every repo so reviewers see the same branch name across all sibling PRs.
- Runs **one plan and one agent conversation** across the whole set. The file-tool calls dispatch by path prefix — `todolist-api/src/Auth.cs` ends up in the api sandbox, `todolist-web/src/auth/login.ts` ends up in the web sandbox.
- Opens **one pull request per repo**, all cross-linked.
- Sets the ticket back to resolved with every PR URL in the comment.

## The whole config — `TodoList` with four repos

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

sandbox:
  agent_registry: holgerleichsenring
  agent_version: 0.60.1

orchestrator:
  registry: holgerleichsenring
  version: 0.60.1

agents:
  azure-openai-default:
    type: azure_openai
    endpoint: https://oai-acme-dev.openai.azure.com
    api_version: 2025-01-01-preview
    cache: { is_enabled: true, strategy: automatic }
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
    open_states: [New, Active]
    done_status: Resolved

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
        agent-smith:bug:                fix-bug
        agent-smith:feature:            add-feature

skills:
  source: default
  version: v3.0.1
  cache_dir: /var/lib/agentsmith/skills

secrets:
  azure_openai_api_key: ${AZURE_OPENAI_API_KEY}
  azure_devops_token:   ${AZURE_DEVOPS_TOKEN}
```

## How the parts wire

**`repos:`** is the catalog. Every repo your projects might touch goes here, exactly once. The catalog key (`todolist-api`, `todolist-worker`, …) is the name you use everywhere else. A repo can appear in more than one project — handy if you have shared library repos.

**`projects.azuredevops-todolist.repos:`** is the list of which repos belong to this project. The order matters mildly: the first repo is the "primary" for legacy single-repo code paths and for display in run summaries. Agent Smith treats them all equally during the run; only the display defaults pick the first one.

**Path-prefix routing.** When the agent calls `read_file("todolist-api/src/Auth.cs")`, the framework parses the first path segment, looks up `Sandboxes["todolist-api"]`, and forwards the read to that sandbox. Same logic for write, edit, find_files, grep_in_tree, etc. The run_command tool requires an explicit `repo` argument so there's no path-segment guessing.

**Per-repo bootstrap.** Each repo needs its own `.agentsmith/context.yaml` and `.agentsmith/coding-principles.md` so Agent Smith knows the toolchain and the project rules per repo. The `init-project` pipeline writes these files into each repo's sandbox and opens one bootstrap PR per repo, cross-linked. Run it once per project (which means once for all the repos in the project — `init-project` iterates internally).

## Branch coherence

Every repo in the run uses the same branch name: `agentsmith/ticket-{N}`. Reviewers see the same name across all sibling pull requests, which makes the multi-repo change set legible at a glance. If a repo had no actual changes after the agent run, the PR is skipped for that repo and the branch isn't pushed.

## What you get in the ticket comment

```
Resolved by Agent Smith (run 2026-05-22T14-03-11-9f2a).

Pull requests:
- todolist-api    https://dev.azure.com/.../pullrequest/4471
- todolist-worker https://dev.azure.com/.../pullrequest/4472
- todolist-web    https://dev.azure.com/.../pullrequest/4473
- todolist-docs   (no changes — skipped)

Cost: 1.40 USD. 47 tests passed across the changed repos.
```

## Next

- [Repos: mono-repo](repos-mono.md) — if you were here by mistake.
- [Methodology](../how-it-works/methodology.md) — what plan / review / verify do across repos.
- [Multi-repo, deeper](../how-it-works/multi-repo.md) — the conceptual deep-dive.
- [Trigger it](../trigger-it/webhooks.md) — wiring the tracker so the run starts on a ticket update.
