# agentsmith.yml Reference

Complete reference for the main configuration file.

## Full Annotated Example

```yaml
# ─── Projects ────────────────────────────────────────────────────────
projects:
  my-api:                           # Project key (used in CLI: --project my-api)
    source:
      type: GitHub                  # GitHub | AzureRepos | GitLab | Local
      url: https://github.com/owner/repo
      auth: token                   # Auth method (resolved from secrets)
      # default_branch: main        # PR target branch (auto-detected if omitted)

    tickets:
      type: GitHub                  # GitHub | AzureDevOps | Jira | GitLab
      url: https://github.com/owner/repo
      auth: token
      # open_states: ["New", "Active"]  # States considered "open" (ADO whitelist)
      # done_status: "Closed"           # Target state when closing
      # close_transition_name: "Close"  # Jira transition name for closing
      # extra_fields: []                # Additional ADO fields to fetch
      # Azure DevOps only:
      # organization: my-org
      # project: my-project

    agent:
      type: Claude                  # Claude | OpenAI | Gemini | Ollama
      model: claude-sonnet-4-20250514

      retry:
        max_retries: 5
        initial_delay_ms: 2000
        backoff_multiplier: 2.0
        max_delay_ms: 60000

      cache:                        # Anthropic prompt caching (Claude only)
        is_enabled: true
        strategy: automatic

      compaction:                   # Context window management — see docs/concepts/context-compaction.md
        is_enabled: true
        threshold_iterations: 8     # Fire when iterations >= N (boolean OR with max_context_tokens)
        max_context_tokens: 80000   # Fire when estimated tokens >= N (boolean OR with threshold_iterations)
        keep_recent_iterations: 3   # Claude compactor knob; OpenAi compactor keeps 2 complete tool-call rounds
        summary_model: claude-haiku-4-5-20251001  # Claude compactor — summarizer model
        deployment_name: gpt-4o-mini-deployment   # OpenAI/Azure compactor — summarizer deployment override (cheaper than primary)
        # Provider availability:
        #   claude        ✓  ClaudeContextCompactor (p0008)
        #   openai        ✓  OpenAiContextCompactor (p0114)
        #   azure-openai  ✓  OpenAiContextCompactor (p0114)
        #   gemini        ✗  NoOp placeholder — same long-loop cost; follow-up phase
        #   ollama        ✗  NoOp placeholder — same long-loop cost; follow-up phase

      models:                       # Multi-model routing (optional)
        scout:
          model: claude-haiku-4-5-20251001
          max_tokens: 4096
        primary:
          model: claude-sonnet-4-20250514
          max_tokens: 8192
        planning:
          model: claude-sonnet-4-20250514
          max_tokens: 4096
        summarization:
          model: claude-haiku-4-5-20251001
          max_tokens: 2048

      pricing:                      # USD per million tokens
        models:
          claude-sonnet-4-20250514:
            input_per_million: 3.0
            output_per_million: 15.0
            cache_read_per_million: 0.30
          claude-haiku-4-5-20251001:
            input_per_million: 0.80
            output_per_million: 4.0
            cache_read_per_million: 0.08

      parallelism:                  # Fan-out for same-stage skill rounds
        max_concurrent_skill_rounds: 1   # Default 1 = sequential; raise to 4 for api-scan/security-scan

    pipeline: fix-bug               # Default pipeline for this project
    skills_path: skills/coding      # Relative to config/ directory
    coding_principles_path: .agentsmith/coding-principles.md

    # ─── Trigger config ────────────────────────────────────────────
    # One section per platform; pick the one matching tickets.type.
    # All four shapes are identical (Jira adds assignee_name).
    github_trigger:
      pipeline_from_label:
        agent-smith: fix-bug
        security-review: security-scan
      default_pipeline: fix-bug
      trigger_statuses: []          # empty = all states allowed
      done_status: "In Review"      # post-PR transition

    # gitlab_trigger:    # same shape
    # azuredevops_trigger:  # same shape (uses tags instead of labels)
    # jira_trigger:
    #   assignee_name: "Agent Smith"      # required for Jira gating
    #   pipeline_from_label: { ... }
    #   ...

    # ─── Polling (alternative ingress to webhooks) ─────────────────
    polling:
      enabled: false                # default: webhook-only
      interval_seconds: 60
      jitter_percent: 10            # ±% applied to the interval

# ─── Process-wide queue (consumer + receiver) ──────────────────────
agent:
  queue:
    max_parallel_jobs: 4            # SemaphoreSlim cap on PipelineQueueConsumer
    consume_block_seconds: 5        # LPOP poll interval
    shutdown_grace_seconds: 30      # in-flight grace on SIGTERM
    redis_retry_interval_seconds: 30 # subsystems poll IConnectionMultiplexer.IsConnected
                                     # at this cadence when Redis is configured but
                                     # unreachable; once connected they start their work

# ─── Pipelines ───────────────────────────────────────────────────────
pipelines:
  fix-bug:
    commands:
      - FetchTicketCommand
      - CheckoutSourceCommand
      - BootstrapProjectCommand
      - LoadCodeMapCommand
      - LoadCodingPrinciplesCommand
      - LoadContextCommand
      - AnalyzeCodeCommand
      - GeneratePlanCommand
      - ApprovalCommand
      - AgenticExecuteCommand
      - TestCommand
      - WriteRunResultCommand
      - CommitAndPRCommand

# ─── Tool Runner ─────────────────────────────────────────────────────
tool_runner:
  type: auto                        # auto | docker | podman | process
  # socket: unix:///var/run/docker.sock
  images:
    nuclei: projectdiscovery/nuclei:latest
    spectral: stoplight/spectral:6

# ─── Secrets ─────────────────────────────────────────────────────────
secrets:
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
  # openai_api_key: ${OPENAI_API_KEY}
  # gemini_api_key: ${GEMINI_API_KEY}
  # azure_devops_token: ${AZURE_DEVOPS_TOKEN}
```

