# OpenAI-Compatible Endpoints

Any service that implements the OpenAI chat completions API works with Agent Smith. Set `type: OpenAI` and point `endpoint` to the service.

## How It Works

Agent Smith uses the `OpenAiCompatibleClient` for both native OpenAI and third-party endpoints. The same tool calling, agentic loop, and cost tracking apply -- you just change the URL and API key.

```yaml
agent:
  type: OpenAI                          # Always "OpenAI" for compatible endpoints
  model: <model-id>                     # Model name as the endpoint expects it
  endpoint: https://<provider>/v1       # Base URL (must end in /v1)
  api_key_secret: <SECRET_NAME>         # Key name from secrets section
```

## Supported Services

### Groq

Free tier available. Extremely fast inference.

```yaml
projects:
  my-api:
    agent:
      type: OpenAI
      model: llama-3.3-70b-versatile
      endpoint: https://api.groq.com/openai/v1
      api_key_secret: GROQ_API_KEY
      models:
        scout:
          model: llama-3.3-70b-versatile
          max_tokens: 4096
        primary:
          model: llama-3.3-70b-versatile
          max_tokens: 8192

    pricing:
      models:
        llama-3.3-70b-versatile:
          input_per_million: 0.0        # Free tier
          output_per_million: 0.0

secrets:
  groq_api_key: ${GROQ_API_KEY}
```

!!! tip "Scanning your own codebase for $0.00"
    Groq's free tier with Llama 3.3 70B is a viable option for security scans and code reviews on personal projects. Tool calling works, and the agentic loop runs identically to Claude -- just with rate limits on the free plan.

### Together AI

Wide model selection, competitive pricing.

```yaml
projects:
  my-api:
    agent:
      type: OpenAI
      model: meta-llama/Llama-3.3-70B-Instruct-Turbo
      endpoint: https://api.together.xyz/v1
      api_key_secret: TOGETHER_API_KEY
      models:
        primary:
          model: meta-llama/Llama-3.3-70B-Instruct-Turbo
          max_tokens: 8192
        scout:
          model: meta-llama/Llama-3.3-70B-Instruct-Turbo
          max_tokens: 4096

    pricing:
      models:
        meta-llama/Llama-3.3-70B-Instruct-Turbo:
          input_per_million: 0.88
          output_per_million: 0.88

secrets:
  together_api_key: ${TOGETHER_API_KEY}
```

### Fireworks AI

Optimized inference infrastructure.

```yaml
projects:
  my-api:
    agent:
      type: OpenAI
      model: accounts/fireworks/models/llama-v3p3-70b-instruct
      endpoint: https://api.fireworks.ai/inference/v1
      api_key_secret: FIREWORKS_API_KEY

secrets:
  fireworks_api_key: ${FIREWORKS_API_KEY}
```

### vLLM (Self-Hosted)

Run your own inference server with vLLM's OpenAI-compatible endpoint:

```yaml
projects:
  my-api:
    agent:
      type: OpenAI
      model: Qwen/Qwen2.5-Coder-32B-Instruct
      endpoint: http://gpu-server:8000/v1

    pricing:
      models:
        Qwen/Qwen2.5-Coder-32B-Instruct:
          input_per_million: 0.0
          output_per_million: 0.0
```

No API key needed for local vLLM. No secrets section required.

### LiteLLM (Proxy)

Use LiteLLM as a unified proxy to route between multiple providers:

```yaml
projects:
  my-api:
    agent:
      type: OpenAI
      model: claude-sonnet-4-20250514    # LiteLLM maps this to the real provider
      endpoint: http://litellm-proxy:4000/v1
      api_key_secret: LITELLM_API_KEY

secrets:
  litellm_api_key: ${LITELLM_API_KEY}
```

## Configuration Pattern

All OpenAI-compatible endpoints follow the same pattern:

```yaml
agent:
  type: OpenAI              # (1)
  model: <model-id>         # (2)
  endpoint: <base-url>/v1   # (3)
  api_key_secret: <KEY>     # (4)
```

1. Always `OpenAI` -- this selects the OpenAI-compatible client
2. Model ID as the provider expects it (varies by service)
3. Must include `/v1` -- Agent Smith appends `/chat/completions`
4. References a key in the `secrets` section; omit for keyless endpoints

## Feature Support

| Feature | Status | Notes |
|---------|--------|-------|
| Tool calling | Yes | Requires the model/endpoint to support OpenAI tool format |
| Agentic loop | Yes | Same loop as native OpenAI |
| Multi-model routing | Yes | All models must be on the same endpoint |
| Prompt caching | No | Anthropic-specific feature |
| Context compaction | Yes | Uses the configured summarization model |
| Cost tracking | Yes | Configure pricing manually per model |
| Retry/backoff | Yes | Same retry config as all providers |

!!! warning
    Not all models on all endpoints support tool calling reliably. Test with a simple task before running large pipelines. Models that do not return well-formed tool calls will cause the agentic loop to fail.
