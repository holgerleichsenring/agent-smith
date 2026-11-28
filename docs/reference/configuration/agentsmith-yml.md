# agentsmith.yml reference

!!! note "Which surface reads this"
    This page documents the file format. A **server** reads only the bootstrap slice (`persistence:`, `secrets:`, `auth:`, `tool_runner:`) from it and keeps everything else in its database, edited in the [Config studio](../../configure-it/config-studio.md). The **CLI** reads the whole file. Same shape either way, and `agent-smith config import` takes exactly this document. See [Where configuration lives](../../configure-it/index.md).

The file is a set of named **catalogs** (agents, trackers, connections, repos, MCP servers, secrets) plus **projects** that wire catalog entries together by name, plus a handful of global settings blocks. Names are matched without regard to case, so `TodoList` and `todolist` are the same project; two entries of one catalog that differ only in case are refused.

The annotated `config/agentsmith.example.yml` in the repo is the fullest worked example, and `config/agentsmith.schema.json` gives your editor completion (see [agentsmith.yml](../../configure-it/yaml.md#editor-support)).

The loader ignores a key it doesn't know, so an older file keeps loading after an upgrade. The schema is therefore where a misspelt key is caught: it declares exactly the keys the loader reads, and every block refuses any other. The one exception is a retired block kept so an older file still validates: `pipeline_storage` is declared, marked deprecated, and ignored. `agent-smith config import` names each key of the file it doesn't store, with the reason: retired, bootstrap-only, or no such setting. A retired key in a loaded configuration is also reported as an advisory startup finding.

## A complete small example

```yaml
agents:
  default-claude:
    type: claude
    catalog:
      sonnet: { model: claude-sonnet-4-6, tier: strong }
      haiku:  { model: claude-haiku-4-5-20251001, tier: fast }
    models:
      primary:       { use: sonnet }
      planning:      { use: sonnet }
      scout:         { use: haiku }
      summarization: { use: haiku }
    pricing:
      models:
        claude-sonnet-4-6:         { input_per_million: 3.0, output_per_million: 15.0, cache_read_per_million: 0.30 }
        claude-haiku-4-5-20251001: { input_per_million: 1.0, output_per_million: 5.0,  cache_read_per_million: 0.10 }

connections:
  acme:
    type: github
    owner: acme-org
    auth: github_token

trackers:
  acme-issues:
    type: github
    url: https://github.com/acme-org/todolist-api
    auth: github_token
    open_states: [open]
    done_status: closed
    default_pipeline: code
    pipeline_from_label:
      agent-smith:bug:     code
      agent-smith:feature: code

projects:
  todolist:
    agent: default-claude
    tracker: acme-issues
    repos:
      - acme/todolist-api
      - acme/todolist-web
    resolution:
      tag: todolist

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
  github_token:   ${GITHUB_TOKEN}
```

The prices above are placeholders; put in what your provider charges.

## agents

One entry per LLM configuration. A project names one with `agent:`.

| Key | Description |
|-----|-------------|
| `type` | `claude` (alias `anthropic`), `openai`, `azure_openai`, `gemini` (alias `google`), `ollama`, `copilot`, `external_worker`. See [AI providers](../../connect-your-stuff/ai-providers.md) |
| `endpoint`, `api_version` | provider endpoint: Azure OpenAI, Ollama, or with `type: openai` any OpenAI-compatible server (see [OpenAI-compatible servers](../../connect-your-stuff/ai-providers.md#openai-compatible-servers)) |
| `api_key_secret` | the name of the environment variable holding the key, when the provider default isn't it. With `type: openai` and an `endpoint` there is no default |
| `catalog.<entry>` | the models the agent may call, declared once each, see below |
| `models.<role>` | which model each role uses, see below |
| `model`, `deployment` | the inline form: the agent's own model, which an unset `primary` answers with. The config studio saves the catalog form and clears these |
| `pricing.models.<model>` | `input_per_million`, `output_per_million`, `cache_read_per_million` in USD |
| `cache` | `is_enabled` (default `true`), `strategy` (default `automatic`), prompt caching |
| `retry` | `max_retries` (5), `initial_delay_ms` (2000), `backoff_multiplier` (2.0), `max_delay_ms` (60000) |
| `network_timeout_seconds` | how long one provider call may take, default 300 |
| `max_concurrent_runs` | how many runs may use the agent's primary deployment (provider, endpoint, deployment) at once; unset is no limit, below 1 is refused. A run holds its place for the master and every sub-agent, so it never waits on itself; another run, design turn, admin turn or scan waits at its first model call and shows "Waiting for the model: … runs n at a time". Set 1 for a shared deployment whose tokens-per-minute quota one run fills |
| `rate_limit` | `requests_per_minute`, `input_tokens_per_minute`; unset takes the provider's default budget |
| `compaction` | `is_enabled` (true), `max_context_tokens` (200000), `max_context_tokens_trigger_ratio` (0.7), `keep_recent_iterations` (3), `fold_target_ratio` (0.35, the share a fold shrinks the view to), `summary_budget_ratio` (0.1, past it the summary segments are merged). It summarizes with the `summarization` role |
| loop tuning | `max_master_loop_iterations` (200), `max_sub_agent_loop_iterations` (100), `max_fix_iterations` (3, read only by a pinned catalog whose coding master predates 3.0.0), `ledger_reminder_every_n_iterations` (10), `checkpoint_push_min_interval_seconds` (120), `supports_vision` (true) |
| scan tuning | `scan_min_source_reads` (6), `scan_master_max_output_tokens` (32000), `scan_master_loop_iterations` (100), `scan_context_window_tokens` (200000) |
| dialog tuning | `dialog_master_loop_iterations` (200), `dialog_sub_agent_loop_iterations` (100), `dialog_review_look_allowance` (24, the looks a design turn's proposal and plan reviewers may take), `dialog_context_window_tokens` (200000; a design turn replays the previous turn's thread only when it can fold against a window), `dialog_transcript` (`verbatim_chars` 60000, `keep_recent_chars` 30000, `min_recent_turns` 2, `max_fold_chars` 200000: past `verbatim_chars` a digest folds the older turns) |

### agents.catalog

Each entry is one model the agent may call, under a name you choose. Two roles on one deployment name one entry instead of repeating it:

```yaml
catalog:
  sonnet:
    model: claude-sonnet-4-6
    max_tokens: 8192               # the OUTPUT cap, default 8192
    deployment: gpt4-1-deployment  # Azure OpenAI deployment name, when it differs from the model
    context_window_tokens: 200000  # optional: the INPUT window the deployment accepts
    provider_type: openai          # optional: answer on another provider than the agent's
    endpoint: https://…            # optional: its own host; unset takes the agent's endpoint
                                   #   when it runs on the agent's provider
    tier: strong                   # optional: strong | fast, your word for the model
    api: responses                 # optional: chat_completions (default) | responses
    reasoning_effort: medium       # optional, only with api: responses; sent as written
```

`api: responses` calls the model on the OpenAI Responses API instead of Chat Completions. Use it for a reasoning model that calls tools: a GPT-6 model calls tools on Chat Completions only at effort `none`, or not at all. The call is stateless (`store: false`), and the model's encrypted reasoning items are sent back with every tool result in the same loop. On Azure the request goes to `{endpoint}/openai/v1/responses` with the deployment as the model and no `api-version`. `reasoning_effort` is sent as you write it (`none`, `low`, `medium`, `high`, `xhigh` or `max`; which ones a model takes is up to the provider). On Chat Completions it is refused, because gpt-5.6 fails with tools at any effort other than `none`. Reasoning tokens are billed inside the output tokens and recorded per call.

Two models side by side, for example to compare them:

```yaml
agents:
  azure:
    type: azure_openai
    endpoint: https://my-resource.openai.azure.com/
    catalog:
      terra:   { model: gpt-5.6, deployment: terra }
      sol:     { model: gpt-6-sol, deployment: sol, api: responses, reasoning_effort: medium }
    models:
      primary: { use: sol }     # or terra; every role names its entry
      scout:   { use: terra }
```

A second agent with the other assignment, named by a second project, runs the same work on the other model. `GET /api/runs/{runId}/measurement` and `GET /api/series-runs/{id}/measurement` return the comparison: questions asked, delivered specs or tickets, cost, cost per delivered ticket, and per model the calls, input (cached), output (reasoning) tokens and cost. A model the bundled price list doesn't know needs a price under `pricing.models`.

`tier` is advice, never enforced. The roles that decide structure (`primary`, `planning`, `reasoning`, `context_generation`, `code_map_generation`) need a strong model; one that resolves to an entry marked `fast` is reported as an advisory startup finding. An entry with no tier is never reported, because agent-smith doesn't rate models.

### agents.models

A role names its catalog entry with `use:`. No role has a built-in model. An unset role inherits, `max_tokens` included:

| Role | Used for | Unset means |
|------|----------|-------------|
| `primary` | the agentic work | the agent's `model` and `deployment` |
| `scout` | code analysis, file discovery | `primary` |
| `planning` | cutting the work into specs | `primary` |
| `summarization` | condensing long histories | `primary` |
| `reasoning` | extended thinking | `primary` |
| `context_generation` | discovering components and writing each `context.yaml` | `primary` |
| `code_map_generation` | the repo analyzer | `scout`, then `primary` |

```yaml
models:
  primary: { use: sonnet }
  scout:   { use: haiku }
```

A `use:` that names no entry of the catalog is reported at startup; a run that reaches the role fails. The config studio and `config import` refuse it.

The inline form still loads: instead of `use:`, a role may state its model itself, with the same keys as a catalog entry except `tier`. With `use:` set, the inline keys beside it are ignored, and the studio and `config import` refuse a role that carries both:

```yaml
model: claude-sonnet-4-6
max_tokens: 8192               # the OUTPUT cap
deployment: gpt4-1-deployment  # Azure OpenAI deployment name, when it differs from the model
context_window_tokens: 200000  # optional: the INPUT window the deployment accepts
provider_type: openai          # optional: answer this role on another provider than the agent's
endpoint: https://…            # optional: this role's own host; unset takes the agent's endpoint
                               #   when the role runs on the agent's provider
```

`context_window_tokens` is unset by default, because the model name doesn't imply it. The config studio offers the price list's window as a one-click value, but saves only what you confirm. State it and the tool loop for that role folds its history and finishes before the provider refuses; preflight reports a compaction threshold that could never fire below a stated window.

Every catalog model needs a price: the bundled price list knows most hosted models by id, and `pricing.models` states or overrides the rest. The studio refuses to save an agent with a catalog model neither knows. Tokens nothing can price are counted, and the run's cost is then marked incomplete rather than shown as a total.

## connections

A discovery scope: host, org and auth once, and repos found under it by the provider API.

| Key | Description |
|-----|-------------|
| `type` | `github`, `gitlab`, `azure_devops` |
| `owner` | GitHub owner or org |
| `group` | GitLab group, a subgroup path works too |
| `organization`, `project` | Azure DevOps organization and project |
| `host` | API host for GitHub Enterprise or self-managed GitLab |
| `auth` | name of an entry in `secrets:` |
| `default_branch` | fallback only, used when the platform can't say what a repo's default branch is |

A project references a connection's repos as `<connection>/<repo>`: exactly (`acme/todolist-api`) or by wildcard rule (`acme/todolist-*`, `"!acme/todolist-legacy"`). [agentsmith.yml schema](agentsmith-yml-schema.md#repos-entry-forms) has how each form resolves. GitLab repos found under a group are named by their path relative to it (`team-platform/todolist-api`).

## repos

One entry per individual repository, for a repo no connection covers.

| Key | Description |
|-----|-------------|
| `type` | `github`, `gitlab`, `azure_devops`, `local` |
| `url` | clone URL (remote types) |
| `path` | filesystem path (`local`) |
| `organization`, `project` | Azure DevOps |
| `auth` | name of an entry in `secrets:` |
| `default_branch` | fallback only, see below |

**Default branch.** The repository's own default branch, as the platform reports it, always wins. A configured `default_branch` (on the repo, on a project's repo item, or on the connection) applies only when the platform has no answer, and `main` when nothing does. When a configured value disagrees with the repository, the run logs a warning naming both.

## trackers

Where tickets come from and how their workflow looks. The tracker owns the workflow for every project routed to it.

| Key | Description |
|-----|-------------|
| `type` | `github`, `gitlab`, `azure_devops`, `jira` |
| `url` | GitHub: the repository whose issues are the tickets. Jira: the site. Azure DevOps: optional, an Azure DevOps Server base such as `https://tfs.example.com/tfs` (the `organization` is its collection); empty or a cloud URL means `https://dev.azure.com/{organization}` |
| `organization`, `project` | Azure DevOps organization and project. GitLab: `project` is the project path. Jira: `project` is the project key |
| `auth` | name of an entry in `secrets:` |
| `open_states` | states a ticket may be in to be picked up |
| `trigger_statuses` | states that start a run, falls back to `open_states` |
| `done_status` | where a finished run moves the ticket |
| `failed_status` | where a failed run moves it; must lie outside `trigger_statuses` |
| `needs_clarification_status` | where a run parks a ticket it has questions about |
| `not_implementable_status` | where a ticket goes that can't be implemented as written; falls back to `needs_clarification_status` |
| `close_transition_name` | Jira: the transition that reaches `done_status` |
| `closed_status` | Jira, Azure DevOps: the status "also close on the tracker" moves a ticket into. Unset: `done_status` |
| `pipeline_from_label` | label → pipeline. Matched in order, first hit wins. With entries, a ticket matching none isn't routed |
| `default_pipeline` | what a ticket runs when the label map is empty. Unset everywhere means `code`, with a startup finding |
| `in_progress_status` | where a parked run's ticket returns when the run resumes; must lie outside `trigger_statuses` |
| `comment_keyword` | a keyword in a ticket comment that triggers a run |
| `pr_trigger_label` | GitHub and GitLab: a pull request label that asks for a review. `security-review` always does too |
| `assignee_name` | Jira: the webhook starts work when an issue is assigned to this user, default `Agent Smith` |
| `webhook_secret` | Jira: the webhook shared secret as a `${name}` reference, see [Webhooks](webhooks.md) |
| `label_names` | renames the labels the framework writes. Keys: `pending`, `enqueued`, `in-progress`, `done`, `failed`, `waiting`, `shortfall`, `approved-set` |
| `lifecycle_status_names` | Jira: lifecycle state → native workflow status, instead of carrying the lifecycle only as labels |
| `work_item_kinds` | Azure DevOps and Jira: filing role (`work`, `bug`, `spec` — `phase` still accepted — `chat`) → work-item or issue type. GitHub and GitLab ignore it |
| `extra_fields` | additional work-item fields to fetch |
| `zero_match_comment` | comment on a ticket no project matched |
| `polling` | `enabled` (false), `interval_seconds` (60). See [Polling](../../trigger-it/polling.md) |
| `endpoints` | Jira: override individual REST paths — `search`, `issue`, `comment`, `transitions`, `create`. A path left out keeps its Jira Cloud v3 default. See [Jira](../../connect-your-stuff/tracker-jira.md) |

A label the framework writes may not be spelled like one of your routing words (a `pipeline_from_label` key or a project's resolution value); that configuration is refused. The tracker pages under [Connect your stuff](../../connect-your-stuff/tracker-azure-devops.md) show each type in context.

The pipelines a label map or `default_pipeline` may name are the ones a ticket can be routed to: `code`, `security-scan`, `api-security-scan`, `pr-review`, `mad-discussion`, `legal-analysis`. The retired names `fix-bug`, `fix-no-test`, `add-feature` and `phase-execution` no longer run. Write `code`.

## projects

A project wires one agent, one tracker and a set of repos, and says how a ticket finds it.

| Key | Description |
|-----|-------------|
| `agent` | name from `agents:` |
| `tracker` | name from `trackers:` |
| `repos` | list of repo references, always a list. See [repos entry forms](agentsmith-yml-schema.md#repos-entry-forms) |
| `resolution` | how a ticket finds this project, one entry `{ strategy: value }`: `tag`, `area_path`, `repo`, `to_address`. See [Project resolution](project-resolution.md) |
| `templates` | what this project's contexts are built after. See [Project templates](../../configure-it/templates.md) |
| `default_pipeline` | the project's default pipeline (CLI runs and fallback paths). What a *ticket* runs is decided by the tracker's routing |
| `pipelines` | pipelines this project hosts with their own overrides: `name`, `agent`, `skills_path`, `coding_principles_path` |
| `coding_principles_path`, `skills_path` | project-level overrides |
| `sandbox` | per-project sandbox overrides, see below |
| `registries` | private package feeds this project's runs authenticate against in the sandbox: a list of `host`, `username` and `token` (a `${secret}` reference). A repository's `nuget.config`, `.npmrc` or other feed config naming the host gets user-level credentials staged. The top-level `registries:` list is retired; `database migrate` copied a stored one into every project that had none |
| `merge` | whether a green code run merges its own pull requests, see below |
| `design_sources` | names from `design_sources:` this project may read designs from. See [Design sources](../../configure-it/design-sources.md) |

### Trigger blocks

`github_trigger`, `gitlab_trigger`, `azuredevops_trigger` and `jira_trigger` are retired: a project routes by `resolution:` and its tracker carries the workflow. A file that still has one keeps loading — at load the block fills whatever the project and its tracker leave unset — and a server moves its stored blocks onto the trackers once, in the background. See [Project resolution](project-resolution.md).

### projects.templates

```yaml
templates:
  - context: api                 # a context of this project
    context_repo: todolist-api   # optional, when two repos declare the context name
    project: acme-orders         # the project the template belongs to
    repo: orders-api             # one repo ref of that project, no wildcard
    template_context: service    # the context inside that repo
    revision: v2.4.0             # optional: tag, branch or commit; empty = default branch
```

A binding with `kind: deployment` (default `code`) says how the context is delivered, and lists `environments`, each with `name`, optional `repo` (a repos-catalog ref holding the manifest; default the context's own repo), `approval` (default `true`: a person merges the delivery's pull request) and `parameters` (string values of the template's parameters). See [Deployment templates](../../configure-it/deployment-templates.md).

Refused: an unknown project, a repo that project doesn't carry, a wildcard repo, a `context_repo` this project doesn't carry, a cycle, an unknown `kind`, `environments` on a code binding, an unnamed or repeated environment, and an environment repo that is a wildcard or doesn't resolve. [Project templates](../../configure-it/templates.md) explains what reads them.

### projects.sandbox

| Key | Description |
|-----|-------------|
| `toolchain_image` | one image for the whole project, wins over everything else |
| `images` | language → image, overrides the built-in image per language |
| `resources` | cpu and memory requests and limits, all four or none |
| `step_timeout_seconds`, `run_command_timeout_seconds` | override the global timeouts |
| `agent_registry`, `agent_version` | override the sandbox agent image |
| `hold_seconds` | how long this project's design conversations hold their sandboxes between turns |
| `run_as_root` | with `sandbox.hardening_enabled` on, `true` runs this project's sandboxes as root with default capabilities, for prerequisites that need root; every run then carries a warning |
| `secrets` | `env` (`VAR: "secretName:key"`, every repository's commands), `repo_env` (`<repo>: { VAR: "secretName:key" }`, only that repository's commands; needs sandbox agents of wire protocol 3) and `files` (`mount`, `secret`, `key`): Kubernetes Secrets mounted into the sandbox |

Empty means inherit. [Sandbox architecture](../concepts/sandbox-architecture.md) has what each one does at runtime.

### projects.review

```yaml
review: end                         # end (default) | each | none
```

When a person looks at the project's work (2026-11-25-9992i). A series copies it when it opens and keeps it.

| Value | A series | A single ticket |
|-------|----------|-----------------|
| `end` | Works on a series branch (`agent-smith/series/<id>`, one per repository it touches). The series master merges each ticket into it, past protection: the branch carries no host policy. One final pull request per repository goes into the default branch, its description a section per ticket (title and pull request, the master's acts with their reasons, what it could not verify); the master asks before it, and a person reviews it. | A series of one: the same as `each`. |
| `each` | No series branch. Each ticket's pull request goes into the default branch; the master asks after each ticket. | A series of one: the ticket page's *Your move* asks — **Merge** merges it, or look at the pull request first; merging on the host works too. |
| `none` | No series branch. The master merges each ticket into the default branch on green. | A series of one: its master merges it on green. |

Every merge question offers **Merge it** and **Look at the pull request first** with a link to each pull request, and takes no default.

The host's branch protection is a limit of the environment, not a setting: a merge into the default branch arms Azure DevOps auto-complete, GitHub auto-merge or GitLab's merge when the pipeline succeeds, and the host's rules decide. Only a merge into a series branch and a merge the series master asks for with `how: now` go past protection; those need the token's bypass permission (Azure DevOps *Bypass policies when completing pull requests*; GitHub repository admin with bypassing allowed, or the token's actor on a ruleset's bypass list; GitLab has no API bypass and merges when its checks allow).

`review` replaced four keys of the merge block (retired 2026-11-25). A file still naming one of them gets an advisory finding naming the key; a stored configuration was moved by `database migrate` once, erring toward more review: a project whose series tickets waited for a person became `each`, otherwise one whose green runs merged nothing by themselves became `end`, and one that merged on green became `none`. The migration's log names every project whose single tickets now behave otherwise. A conflicting pull request is refreshed by its series master (`refresh_branch`), nothing refreshes by itself.

### projects.merge

```yaml
merge:
  ci_excerpt_chars: 4000            # default 4000
  ci_failed_checks_read: 5          # default 5
  ci_poll_seconds: 60               # default 60
  ci_settle_seconds: 180            # default 180
  ci_pending_seconds: 3600          # default 3600
  ci_fix_attempts: 3                # refresh-base repair passes: default 3
```

How the host's CI is read; every key applies under every review unless it says otherwise.

| Key | Description |
|-----|-------------|
| `ci_poll_seconds` | How often the open pull requests' pending CI is read. Also how often a deploy's pull request or pipeline run is read while it runs ([following a deploy](../../configure-it/deployment-templates.md#following-a-deploy)). Default 60. |
| `ci_settle_seconds` | How long a head with no CI reporting must stay so before it counts as having none; the series master then queues a build that never started again or merges. Default 180: a host registers its checks seconds to minutes after a push. |
| `ci_pending_seconds` | After how long a head still pending wakes its series master once; it is read every `ci_poll_seconds` for up to twice this, and the five-minute re-read still sees it move afterwards. A deploy still pending this long after it was delivered stops being followed, and says so. Default 3600. |
| `ci_fix_attempts` | How many repair passes a refresh-base run may make on a build or test stage the merged base turned red (default 3). A red CI starts no fix run by itself; the series master decides. |
| `ci_excerpt_chars` | How many characters of each failed CI check's evidence are read, stored and shown: the host's summary and annotations, then the end of its log. Default 4000. |
| `ci_failed_checks_read` | How many failed checks of one red head have their logs read; the rest are named with their links. Default 5. |

A pull request its host says conflicts with its base gets its base merged in by a `refresh-base` run, pushed without force. Index lines that both sides added to `.agentsmith/contexts/*/done.md` and `.agentsmith/memory/MEMORY.md` merge with `merge=union` and keep both sides' lines; a master resolves any other conflict, and the declared `verify` stages build and test every merge before the push. A merge that can't be resolved or stays red leaves the branch unchanged, is said on the pull request and holds the ticket for a person.

Every open pull request's CI is read with its record and shown on the ticket page: `none` (nothing reports on the head), `pending`, `green` or `red`. One rule decides for every host: where checks are marked required only those count; any failed check is red; else anything running is pending; else green. GitHub reads the check runs and commit statuses of the head (a failed GitHub Actions job adds the end of its log), Azure DevOps the build-validation policies (the failed build's task issues and log end) and the pull request's statuses of its latest iteration, GitLab the head pipeline (each failed job's stage, name and trace end). The token needs read access to checks, commit statuses and Actions logs on GitHub, Build (read) on Azure DevOps, and `read_api` on GitLab; a refused read leaves the stored CI as it was.

A merge the host refuses leaves the pull request open. The step result and the run's pull-request record name the reason; a missing bypass permission reads `cannot bypass <policy> on <provider>: <reason>; grant <permission>, or merge with how auto so the host's rules decide`. Repositories merge in order and the first refusal stops the rest. A pull request opened against a feature rung is left open. A local repository fast-forwards its default branch either way.

Nothing approves a pull request. A review the host requires is a person's: the armed auto-complete or auto-merge waits for it. (`approval_auth` was retired on 2026-11-25.)

### projects.machine

The identity agent-smith acts under on each hoster: every commit it makes carries its name and email, and every push, pull request, comment, merge and auto-complete goes out with its token, so the hoster records each act under the machine. A hoster the project has repositories on and no entry for acts with each repository's own `auth` token. The project's settings say so in a finding.

```yaml
machine:
  name: agent-smith-bot                  # commit author and the name agent-smith's own records book acts under
  email: agent-smith-bot@example.com     # commit email
  github:
    auth: github_app_key                 # a GitHub App's private key (PEM), or a bot user's token without app_id
    app_id: "123456"                     # GitHub App id or client id
    installation_id: "7890123"           # the App's installation on the repositories' owner
    email: "123+agent-smith-bot[bot]@users.noreply.github.com"   # optional: this hoster's commit email
  gitlab:
    auth: gitlab_bot_token               # a project or group access token (its bot user)
  azure_devops:
    auth: azdo_bot_pat                   # a bot user's personal access token, or a service principal's client secret
    tenant_id: 00000000-0000-0000-0000-000000000000   # optional: with client_id, a Microsoft Entra service principal
    client_id: 11111111-1111-1111-1111-111111111111
```

| Key | Description |
|-----|-------------|
| `name`, `email` | Required as soon as one hoster is named. The commit author and committer on every repository of the project; series acts are booked and said under `name`. |
| `<hoster>.auth` | The secret behind every act on that hoster. With `app_id` on GitHub it is the App's private key: agent-smith signs a JWT with it and trades that for an installation token, renewed before it expires. |
| `github.app_id`, `github.installation_id` | Make the GitHub entry a GitHub App. Both are needed. |
| `azure_devops.tenant_id`, `azure_devops.client_id` | Make the Azure DevOps entry a Microsoft Entra service principal; `auth` is then its client secret. agent-smith trades it for an Entra access token (client credentials grant, renewed before it expires) and sends that as `Authorization: Bearer` on every Azure DevOps call and as git's bearer header on every clone, fetch and push. Both are needed. |
| `<hoster>.email` | This hoster's commit email when it differs from `email`. For a GitHub App, use its bot address so GitHub links the commits to it. |

Setting it up:

- **GitHub:** create a GitHub App with Contents, Pull requests and Workflows (write: a change under `.github/workflows/` is pushed too) and Checks, Commit statuses and Actions (read) permissions, install it on the owner of the repositories, and store its private key as a secret. Alternatively, use a machine user's token without `app_id`.
- **GitLab:** create a project or group access token (`api`, `write_repository`), which comes with its own bot user, and store it as a secret.
- **Azure DevOps:** create a user for the bot, give it Contribute and Contribute to pull requests on the repositories (plus *Bypass policies when completing pull requests* for merges into a series branch and the series master's `how: now`), and store its personal access token as a secret. Or use a service principal, which needs no person's account:
  1. The Azure DevOps organization must be connected to Microsoft Entra ID (*Organization settings → Microsoft Entra*). In that tenant, register an app and create a client secret. Note the tenant id and the application (client) id.
  2. In Azure DevOps, under *Organization settings → Users*, add the service principal with the **Basic** access level (Stakeholder has no Azure Repos). Search for it by the app's name; Azure DevOps keys it by the object id under *Enterprise applications*, not the one under *App registrations*.
  3. Add it to each project's **Contributors** group. That gives Contribute, Create branch, Contribute to pull requests and work item edits on the project's boards. If repositories or area paths narrow those rights, grant them to the principal there.
  4. For merges into a series branch and the series master's `how: now`, also grant *Bypass policies when completing pull requests* on the repositories. No security group has it by default.
  5. Store the client secret as a secret and set `tenant_id` and `client_id`. Entra client secrets expire; renew the secret before it does.
  Azure DevOps Server has no Entra principals; use a bot user's PAT there. The bearer header is passed to git with `--config-env`, which needs git 2.31 or later in the sandbox image.

The project's tracker writes under the same block: comments, label and status moves and description edits on a GitHub, GitLab or Azure Boards tracker go out with the entry for the tracker's hoster (a GitHub App must be installed on the tracker's repository too). A Jira tracker writes as the account its own `auth` and `email` name, so make that a bot account. Reads — polling and the change sweep — stay on the tracker's own `auth`, and so does the zero-match comment (`zero_match_comment`), which no project owns. When projects share a tracker, give them the same identity for its hoster: each project tells its own moves apart only by its own identity, and a finding says when they differ. Moves made under the old token before the switch count as a person's.

The preflight repository checks probe each project's repositories with the token their acts use.

agent-smith never approves its own pull requests. Where a policy wants a person's review, the person gives it.

## secrets

Names mapped to environment variable references. The file never holds a value, and a config that carries a raw secret is refused.

```yaml
secrets:
  github_token:      ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
```

Every repo, connection and tracker authenticates with the secret its own `auth` names, for API calls and for clone, fetch and push alike, so two GitLab instances or two Azure DevOps organizations each get their own token. An `auth` that names no entry here is a blocking finding. A Jira tracker also needs `email`, the account its token belongs to.

A configuration written before this rule keeps working. An empty `auth` is filled at load with its type's legacy secret (`github_token`, `gitlab_token`, `azure_devops_token`, `jira_token`), and that secret is added as `${GITHUB_TOKEN}` (and so on) when the catalog lacks it. An empty Jira `email` comes from the `jira_email` secret, else `JIRA_EMAIL`. An empty GitLab or Jira tracker `url` or `project` comes from `GITLAB_URL`, `JIRA_URL`, `GITLAB_PROJECT` or `JIRA_PROJECT`, and the `host` of a GitLab repo under `repos:` comes from `GITLAB_URL`. Each fill shows up as an advisory startup finding, and so does an `auth` that names a different secret while the old variable is still set, because that is the moment the token in use changes.

## pipeline_triggers

A global label → pipeline map, used when the tracker declares no `pipeline_from_label`. There's no studio screen for it; on a server, edit it through export and import.

```yaml
pipeline_triggers:
  agent-smith:bug:     code
  agent-smith:feature: code
  security-review:     security-scan
```

## Global settings

These blocks apply to every project unless a project overrides them. On a server they're the [Settings](../../configure-it/settings.md) groups.

| Block | What it sets |
|-------|--------------|
| `deployment` | `registry`, `version`: an optional image pin for the sandbox agent |
| `orchestrator` | `max_run_wall_time_seconds` (1800; a PoC run is held to `pocs.run_wall_time_seconds` instead) |
| `sandbox` | agent image, `step_timeout_seconds` (900), `run_command_timeout_seconds` (300), `max_concurrent_sandboxes`, `hold_seconds` (180), registry trust, pull secrets and `hardening_enabled` (false: sandboxes run as uid 1000 with no capabilities when true) |
| `primary_provider` | the agent used when a project names none |
| `limits` | per-skill ceilings of the agentic loop |
| `pipeline_cost_cap` | money and token caps per run, see [Pipeline cost cap](pipeline-cost-cap.md) |
| `queue` | `max_parallel_jobs` (4), `shutdown_grace_seconds` (30), `redis_retry_interval_seconds` (30) |
| `dialogue` | `hot_wait_seconds` (600), `approval_timeout_seconds` (259200), `dashboard_url`, `admin_agent` (the agent the admin assistant runs on), `admin_read_limit` (50), `admin_result_max_chars` (40000), `series_master_budget_usd` (20), `series_master_budget_ask_usd` (2), `series_master_thread_max_chars` (60000), `series_master_thread_keep_turns` (4), `series_status_minutes` (15), `series_wake_settle_seconds` (60), `question_default_minutes` (120) |
| `skills` | an override for where the skill catalog comes from; normally unset |
| `pipeline_data_flow` | whether the data-flow gate warns or enforces |
| `trace` | `enabled`: record every model call's prompt and answer. `AGENTSMITH_TRACE`, when set, wins over it |
| `spec_review` | `reground_read_budget` (24): the file reads that re-check the citations of the spec a run enters (`design_turn`, `rounds`, `reviewers`, `max_concurrent`, `budget_share_percent`, `attack_look_floor` and `series_contract_max_chars` are retired) |
| `series` | `max_specs` (24, at most 26), `specs_per_attempt` (8, at most 18), `seconds_per_spec` (0 = not measured), `budget_usd` (150, `0` = no attempt starts by itself), `wall_clock_hours` (12), `continue_after_wall_time` (true) |
| `pocs` | PoCs, off by default: `enabled` (false), `project_agent` and `project_tracker` (what every `poc-` project names; required while on), `machine` (the identity every PoC act runs under — the target install, source and GitOps commits and builds — for a `poc-` project without a `machine:` block of its own; the same block as [projects.machine](#projectsmachine), a finding names a target whose source repository's hoster it leaves out), `targets` (`name`, a DNS label; `source_repo`, one exact repo ref, no glob; `root` (`pocs`), `deploy_root` (`deploy`), `template_path` (`poc-template`), relative folders inside the source repo; `domain_pattern` with one `{app}`; `lifetime_days` (90); `reminder_days` ([14, 7, 1]); `per_person_limit` (3); `registry`, required: the container registry, `type` (`acr`), `login_server`, `repository` (`pocs`), `pull_user`, `pull_auth` (a secrets-catalog entry), `pull_mode` (`pull_secret` or `cluster_pulls`) — not the package-feed `registries:` list; `build`: how the source repo host's pipeline builds, scans and pushes, `pipeline` (`poc-build`), `github_environment` and `gitlab_environment` (`poc-push`), `push_user_secret` (`POC_REGISTRY_USER`), `push_password_secret` (`POC_REGISTRY_PASSWORD`), `azure_service_connection` (`poc-registry`), `queue_minutes` 3, `build_timeout_minutes` 12, `scan_timeout_minutes` 5, `push_timeout_minutes` 3, `poll_seconds` 15, `report_kb` 1024; `secrets`: `provider` (`sealed_secrets`), `sealed_secrets_cert` (the controller's public certificate, PEM), `sealed_secrets_cert_url` (https, for the form's Fetch); `ingress`: `class` (required while on), `tls_issuer` (a cert-manager ClusterIssuer, optional); `auth`: `ingress_annotations` (a map set on every PoC's Ingress, at least one while on); `network`: `ingress_controller_namespace` (required while on), `dns_namespace` (`kube-system`), `dns_pod_labels` ({`k8s-app`: `kube-dns`}), `egress_proxy` (`url`, http(s), required while on; `port` 3128, the proxy pod's port; `namespace` with `pod_labels`, or `cidr`, exactly one; `no_proxy` `localhost,127.0.0.1,.svc,.cluster.local`); `quota`: `cpu` 1, `memory` 1Gi, `pods` 4, `ephemeral_storage` 2Gi, at least twice each container limit and 2 pods; `container`: `cpu_request` 50m, `cpu_limit` 500m, `memory_request` 128Mi, `memory_limit` 512Mi, `ephemeral_limit` 256Mi; `argo`, required while on: the Argo CD that deploys the target's PoC folders, `url` (https), `token_secret` (the secret holding the AppProject role's JWT), `project` (the AppProject), `namespace` (`argocd`), `destination_server` (`https://kubernetes.default.svc`), `repo_url` (the source repo as Argo CD's repository credential names it), `application_set` (empty = `pocs-<target name>`), `token_warn_days` 14, `generator_requeue_seconds` 30, from 10 to 180), `archive` (`max_zip_mb` 25, `max_unpacked_mb` 100, `max_entries` 2000, `max_ratio` 100, `max_path_chars` 240 (at most 240), `max_file_mb` 10, `max_binary_mb` 20, `max_dockerfile_kb` 64, `mismatch_ratio_percent` 50 (at most 100), `mismatch_file_limit` 100), `scan` (`osv_image`, an own mirror pinned by digest, default `ghcr.io/google/osv-scanner:v2.2.4@sha256:f7ba…87c4`; `timeout_seconds` 300; `unfixed_critical_block` true; `secret_min_confidence` 8, from 1 to 10; `trivy_image`, an own mirror pinned by digest, default `aquasec/trivy:0.76.2@sha256:8f6b…68fb`), `variables` (`max_names` 64, `max_value_kb` 4), `gitops` (`commit_attempts` 3, `max_folder_files` 500), `rollout` (`poll_seconds` 10, from 1 to 60; `deadline_seconds` 600, at least 60; `end_margin_seconds` 60, at least 30; `commit_allowance_seconds` 120, at least 0), `lifetime` (`interval_seconds` 3600, at least 300; `extend_days` 90, at least 1; `max_owner_extensions` 2, at least 0; `unseen_delete_after_days` 30, at least 1: an expired PoC whose owner saw no reminder is stopped, then deleted that many days later unless extended), `run_wall_time_seconds` (2700: every PoC run is held to it instead of `orchestrator.max_run_wall_time_seconds`), `blocked_retention_days` (7: stored uploads are purged daily after it), `readiness_path_max_chars` (256: the longest readiness path an owner sets on a PoC's detail page). See [Settings](../../configure-it/settings.md#pocs), [The app template](../../configure-it/settings.md#the-app-template) and [PoC targets on Argo CD](../../configure-it/settings.md#poc-targets-on-argo-cd) |
| `role_mapping` | what a role name means: `role_claim`, `group_claim`, `group_roles`, `roles`, `person_grants`, `project_bindings` (`project`, `groups`, `people` as `claim`/`value`, `roles`: roles held in that project only, their run, init and dialog permissions), `observation_retention_days`. Edited on the Access page |
| `mcp_servers` | the studio's MCP server catalog: `transport`, `url`, `auth` |
| `design_sources` | the design source catalog: `vendor` (`figma`), `auth` (a name from `secrets:`), `display_name`. See [Design sources](../../configure-it/design-sources.md) |

## Bootstrap and file-only blocks

| Block | Description |
|-------|-------------|
| `persistence` | `provider` (`sqlite`, `postgresql`, `mysql`, `sqlserver`) and `connection_string`. Replaced by `AGENTSMITH_PERSISTENCE_PROVIDER` + `AGENTSMITH_PERSISTENCE_CONNECTION` when both are set |
| `auth` | the token authority dashboard sign-in validates against |
| `tool_runner` | how the api-scan tools run, see [Tool configuration](tools.md). Read from the file at start, never stored or imported |
