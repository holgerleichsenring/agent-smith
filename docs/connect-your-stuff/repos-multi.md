# Repos: multi-repo

One product, multiple repos. One ticket, one run, multiple pull requests. This is what made me build Agent Smith the way it is — most real products I've worked on span two or three repos and a "fix the auth flow" ticket touches all of them.

## What multi-repo means here

A multi-repo project is one entry under `projects:` with more than one repo in its `repos:` list. When a ticket targeted at that project comes in, Agent Smith:

- Spawns **one sandbox per repo**, each with its own toolchain image (a .NET repo gets `dotnet/sdk:8.0`, a Node repo gets `node:20`, a Python repo gets `python:3.12`).
- Clones each repo into its sandbox at `/work`.
- Cuts a branch named `agentsmith/ticket-{N}` in every repo so reviewers see the same branch name across all sibling PRs.
- Runs **one plan and one agent conversation** across the whole set. The file-tool calls dispatch by path prefix — `TodoList.Api/src/Auth.cs` ends up in the api sandbox, `TodoList.Web/src/auth/login.ts` ends up in the web sandbox.
- Opens **one pull request per repo**, all cross-linked.
- Sets the ticket back to resolved with every PR URL in the comment.

## The whole config — `TodoList` with four repos

```yaml
# yaml-language-server: $schema=https://raw.githubusercontent.com/holgerleichsenring/agent-smith/main/config/agentsmith.schema.json

deployment:
  registry: holgerleichsenring
  version: 0.108.0

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

connections:
  acme:
    type: azure_devops
    url: https://dev.azure.com/acme-org
    organization: acme-org             # azure_devops connections need organization + project
    project: Platform
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
      - acme/TodoList.*                # discovered under the connection via the provider API
      - "!acme/TodoList.Legacy"        # exclusion glob
    azuredevops_trigger:
      project_resolution:
        strategy: tag
        value: TodoList
      trigger_statuses: [New, Active]
      done_status: Resolved
      pipeline_from_label:
        agent-smith:bug:                fix-bug
        agent-smith:feature:            add-feature

secrets:
  azure_openai_api_key: ${AZURE_OPENAI_API_KEY}
  azure_devops_token:   ${AZURE_DEVOPS_TOKEN}
```

## How the parts wire

**`connections:`** is the catalog — the recommended multi-repo shape. A connection holds host, org and auth exactly once, and repos are *discovered* under it via the provider API instead of being hand-listed. `azure_devops` connections need `organization` + `project`; `github` needs `owner`; `gitlab` needs `group`.

**`projects.azuredevops-todolist.repos:`** references discovered repos by glob: `acme/TodoList.*` pulls in every matching repo, and a `"!..."` entry excludes one from the match. An exact (non-glob) reference like `acme/TodoList.Api` resolves statically from the connection with no live discovery — which is why it also works offline and from the CLI. A list item may also be a mapping when one repo needs a setting of its own:

```yaml
    repos:
      - acme/TodoList.*
      - { repo: acme/TodoList.Web, default_branch: develop }
```

The classic top-level `repos:` catalog (one entry per repo, `type` + `url` + `auth`, referenced by key) stays available as the escape hatch for a single repo that isn't discoverable through a provider API — see [Repos: mono-repo](repos-mono.md) for that shape. A repo can appear in more than one project either way — handy if you have shared library repos.

**Path-prefix routing.** When the agent calls `read_file("TodoList.Api/src/Auth.cs")`, the framework parses the first path segment, looks up `Sandboxes["TodoList.Api"]`, and forwards the read to that sandbox. Same logic for write, edit, find_files, grep_in_tree, etc. The run_command tool requires an explicit `repo` argument so there's no path-segment guessing.

**Per-repo bootstrap.** Each repo needs its own `.agentsmith/context.yaml` and `.agentsmith/coding-principles.md` so Agent Smith knows the toolchain and the project rules per repo. The `init-project` pipeline writes these files into each repo's sandbox and opens one bootstrap PR per repo, cross-linked. Run it once per project (which means once for all the repos in the project — `init-project` iterates internally).

**`deployment`** is the single registry + version pin; it feeds both the orchestrator container and the sandbox-agent image. Skills need no block at all — they ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

## Toolchain images per repo

Each sandbox gets a toolchain image matching its repo's language automatically. When the defaults don't fit — an internal registry mirror, a newer SDK — pin per language on the project:

```yaml
projects:
  azuredevops-todolist:
    sandbox:
      images:
        dotnet: my-mirror.azurecr.io/dotnet/sdk:9.0
        node:   my-mirror.azurecr.io/node:22
```

A whole-project `sandbox.toolchain_image` wins outright over the per-language map. Independently of both, each repo's `.agentsmith/context.yaml` can carry an LLM-proposed `stack.image` and `stack.resources`; those are used only by code-changing pipelines.

## Branch coherence

Every repo in the run uses the same branch name: `agentsmith/ticket-{N}`. Reviewers see the same name across all sibling pull requests, which makes the multi-repo change set legible at a glance. If a repo had no actual changes after the agent run, the PR is skipped for that repo and the branch isn't pushed.

## What you get in the ticket comment

```
Resolved by Agent Smith (run 2026-05-22T14-03-11-9f2a).

Pull requests:
- TodoList.Api    https://dev.azure.com/.../pullrequest/4471
- TodoList.Worker https://dev.azure.com/.../pullrequest/4472
- TodoList.Web    https://dev.azure.com/.../pullrequest/4473
- TodoList.Docs   (no changes — skipped)

Cost: 1.40 USD. 47 tests passed across the changed repos.
```

## Next

- [Repos: mono-repo](repos-mono.md) — if you were here by mistake.
- [Methodology](../how-it-works/methodology.md) — what plan / review / verify do across repos.
- [Multi-repo, deeper](../how-it-works/multi-repo.md) — the conceptual deep-dive.
- [Trigger it](../trigger-it/webhooks.md) — wiring the tracker so the run starts on a ticket update.