## Section Reference

### projects

Each key under `projects` defines a project. Use `--project <key>` on the CLI to select which project to run.

| Field | Required | Description |
|-------|----------|-------------|
| `source.type` | Yes | Source provider: `GitHub`, `AzureRepos`, `GitLab`, `Local` |
| `source.url` | Yes* | Repository URL (*not required for `Local`) |
| `source.path` | No | Local path (for `Local` type) |
| `source.auth` | Yes | Auth method: `token` |
| `source.default_branch` | No | PR target branch. If omitted, read from remote API (cached per run); last resort `main` |
| `tickets.type` | Yes | Ticket provider: `GitHub`, `AzureDevOps`, `Jira`, `GitLab` |
| `tickets.url` | Yes | Ticket system URL |
| `tickets.organization` | No | Azure DevOps organization name |
| `tickets.project` | No | Azure DevOps project name |
| `tickets.open_states` | No | Whitelist of states considered "open" for `ListOpenAsync` (ADO only, default: `New`, `Active`, `Committed`) |
| `tickets.done_status` | No | Target state when closing a ticket (default: `Closed` for ADO, `Done` for Jira) |
| `tickets.close_transition_name` | No | Jira only: transition name for closing (default: `Close`) |
| `tickets.extra_fields` | No | Additional fields to fetch from work items (ADO only, e.g. custom fields). Missing fields map to null |
| `pipeline` | Legacy* | Single-pipeline form: pipeline name. Translated by the loader into a single-element `pipelines:` list with `default_pipeline = <name>`. *Use `pipelines:` for new configs. |
| `pipelines` | Yes** | Multi-pipeline form (p0106): list of `{ name, agent?, skills_path?, coding_principles_path? }`. Each entry's optional fields override the project-level value. **Required if `pipeline:` is not set. |
| `default_pipeline` | No | Pipeline name to use when CLI / fallback paths omit an explicit choice. Required when `pipelines:` has more than one entry; auto-set from `pipeline:` for legacy configs. |
| `skills_path` | No | Project-level skills directory. Optional — `pipelines[].skills_path` overrides; otherwise the per-pipeline default applies (`security-scan` → `skills/security`, etc.). |
| `coding_principles_path` | No | Path to coding conventions file (overridable per pipeline). |

