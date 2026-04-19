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

      compaction:                   # Context window management
        is_enabled: true
        threshold_iterations: 8     # Compact after N agentic loop iterations
        max_context_tokens: 80000   # Target token limit
        keep_recent_iterations: 3   # Preserve last N iterations verbatim
        summary_model: claude-haiku-4-5-20251001

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

    pipeline: fix-bug               # Default pipeline for this project
    skills_path: skills/coding      # Relative to config/ directory
    coding_principles_path: .agentsmith/coding-principles.md

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
| `pipeline` | Yes | Default pipeline name |
| `skills_path` | No | Path to skill definitions (default: `skills/coding`) |
| `coding_principles_path` | No | Path to coding conventions file |

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

### tool_runner

Controls how security scanning tools (Nuclei, Spectral) are executed.

| Field | Default | Description |
|-------|---------|-------------|
| `type` | `auto` | `auto` detects Docker socket, falls back to process |
| `socket` | -- | Custom Docker/Podman socket path |
| `docker_hostname` | `host.docker.internal` | Hostname used to reach the host from inside a container. Change for Podman (`host.containers.internal`) or custom networking |
| `images.nuclei` | `projectdiscovery/nuclei:latest` | Nuclei container image |
| `images.spectral` | `stoplight/spectral:6` | Spectral container image |
