# Gemini (Google)

Google Gemini offers the cheapest cloud pricing of any supported provider. Gemini 2.5 Flash is a strong choice for high-volume, cost-sensitive workloads.

## Setup

**1. Get an API key** from [aistudio.google.com](https://aistudio.google.com/)

**2. Set the environment variable:**

```bash
export GEMINI_API_KEY=AI...
```

**3. Configure `agentsmith.yml`:**

```yaml
projects:
  my-api:
    agent:
      type: Gemini
      model: gemini-2.5-flash

secrets:
  gemini_api_key: ${GEMINI_API_KEY}
```

## Model Routing

Use Flash for most tasks and Pro for complex execution:

```yaml
agent:
  type: Gemini
  model: gemini-2.5-flash
  models:
    scout:
      model: gemini-2.5-flash
      max_tokens: 4096
    primary:
      model: gemini-2.5-pro
      max_tokens: 8192
    planning:
      model: gemini-2.5-pro
      max_tokens: 4096
    summarization:
      model: gemini-2.5-flash
      max_tokens: 2048
```

## Pricing Configuration

```yaml
agent:
  pricing:
    models:
      gemini-2.5-pro:
        input_per_million: 1.25
        output_per_million: 10.0
      gemini-2.5-flash:
        input_per_million: 0.15
        output_per_million: 0.60
```

## Cost Comparison

Gemini Flash is roughly 20x cheaper than Claude Sonnet on input tokens:

| Model | Input/M | Output/M | Relative Cost |
|-------|---------|----------|---------------|
| Gemini 2.5 Flash | $0.15 | $0.60 | 1x |
| Gemini 2.5 Pro | $1.25 | $10.00 | 8x |
| Claude Sonnet 4 | $3.00 | $15.00 | 20x |
| GPT-4.1 | $2.00 | $8.00 | 13x |

!!! tip
    For security scans and multi-role discussions where many roles each make short calls, Gemini Flash keeps costs near zero while still providing useful analysis.

## Feature Comparison

| Feature | Gemini | Claude | OpenAI |
|---------|--------|--------|--------|
| Tool calling | Yes | Yes | Yes |
| Agentic loop | Yes | Yes | Yes |
| Multi-model routing | Yes | Yes | Yes |
| Prompt caching | No | Yes | No |
| Context compaction | Yes | Yes | Yes |
| Cost tracking | Yes | Yes | Yes |

## When to Use Gemini

- **High-volume scanning** -- Run security scans or code reviews on many repos cheaply
- **Scout/summarization tasks** -- Flash is fast and cheap for file discovery and context compaction
- **Budget-constrained teams** -- Get working AI-assisted development at a fraction of Claude/GPT-4 pricing
- **Hybrid routing** -- Use Flash for scout + summarization, Claude Sonnet for primary execution
