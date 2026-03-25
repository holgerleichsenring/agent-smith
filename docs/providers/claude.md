# Claude (Anthropic)

Claude is Agent Smith's default and most-tested provider. It offers the best tool calling accuracy and is the only provider with prompt caching support.

## Setup

**1. Get an API key** from [console.anthropic.com](https://console.anthropic.com/)

**2. Set the environment variable:**

```bash
export ANTHROPIC_API_KEY=sk-ant-...
```

**3. Configure `agentsmith.yml`:**

```yaml
projects:
  my-api:
    agent:
      type: Claude
      model: claude-sonnet-4-20250514

secrets:
  anthropic_api_key: ${ANTHROPIC_API_KEY}
```

## Recommended Model Routing

Use Haiku for cheap bulk tasks and Sonnet for the work that matters:

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514
  models:
    scout:
      model: claude-haiku-4-5-20251001       # Code analysis, file discovery
      max_tokens: 4096
    primary:
      model: claude-sonnet-4-20250514        # Agentic code execution
      max_tokens: 8192
    planning:
      model: claude-sonnet-4-20250514        # Plan generation
      max_tokens: 4096
    summarization:
      model: claude-haiku-4-5-20251001       # Context compaction
      max_tokens: 2048
    context_generation:
      model: claude-haiku-4-5-20251001       # Auto-generating context.yaml
      max_tokens: 3072
    code_map_generation:
      model: claude-haiku-4-5-20251001       # Auto-generating code-map.yaml
      max_tokens: 4096
```

## Prompt Caching

Anthropic's prompt caching stores the system prompt and early messages server-side, reducing costs by up to 90% on repeated calls within the same session.

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514
  cache:
    is_enabled: true         # Default: true
    strategy: automatic      # Only strategy currently supported
```

!!! info "How it works"
    Agent Smith automatically marks the system prompt and code context as cacheable. On the first call, Anthropic stores these blocks. Subsequent calls in the same agentic loop read from cache at `cache_read_per_million` pricing instead of full `input_per_million`.

### Cache Pricing Impact

| Model | Input | Cache Read | Savings |
|-------|-------|------------|---------|
| Sonnet 4 | $3.00/M | $0.30/M | 90% |
| Haiku 4.5 | $0.80/M | $0.08/M | 90% |

A typical bug fix runs 10-15 agentic iterations. After the first iteration, the system prompt (~5K tokens) is cached for all subsequent calls.

## Context Compaction

Long agentic loops can exceed the context window. Compaction summarizes older iterations to keep the conversation within limits:

```yaml
agent:
  type: Claude
  compaction:
    is_enabled: true
    threshold_iterations: 8       # Start compacting after 8 iterations
    max_context_tokens: 80000     # Target token budget
    keep_recent_iterations: 3     # Always keep last 3 iterations verbatim
    summary_model: claude-haiku-4-5-20251001  # Cheap model for summaries
```

## Retry Configuration

Claude returns `429` (rate limit) and `529` (overloaded) errors under load. Agent Smith retries automatically with exponential backoff:

```yaml
agent:
  type: Claude
  retry:
    max_retries: 5
    initial_delay_ms: 2000
    backoff_multiplier: 2.0
    max_delay_ms: 60000
```

!!! note
    The `529 Overloaded` status is specific to Anthropic and is included in the retry logic automatically.

## Pricing Configuration

```yaml
agent:
  pricing:
    models:
      claude-sonnet-4-20250514:
        input_per_million: 3.0
        output_per_million: 15.0
        cache_read_per_million: 0.30
      claude-haiku-4-5-20251001:
        input_per_million: 0.80
        output_per_million: 4.0
        cache_read_per_million: 0.08
```

## Typical Cost Per Run

| Pipeline | Model Mix | Approximate Cost |
|----------|-----------|-----------------|
| fix-bug (small) | Haiku scout + Sonnet primary | $0.05 - $0.15 |
| fix-bug (large) | Haiku scout + Sonnet primary | $0.20 - $0.50 |
| security-scan | Haiku all roles | $0.03 - $0.08 |
| api-scan | Haiku panel + Sonnet synthesis | $0.04 - $0.12 |
