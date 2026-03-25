# OpenAI (GPT-4)

Agent Smith supports OpenAI models with the same agentic loop and tool calling as Claude. GPT-4.1 is the recommended model.

## Setup

**1. Get an API key** from [platform.openai.com](https://platform.openai.com/)

**2. Set the environment variable:**

```bash
export OPENAI_API_KEY=sk-...
```

**3. Configure `agentsmith.yml`:**

```yaml
projects:
  my-api:
    agent:
      type: OpenAI
      model: gpt-4.1

secrets:
  openai_api_key: ${OPENAI_API_KEY}
```

## Model Routing

Use GPT-4.1-mini for lightweight tasks:

```yaml
agent:
  type: OpenAI
  model: gpt-4.1
  models:
    scout:
      model: gpt-4.1-mini
      max_tokens: 4096
    primary:
      model: gpt-4.1
      max_tokens: 8192
    planning:
      model: gpt-4.1
      max_tokens: 4096
    summarization:
      model: gpt-4.1-mini
      max_tokens: 2048
```

## Retry Configuration

OpenAI returns `429` on rate limits. The retry config works identically to Claude:

```yaml
agent:
  type: OpenAI
  retry:
    max_retries: 5
    initial_delay_ms: 2000
    backoff_multiplier: 2.0
    max_delay_ms: 60000
```

## Pricing Configuration

```yaml
agent:
  pricing:
    models:
      gpt-4.1:
        input_per_million: 2.0
        output_per_million: 8.0
      gpt-4.1-mini:
        input_per_million: 0.40
        output_per_million: 1.60
```

## Custom Endpoint

To use an OpenAI-compatible proxy or Azure OpenAI, set `endpoint`:

```yaml
agent:
  type: OpenAI
  model: gpt-4.1
  endpoint: https://my-proxy.example.com/v1
  api_key_secret: MY_PROXY_KEY    # Resolves from secrets section
```

!!! tip
    For third-party OpenAI-compatible endpoints (Groq, Together AI, vLLM, etc.), see the [OpenAI-Compatible](openai-compatible.md) page.

## Feature Comparison with Claude

| Feature | OpenAI | Claude |
|---------|--------|--------|
| Tool calling | Yes | Yes |
| Agentic loop | Yes | Yes |
| Multi-model routing | Yes | Yes |
| Prompt caching | No | Yes |
| Context compaction | Yes | Yes |
| Cost tracking | Yes | Yes |

The main trade-off: OpenAI does not support prompt caching, so repeated system prompts are billed at full input price on every call. For long agentic loops, Claude with caching enabled is significantly cheaper.
