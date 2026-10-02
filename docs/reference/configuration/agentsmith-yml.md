# agentsmith.yml reference

!!! note "Which surface reads this"
    This page documents the file format. A **server** reads only the bootstrap slice (`persistence:`, `secrets:`, `auth:`, `tool_runner:`) from it and keeps everything else in its database, edited in the [Config studio](../../configure-it/config-studio.md). The **CLI** reads the whole file. Same shape either way, and `agent-smith config import` takes exactly this document. See [Where configuration lives](../../configure-it/index.md).

The file is a set of named **catalogs** (agents, trackers, connections, repos, MCP servers, secrets) plus **projects** that wire catalog entries together by name, plus a handful of global settings blocks. Names are matched without regard to case, so `TodoList` and `todolist` are the same project; two entries of one catalog that differ only in case are refused.

The annotated `config/agentsmith.example.yml` in the repo is the fullest worked example, and `config/agentsmith.schema.json` gives your editor completion (see [agentsmith.yml](../../configure-it/yaml.md#editor-support)).

The loader ignores a key it doesn't know, so an older file keeps loading after an upgrade. The schema is therefore where a misspelt key is caught: it declares exactly the keys the loader reads, and every block refuses any other. `agent-smith config import` names each key of the file it doesn't store, with the reason: retired, bootstrap-only, or no such setting. A retired key in a loaded configuration is also reported as an advisory startup finding.

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
| `rate_limit` | `requests_per_minute`, `input_tokens_per_minute`; unset takes the provider's default budget |
| `compaction` | `is_enabled` (true), `max_context_tokens` (200000), `max_context_tokens_trigger_ratio` (0.7), `keep_recent_iterations` (3). It summarizes with the `summarization` role |
| loop tuning | `max_master_loop_iterations` (200), `max_sub_agent_loop_iterations` (100), `max_fix_iterations` (3), `ledger_reminder_every_n_iterations` (10), `reminder_drift_editless_iterations` (8), `verdict_owed_after_iterations` (3), `checkpoint_push_min_interval_seconds` (120), `supports_vision` (true) |
| scan tuning | `scan_min_source_reads` (6), `scan_master_max_output_tokens` (32000), `scan_master_loop_iterations` (100), `scan_context_window_tokens` (200000) |

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
```

`tier` is advice, never enforced. The roles that decide structure (`primary`, `planning`, `reasoning`, `context_generation`, `code_map_generation`) need a strong model; one that resolves to an entry marked `fast` is reported as an advisory startup finding. An entry with no tier is never reported, because agent-smith doesn't rate models.

### agents.models

A role names its catalog entry with `use:`. No role has a built-in model. An unset role inherits, `max_tokens` included:

| Role | Used for | Unset means |
|------|----------|-------------|
| `primary` | the agentic work | the agent's `model` and `deployment` |
| `scout` | code analysis, file discovery | `primary` |
| `planning` | cutting the work into phases | `primary` |
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
| `url` | GitHub: the repository whose issues are the tickets. Jira: the site. Azure DevOps: optional |
| `organization`, `project` | Azure DevOps organization and project. GitLab: `project` is the project path. Jira: `project` is the project key |
| `auth` | name of an entry in `secrets:` |
| `open_states` | states a ticket may be in to be picked up |
| `trigger_statuses` | states that start a run, falls back to `open_states` |
| `done_status` | where a finished run moves the ticket |
| `failed_status` | where a failed run moves it; must lie outside `trigger_statuses` |
| `needs_clarification_status` | where a run parks a ticket it has questions about |
| `not_implementable_status` | where a ticket goes that can't be implemented as written; falls back to `needs_clarification_status` |
| `close_transition_name` | Jira: the transition that reaches `done_status` |
| `pipeline_from_label` | label → pipeline. Matched in order, first hit wins. With entries, a ticket matching none isn't routed |
| `default_pipeline` | what a ticket runs when the label map is empty. Unset everywhere means `code`, with a startup finding |
| `label_names` | renames the labels the framework writes. Keys: `pending`, `enqueued`, `in-progress`, `done`, `failed`, `waiting`, `shortfall`, `approved-set` |
| `lifecycle_status_names` | Jira: lifecycle state → native workflow status, instead of carrying the lifecycle only as labels |
| `work_item_kinds` | Azure DevOps and Jira: filing role (`work`, `bug`, `phase`, `chat`) → work-item or issue type. GitHub and GitLab ignore it |
| `extra_fields` | additional work-item fields to fetch |
| `zero_match_comment` | comment on a ticket no project matched |
| `polling` | `enabled` (false), `interval_seconds` (60), `jitter_percent` (10). See [Polling](../../trigger-it/polling.md) |
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
| `github_trigger`, `gitlab_trigger`, `azuredevops_trigger`, `jira_trigger` | a full trigger block, overriding the tracker field by field. Must match the tracker's type |
| `sandbox` | per-project sandbox overrides, see below |

### Trigger blocks

Only needed when one project departs from its tracker's workflow. Every field is optional and overrides the tracker's.

| Key | Description |
|-----|-------------|
| `project_resolution` | `{ strategy, value }`, the long form of `resolution:` |
| `pipeline_from_label`, `default_pipeline` | as on the tracker |
| `trigger_statuses`, `done_status`, `failed_status`, `needs_clarification_status`, `not_implementable_status` | as on the tracker |
| `in_progress_status` | the status a parked run's ticket returns to when the run resumes; outside `trigger_statuses` |
| `comment_keyword` | a keyword in a ticket comment that triggers |
| `pr_trigger_label` | GitHub and GitLab: a pull request label that asks for a review. `security-review` always does too |
| `assignee_name` | Jira: the webhook starts work when an issue is assigned to this user, default `Agent Smith` |
| `secret` | Jira: the webhook shared secret, see [Webhooks](webhooks.md) |

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

Refused: an unknown project, a repo that project doesn't carry, a wildcard repo, a `context_repo` this project doesn't carry, and a cycle. [Project templates](../../configure-it/templates.md) explains what reads them.

### projects.sandbox

| Key | Description |
|-----|-------------|
| `toolchain_image` | one image for the whole project, wins over everything else |
| `images` | language → image, overrides the built-in image per language |
| `resources` | cpu and memory requests and limits, all four or none |
| `step_timeout_seconds`, `run_command_timeout_seconds` | override the global timeouts |
| `agent_registry`, `agent_version` | override the sandbox agent image |
| `hold_seconds` | how long this project's design conversations hold their sandboxes between turns |
| `secrets` | `env` (`VAR: "secretName:key"`) and `files` (`mount`, `secret`, `key`): Kubernetes Secrets mounted into the sandbox |

Empty means inherit. [Sandbox architecture](../concepts/sandbox-architecture.md) has what each one does at runtime.

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

A global label → pipeline map, used when neither the project's trigger nor its tracker declares a `pipeline_from_label`. There's no studio screen for it; on a server, edit it through export and import.

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
| `orchestrator` | `max_run_wall_time_seconds` (1800) |
| `sandbox` | agent image, `step_timeout_seconds` (900), `run_command_timeout_seconds` (300), `max_concurrent_sandboxes`, `hold_seconds` (180), registry trust and pull secrets |
| `registries` | private package feeds the agent authenticates against in the sandbox |
| `primary_provider` | the agent used when a project names none |
| `limits` | per-skill ceilings of the agentic loop |
| `pipeline_cost_cap` | money and token caps per run, see [Pipeline cost cap](pipeline-cost-cap.md) |
| `queue` | `max_parallel_jobs` (4), `consume_block_seconds` (5), `shutdown_grace_seconds` (30), `redis_retry_interval_seconds` (30) |
| `dialogue` | `hot_wait_seconds` (600), `approval_timeout_seconds` (259200), `dashboard_url` |
| `skills` | an override for where the skill catalog comes from; normally unset |
| `pipeline_storage` | how long in-flight run artifacts stay in Redis |
| `pipeline_data_flow` | whether the data-flow gate warns or enforces |
| `trace` | `enabled`: record every model call's prompt and answer. `AGENTSMITH_TRACE`, when set, wins over it |
| `role_mapping` | what a role name means: `role_claim`, `group_claim`, `group_roles`, `roles`, `person_grants`, `observation_retention_days`. Edited on the Access page |
| `mcp_servers` | the studio's MCP server catalog: `transport`, `url`, `auth` |

## Bootstrap and file-only blocks

| Block | Description |
|-------|-------------|
| `persistence` | `provider` (`sqlite`, `postgresql`, `mysql`, `sqlserver`) and `connection_string`. Replaced by `AGENTSMITH_PERSISTENCE_PROVIDER` + `AGENTSMITH_PERSISTENCE_CONNECTION` when both are set |
| `auth` | the token authority dashboard sign-in validates against |
| `tool_runner` | how the api-scan tools run, see [Tool configuration](tools.md). Read from the file at start, never stored or imported |
