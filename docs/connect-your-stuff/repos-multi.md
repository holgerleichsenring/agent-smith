# Repos: multi-repo

One product, multiple repos. One ticket, one run, multiple pull requests. This is what made me build Agent Smith the way it is — most real products I've worked on span two or three repos and a "fix the auth flow" ticket touches all of them.

## What multi-repo means here

A multi-repo project is one entry under `projects:` with more than one repo in its `repos:` list. When a ticket targeted at that project comes in, Agent Smith:

- Spawns **one sandbox per repo**, each with its own toolchain image (a .NET repo gets a .NET SDK image, a Node repo a Node image). A repo whose contexts need different toolchains gets one sandbox per toolchain.
- Clones each repo into its sandbox at `/work`.
- Cuts a branch named `agent-smith/{ticket}` in every repo so reviewers see the same branch name across all sibling PRs.
- Works from **one specification and one agent conversation** across the whole set. The file-tool calls dispatch by path prefix — `TodoList.Api/src/Auth.cs` ends up in the api sandbox, `TodoList.Web/src/auth/login.ts` ends up in the web sandbox.
- Opens **one pull request per repo**, all cross-linked.
- Sets the ticket back to resolved with every PR URL in the comment.

## In the studio

Configuration for a server lives in the database and you edit it in the dashboard: switch the left rail to **Configuration** and work down the catalogs. The order matters, because each entry references the one before it: **Secrets** (names only), then **Agents**, then **Repositories**, then a **Tracker**, then a **Project** that wires them together. References are picked from dropdowns, so a project can't point at something that doesn't exist, and the drawer keeps **Create** disabled until every reference resolves.

For several repos, tick them all on the project drawer's **repos** tab. If they live under one org, group or Azure DevOps project, add a **Connection** instead and let the project pull repos from that scope. Pick the connection and the drawer lists what discovery found there, with a filter box and a count of how many matched. Tick individual repos, or type a wildcard like `TodoList.*` into the filter and add it as a rule: the rule shows how many repos it matches right now, and repos it covers show as covered instead of offering a second pick. A rule means a new sibling repo doesn't need a new catalog entry.

The full tour is on [The Config studio](../configure-it/config-studio.md); what follows is the same wiring written as YAML, which is what the CLI reads directly and what `agent-smith config import` takes.

## The same thing as YAML


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
        agent-smith:bug:                code
        agent-smith:feature:            code

secrets:
  azure_openai_api_key: ${AZURE_OPENAI_API_KEY}
  azure_devops_token:   ${AZURE_DEVOPS_TOKEN}
```

## How the parts wire

**`connections:`** is the catalog, and the recommended multi-repo shape. A connection holds host, org and auth exactly once, and repos are *discovered* under it via the provider API instead of being hand-listed. `azure_devops` connections need `organization` + `project`; `github` needs `owner`; `gitlab` needs `group` (a subgroup path such as `acme-org/team-platform` works too); `host` overrides the API host for GitHub Enterprise or self-managed GitLab.

On GitLab, a repo discovered under a group is named by its path relative to that group: a project in a subgroup is `team-platform/todolist-api`, one directly in the group keeps its short name. Two repos called `todolist-api` in different subgroups therefore stay two repos, and a wildcard matches against that relative path.

**`projects.azuredevops-todolist.repos:`** references discovered repos by glob: `acme/TodoList.*` pulls in every matching repo, and a `"!..."` entry excludes one from the match. An exact (non-glob) reference like `acme/TodoList.Api` resolves statically from the connection with no live discovery, which is why it also works offline and from the CLI. The wildcard is `*`. A list item may also be a mapping when one repo needs a setting of its own:

```yaml
    repos:
      - acme/TodoList.*
      - { repo: acme/TodoList.Web, default_branch: develop }
```

A branch set this way is a fallback. Agent Smith reads each repository's own default branch from the platform and uses that; the configured `default_branch` (on the repo item, or on the connection) applies only when the platform has no answer, and `main` when neither does. When the configured value and the repository disagree, the run logs a warning naming both.

The classic top-level `repos:` catalog (one entry per repo, `type` + `url` + `auth`, referenced by key) stays available as the escape hatch for a single repo that isn't discoverable through a provider API — see [Repos: mono-repo](repos-mono.md) for that shape. A repo can appear in more than one project either way — handy if you have shared library repos.

**Path-prefix routing.** When the agent calls `read_file("TodoList.Api/src/Auth.cs")`, the framework parses the first path segment, looks up `Sandboxes["TodoList.Api"]`, and forwards the read to that sandbox. Same logic for write, edit, find_files, grep_in_tree, etc. The run_command tool requires an explicit `repo` argument so there's no path-segment guessing.

**Per-repo bootstrap.** Each repo needs its own contexts, `.agentsmith/contexts/<name>/context.yaml` and `principles.md`, one directory per component, so Agent Smith knows the toolchain and the rules for each. The `init-project` pipeline writes them into each repo and opens one bootstrap PR per repo, cross-linked. Run it once per project, for example with the **Initialize** button on the project card; it covers every repo in the project.

**`deployment`** is the optional registry + version pin for the sandbox-agent image, whose tag is otherwise derived from the running server's release. Skills need no block at all — they ship embedded in the release; a `skills:` block is only an override for skills development or air-gap mirrors (see [Skills catalog](../how-it-works/skills-catalog.md)).

## Toolchain images per repo

Each sandbox gets a toolchain image matching its contexts automatically. When the defaults don't fit (an internal registry mirror, a newer SDK), pin per language on the project, in YAML or on the drawer's **sandbox** tab:

```yaml
projects:
  azuredevops-todolist:
    sandbox:
      images:
        dotnet: my-mirror.azurecr.io/dotnet/sdk:9.0
        node:   my-mirror.azurecr.io/node:22
```

The image is picked in this order: a whole-project `sandbox.toolchain_image` wins outright, then the per-language `sandbox.images` map, then the `stack.image` a context declares in its `context.yaml` (only if it comes from a registry the installation trusts), then the built-in image for the language, then a generic image that has git and no language toolchain at all. The run log names which of these decided and why, and warns when a run lands on the generic image, because such a sandbox can't build or test. A context's `stack.resources` sizes the sandbox for code-changing pipelines only.

## Branch coherence

Every repo in the run uses the same branch name: `agent-smith/{ticket}`. Reviewers see the same name across all sibling pull requests, which makes the multi-repo change set legible at a glance. If a repo had no actual changes after the agent run, the PR is skipped for that repo and the branch isn't pushed.

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