#### Multiple pipelines per project (p0106)

A project that runs more than one pipeline (e.g. both `fix-bug` and `security-scan` on the same repo) declares them under `pipelines:`. Per-pipeline overrides shadow the project-level defaults; missing fields inherit:

```yaml
projects:
  my-project:
    source: { type: GitHub, url: ..., auth: token }
    tickets: { type: GitHub, url: ..., auth: token }
    agent: { type: Claude, model: claude-sonnet-4-20250514 }
    pipelines:
      - name: fix-bug                       # uses project agent, skills/coding default
      - name: security-scan                 # uses project agent, skills/security default
        skills_path: skills/my-custom-security
      - name: api-security-scan
        agent: { type: OpenAI, model: gpt-4.1 }   # different model just for this pipeline
    default_pipeline: fix-bug               # picked by CLI when --pipeline is omitted
```

**Skills-path resolution chain:** `pipelines[].skills_path` → preset default for the pipeline name (`fix-bug` → `skills/coding`, `security-scan` → `skills/security`, `api-security-scan` → `skills/api-security`, `legal-analysis` → `skills/legal`, `mad-discussion` → `skills/mad`) → `skills/coding`.

**Pipeline-name selection chain (CLI / fallback):** explicit `--pipeline` flag → `default_pipeline` → single-element shortcut (only when `pipelines:` has exactly one entry) → error listing declared pipelines.

The legacy `pipeline: <name>` single-string form continues to work — the loader synthesizes `pipelines: [{ name: <name> }]` and `default_pipeline: <name>` automatically. Trigger references (`pipeline_from_label`, `default_pipeline` in trigger blocks) are validated at load time and fail loud on unknown pipeline names.

### agent

See [AI Providers](../providers/index.md) for provider-specific configuration.

| Field | Required | Default | Description |
|-------|----------|---------|-------------|
| `type` | Yes | `claude` | Provider: `Claude`, `OpenAI`, `Gemini`, `Ollama` |
| `model` | Yes | -- | Default model identifier |
| `endpoint` | No | -- | Custom API endpoint (Ollama, OpenAI-compatible) |
| `api_key_secret` | No | -- | Override which secret holds the API key |

### agent.models

Route different task types to different models for cost optimization.

| Task | Used For | Typical Model |
|------|----------|---------------|
| `scout` | Code analysis, file discovery | Small/fast (Haiku, GPT-4.1-mini) |
| `primary` | Agentic code execution | Large/capable (Sonnet, GPT-4.1) |
| `planning` | Plan generation | Large/capable |
| `summarization` | Context compaction | Small/fast |
| `context_generation` | Auto-generating context.yaml | Small/fast |
| `code_map_generation` | Auto-generating code-map.yaml | Small/fast |
| `reasoning` | Extended thinking (optional) | Reasoning model |

Each assignment has:

```yaml
model: model-id
max_tokens: 8192
```

### secrets

Secrets use `${ENV_VAR}` syntax to reference environment variables. Agent Smith resolves them at startup.

```yaml
secrets:
  github_token: ${GITHUB_TOKEN}
  anthropic_api_key: ${ANTHROPIC_API_KEY}
```

!!! warning
    Never commit actual API keys to `agentsmith.yml`. Always use `${ENV_VAR}` references and set the variables in your environment, CI/CD pipeline, or Kubernetes secrets.

### agent.pricing

Pricing is configured per model in USD per million tokens. This drives the cost tracking displayed in run results.

