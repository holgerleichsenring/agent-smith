# AI Providers

Agent Smith supports multiple AI providers through a unified interface. Every provider uses the same agentic loop, tool calling, and cost tracking -- switch providers by changing `agent.type` in your config.

## Supported Providers

| Provider | `type` value | Tool Calling | Prompt Caching | Pricing |
|----------|-------------|:------------:|:--------------:|---------|
| **[Claude](claude.md)** (Anthropic) | `claude` | Native | Yes | $3-15/M tokens |
| **[OpenAI](openai.md)** (GPT-4) | `openai` | Native | No | $2-8/M tokens |
| **[Gemini](gemini.md)** (Google) | `gemini` | Native | No | $0.15-10/M tokens |
| **[Ollama](ollama.md)** (Local) | `ollama` | Auto-detected | No | Free |
| **[OpenAI-Compatible](openai-compatible.md)** | `openai` | Native | No | Varies |

## Quick Comparison

=== "Best Quality"

    **Claude Sonnet 4** -- Best tool calling accuracy, prompt caching reduces repeat costs by 90%.

    ```yaml
    agent:
      type: Claude
      model: claude-sonnet-4-20250514
    ```

=== "Best Value"

    **Gemini 2.5 Flash** -- Cheapest cloud option at $0.15/M input tokens.

    ```yaml
    agent:
      type: Gemini
      model: gemini-2.5-flash
    ```

=== "Zero Cost"

    **Ollama + qwen2.5-coder** -- Runs entirely on your hardware.

    ```yaml
    agent:
      type: Ollama
      model: qwen2.5-coder:32b
    ```

=== "Free Cloud"

    **Groq + Llama 3.3** -- Free tier with rate limits.

    ```yaml
    agent:
      type: OpenAI
      model: llama-3.3-70b-versatile
      endpoint: https://api.groq.com/openai/v1
      api_key_secret: GROQ_API_KEY
    ```

## How Provider Selection Works

1. Agent Smith reads `agent.type` from your project config
2. The `AgentProviderFactory` creates the matching provider
3. All providers implement the same `IAgentProvider` interface
4. Tool calling, plan generation, and agentic execution work identically across providers

The only provider-specific features are:

- **Prompt caching** -- Claude only
- **Context compaction model** -- can be any provider's model
- **Tool calling auto-detection** -- Ollama only (tests the model at startup)

## Multi-Model Routing

All providers support routing different tasks to different models:

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514
  models:
    scout: { model: claude-haiku-4-5-20251001, max_tokens: 4096 }
    primary: { model: claude-sonnet-4-20250514, max_tokens: 8192 }
    planning: { model: claude-sonnet-4-20250514, max_tokens: 4096 }
    summarization: { model: claude-haiku-4-5-20251001, max_tokens: 2048 }
```

This works with every provider -- use cheaper models for scout/summarization and capable models for planning/execution.
