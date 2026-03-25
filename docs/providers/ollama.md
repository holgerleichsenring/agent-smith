# Ollama (Local Models)

Run Agent Smith entirely on your own hardware with zero API costs. Ollama serves open-source models locally through an OpenAI-compatible API.

## Setup

**1. Install and start Ollama:**

```bash
# macOS / Linux
curl -fsSL https://ollama.com/install.sh | sh
ollama serve
```

Or via Docker:

```bash
docker run -d -p 11434:11434 --name ollama ollama/ollama
```

**2. Pull a model:**

```bash
ollama pull qwen2.5-coder:32b
```

**3. Configure `agentsmith.yml`:**

```yaml
projects:
  my-api:
    agent:
      type: Ollama
      model: qwen2.5-coder:32b
      endpoint: http://localhost:11434    # Default, can be omitted
```

No API key required. No secrets section needed.

## Recommended Models

| Model | Size | Tool Calling | Best For |
|-------|------|:------------:|----------|
| `qwen2.5-coder:32b` | 18 GB | Yes | Code generation, best local coding model |
| `qwen2.5-coder:7b` | 4.4 GB | Yes | Fast coding on modest hardware |
| `llama3.3:70b` | 40 GB | Yes | General-purpose, strong reasoning |
| `mistral-small:24b` | 14 GB | Yes | Good balance of speed and quality |
| `deepseek-r1:32b` | 18 GB | No | Reasoning-heavy tasks (no tool calling) |
| `deepseek-r1:7b` | 4.4 GB | No | Lightweight reasoning |

## Tool Calling Auto-Detection

Agent Smith automatically tests whether a model supports native tool calling at startup:

```
[INF] Connected to Ollama 0.6.2 at http://localhost:11434
[INF] Model qwen2.5-coder:32b: tool_calling=True
```

- **Native tools**: The model receives tool definitions in OpenAI format and returns structured tool calls. Full agentic loop with file operations, search, and code editing.
- **Structured text fallback**: Models without tool calling receive instructions to output structured text. The agent parses the response to extract actions.

!!! warning
    Models without native tool calling (e.g., `deepseek-r1`) have limited agentic capability. They work for plan generation and analysis but cannot reliably execute multi-step code changes.

## Model Routing

Mix model sizes for cost vs. quality:

```yaml
agent:
  type: Ollama
  model: qwen2.5-coder:32b
  models:
    scout:
      model: qwen2.5-coder:7b       # Fast, lightweight for file discovery
      max_tokens: 4096
    primary:
      model: qwen2.5-coder:32b      # Full power for code execution
      max_tokens: 8192
    planning:
      model: qwen2.5-coder:32b
      max_tokens: 4096
    summarization:
      model: qwen2.5-coder:7b       # Small model for compaction
      max_tokens: 2048
```

## Hybrid Cloud + Local

Use Ollama for cheap scouting and cloud for execution:

```yaml
projects:
  my-api:
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
      models:
        scout:
          model: qwen2.5-coder:7b     # Local, free
          max_tokens: 4096
        primary:
          model: claude-sonnet-4-20250514  # Cloud, high quality
          max_tokens: 8192
        planning:
          model: claude-sonnet-4-20250514
          max_tokens: 4096
        summarization:
          model: qwen2.5-coder:7b     # Local, free
          max_tokens: 2048
```

!!! note
    Hybrid routing requires both Ollama running locally and a cloud API key configured. The provider type determines the primary execution path -- model routing within the `models` block can reference any available model.

## Pricing

Ollama models run on your hardware at zero token cost:

```yaml
agent:
  pricing:
    models:
      qwen2.5-coder:32b:
        input_per_million: 0.0
        output_per_million: 0.0
      qwen2.5-coder:7b:
        input_per_million: 0.0
        output_per_million: 0.0
```

Cost tracking still works -- it will show $0.00 for local models, which is useful when mixing local and cloud models to see where money is actually spent.

## Hardware Requirements

| Model Size | RAM Required | GPU VRAM | Notes |
|-----------|-------------|----------|-------|
| 7B | 8 GB | 6 GB | Runs on most machines |
| 14-24B | 16 GB | 12 GB | Good laptop GPU |
| 32B | 24 GB | 20 GB | Desktop GPU (RTX 4090) |
| 70B | 48 GB | 40 GB | Dual GPU or CPU-only (slow) |

!!! tip
    Ollama automatically uses GPU acceleration when available. For CPU-only setups, expect 5-10x slower inference. The 7B models are still usable on CPU; 32B+ models need a GPU for practical agentic loop speeds.

## Troubleshooting

**"Cannot connect to Ollama"** -- Ensure Ollama is running:

```bash
ollama serve        # or: docker start ollama
curl http://localhost:11434/api/version
```

**"Model not found"** -- Pull the model first:

```bash
ollama pull qwen2.5-coder:32b
ollama list         # Verify it's available
```

**Slow inference** -- Check GPU utilization. If Ollama is using CPU:

```bash
ollama ps           # Shows running models and GPU usage
```