```yaml
pricing:
  models:
    claude-sonnet-4-20250514:
      input_per_million: 3.0
      output_per_million: 15.0
      cache_read_per_million: 0.30   # Claude-specific
```

!!! note
    `cache_read_per_million` only applies to Anthropic models with prompt caching enabled. Omit it for other providers.

### Trigger sections (`github_trigger`, `gitlab_trigger`, `azuredevops_trigger`, `jira_trigger`)

Per-project trigger configuration. Both webhooks and polling read this. The shape is shared across platforms; Jira extends it with `assignee_name` and `comment_keyword`.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `pipeline_from_label` | map | `{}` | Trigger label → pipeline name, matched in config order |
| `default_pipeline` | string | `fix-bug` | Used when no label entry matches |
| `trigger_statuses` | list | `[]` | Allowed ticket states (empty = all) |
| `done_status` | string | `"In Review"` | Status set after PR creation |
| `comment_keyword` | string? | none | Optional keyword that re-triggers on comment |
| `assignee_name` | string | — | **Jira only**: required for assignee-based gating |

See [Label-Based Triggers](../setup/label-triggers.md) for per-platform examples and matching rules.

### `polling` (per project)

Opt-in alternative to webhooks. When enabled, Agent Smith pulls eligible tickets on an interval and routes them through the same `TicketClaimService` as webhooks.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `enabled` | bool | `false` | Whether to poll this project |
| `interval_seconds` | int | `60` | Base sleep between poll cycles |
| `jitter_percent` | int | `10` | Random ±% applied to the interval (avoids thundering herd) |

All four platforms support polling. Each provider implements `ITicketProvider.ListByLifecycleStatusAsync(Pending)` natively (GitHub via Issues+labels, GitLab via Issues+labels, Azure DevOps via WIQL on `[System.Tags]`, Jira via JQL search). Jira is label-mode only — native-status-mode polling is deferred. Set `tickets.project` for Jira if your instance hosts multiple projects so the JQL is scoped.

`pipeline_from_label` is honored on the polling path as of p0099a — same first-match semantics as webhooks; lifecycle labels (`agent-smith:*`) are filtered before matching.

See [Polling Setup](../setup/polling.md) for per-platform listing details and required token scopes, and [Polling vs Webhooks](../setup/polling-vs-webhooks.md) for the decision matrix.

### `agent.queue` (root-level)

Process-wide queue settings. The queue is shared across all projects on a given pod; one `agentsmith:queue:jobs` Redis list backs the entire deployment.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `max_parallel_jobs` | int | `4` | `SemaphoreSlim` cap on concurrent pipelines per pod (backpressure knob) |
| `redis_retry_interval_seconds` | int | `30` | How often subsystems re-check `IConnectionMultiplexer.IsConnected` while Redis is configured but unreachable. Lower = faster recovery, more polling noise. (p0101) |
| `consume_block_seconds` | int | `5` | LPOP polling interval inside the consumer loop |
| `shutdown_grace_seconds` | int | `30` | Time to await in-flight pipelines on graceful shutdown |

`max_parallel_jobs` is the only knob that throttles pipeline concurrency. Webhook receivers never block on pipeline execution — they only enqueue, which is O(ms). Increase if your AI provider has headroom; decrease if you're hitting rate limits.

### tool_runner

Controls how security scanning tools (Nuclei, Spectral) are executed.

| Field | Default | Description |
|-------|---------|-------------|
| `type` | `auto` | `auto` detects Docker socket, falls back to process |
| `socket` | -- | Custom Docker/Podman socket path |
| `docker_hostname` | `host.docker.internal` | Hostname used to reach the host from inside a container. Change for Podman (`host.containers.internal`) or custom networking |
| `images.nuclei` | `projectdiscovery/nuclei:latest` | Nuclei container image |
| `images.spectral` | `stoplight/spectral:6` | Spectral container image |
