# Connect your AI provider

Agent Smith calls the AI provider directly from your infrastructure — no SaaS in between, no proxy. Pick the one you have an API key for (or run Ollama for fully local).

Every provider config goes under `agents:` in `agentsmith.yml` and gets a catalog key you reference from `projects.X.agent`. You can have more than one agent registered (a Claude one and a local Ollama one for cost-sensitive runs, for example) and pick per project.

## Model roles

Agent Smith uses different models at different points in a run. Each agent block has a `models:` map with one entry per role:

| Role | What it does | Model size that makes sense |
|---|---|---|
| `scout` | Walks the codebase, picks relevant files. High call volume. | Cheap. `gpt-4.1-mini`, `claude-haiku`, `gemini-flash`. |
| `primary` | Writes the code, reviews, makes the actual changes. | The good one. `gpt-4.1`, `claude-sonnet`, `gemini-pro`. |
| `planning` | Generates the plan from the ticket + scout output. | Usually same as `primary`. |
| `summarization` | Context compaction when conversation gets long. | Cheap. `*-mini` / `*-haiku`. |
| `code_map_generation` | Builds a navigable code map (optional). | Cheap. |
| `context_generation` | Generates the per-repo `.agentsmith/context.yaml` during `init-project`. | Cheap. |

Roles you don't configure fall back to `primary`. If `primary` isn't set either, the agent block is invalid.

## Anthropic Claude

Direct Anthropic API. First-class support with prompt caching on by default.

```yaml
agents:
  default-claude:
    type: claude
    cache: { is_enabled: true, strategy: automatic }   # default — leave on
    retry: { max_retries: 5, initial_delay_ms: 2000, backoff_multiplier: 2.0 }
    compaction: { is_enabled: true, threshold_iterations: 8, keep_recent_iterations: 3 }
    models:
      scout:   { model: claude-haiku-4-5-20251001 }
      primary: { model: claude-sonnet-4-6 }
      planning:      { model: claude-sonnet-4-6 }
      summarization: { model: claude-haiku-4-5-20251001 }

secrets:
  claude_api_key: ${ANTHROPIC_API_KEY}
```

Prompt caching reuses the system prompt + tool definitions + coding principles between iterations of the same run. Typical cost saving on a `fix-bug` run is 40-60%.

## OpenAI

Direct OpenAI API. Supports the reasoning models (`o3`, `o4`, etc.) — set them under `primary` if you want them.

```yaml
agents:
  default-openai:
    type: openai
    retry: { max_retries: 5, initial_delay_ms: 2000, backoff_multiplier: 2.0 }
    models:
      scout:   { model: gpt-4.1-mini, max_tokens: 4096 }
      primary: { model: gpt-4.1,      max_tokens: 8192 }
      planning:      { model: gpt-4.1,      max_tokens: 4096 }
      summarization: { model: gpt-4.1-mini, max_tokens: 2048 }

secrets:
  openai_api_key: ${OPENAI_API_KEY}
```

## Azure OpenAI

OpenAI through your Azure subscription. Same models, different routing. Per-model `deployment` names are required (those map to your Azure deployment slots).

```yaml
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
    pricing:
      models:
        gpt-4.1:      { input_per_million: 2.0,  output_per_million: 8.0,  cache_read_per_million: 0.50 }
        gpt-4.1-mini: { input_per_million: 0.40, output_per_million: 1.60, cache_read_per_million: 0.10 }

secrets:
  azure_openai_api_key: ${AZURE_OPENAI_API_KEY}
```

The `pricing` block is optional but recommended — it lets Agent Smith report dollar cost per run. Without it, only token counts are tracked.

## Google Gemini

Direct Gemini API via Google AI Studio or Vertex AI.

```yaml
agents:
  default-gemini:
    type: gemini
    models:
      scout:   { model: gemini-2.5-flash }
      primary: { model: gemini-2.5-pro }
      planning:      { model: gemini-2.5-pro }
      summarization: { model: gemini-2.5-flash }

secrets:
  gemini_api_key: ${GEMINI_API_KEY}
```

## Ollama (local)

Local models running on your machine. No API key, no internet egress, no cloud cost.

```yaml
agents:
  local-ollama:
    type: ollama
    base_url: http://localhost:11434
    models:
      scout:   { model: llama3.3:8b }
      primary: { model: llama3.3:70b }
      planning:      { model: llama3.3:70b }
      summarization: { model: llama3.3:8b }
```

No secrets needed — Ollama is unauthenticated by default. Bring up the Ollama daemon (`ollama serve`) and pull the models you reference (`ollama pull llama3.3:70b`). For Docker / k8s hosts, point `base_url` at the Ollama service (e.g. `http://ollama.default.svc.cluster.local:11434`).

A 70B model on a 24GB GPU does the job. Smaller models (8B) work for scout / summarization but tend to produce shaky code on the primary role.

## OpenAI-compatible (Groq, LM Studio, vLLM, your own endpoint)

Any service that speaks the OpenAI Chat Completions API.

```yaml
agents:
  groq-default:
    type: openai_compatible
    base_url: https://api.groq.com/openai/v1
    models:
      scout:   { model: llama-3.3-70b-versatile }
      primary: { model: llama-3.3-70b-versatile }

secrets:
  groq_api_key: ${GROQ_API_KEY}
```

The same shape covers LM Studio (`http://localhost:1234/v1`), vLLM (`http://your-vllm-host:8000/v1`), and any other endpoint that returns OpenAI-shaped responses.

## Picking models per project

You don't have to pick one provider for everything. Two patterns work:

**One agent per environment.** Cheap models for dev, good models for prod:

```yaml
agents:
  cheap-ollama:   { type: ollama, base_url: http://localhost:11434, models: { primary: { model: llama3.3:70b } } }
  premium-claude: { type: claude, models: { primary: { model: claude-sonnet-4-6 } } }

projects:
  todolist-dev:    { agent: cheap-ollama,   tracker: acme-issues, repos: [todolist] }
  todolist-prod:   { agent: premium-claude, tracker: acme-issues, repos: [todolist] }
```

**Same agent, different model per role.** The default. Scout runs on the cheap model, primary on the good one — already shown in every example above.

## Cost transparency

Whichever provider you pick, every run records token usage and (if pricing is configured) dollar cost into `.agentsmith/runs/{run-id}/result.md`. Six months later you can answer "what did the auth refactor actually cost?" without guessing. See [Cost tracking](../reference/concepts/cost-tracking.md) in Reference for the detail of what gets recorded.

## Next

- [First run](../get-it-running/first-run.md) — once one provider is wired up, do a run.
- [Skills catalog](../how-it-works/skills-catalog.md) — the role definitions the agent uses come from a separately-versioned repo.
- [Host it](../host-it/docker-compose.md).
