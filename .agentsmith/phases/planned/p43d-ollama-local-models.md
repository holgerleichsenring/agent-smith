# Phase 43d: Local Model Support (Ollama) & Hybrid Routing

## Goal

Support locally-hosted LLMs via Ollama as an `IAgentProvider`. Extend
`ModelAssignment` to support per-task provider routing between cloud and
local models. Can be built in parallel with p43b/p43c after p43a.

---

## Why Ollama

- Docker-native: `docker run ollama/ollama`
- OpenAI-compatible API at `http://{host}:11434/v1`
- Models: Qwen2.5-Coder 32B, DeepSeek-R1 32B, Llama 3.3 70B, Mistral Small 3.1
- Free, MIT licensed, no API costs

---

## ModelAssignment Extension (Backward Compatible)

```csharp
// Extend existing record
public sealed record ModelAssignment(
    string Model,
    int MaxTokens,
    string? ProviderType = null,   // null = existing default provider
    string? Endpoint = null);      // null = cloud API
```

Existing configs without `ProviderType` work unchanged. `AgentProviderFactory`
checks `ProviderType` and instantiates accordingly.

### Config

```yaml
model_registry:
  triage:
    type: Ollama
    model: mistral-small:3.1
    endpoint: http://ollama:11434
  planning:
    type: Claude
    model: claude-sonnet-4-20250514
  execution:
    type: Ollama
    model: qwen2.5-coder:32b
    endpoint: http://ollama:11434
  security_scan:
    type: Claude
    model: claude-opus-4-20250514
  writeback:
    type: Ollama
    model: mistral-small:3.1
    endpoint: http://ollama:11434
```

Simple single-provider config (backward compatible):

```yaml
agent:
  type: claude
  model: claude-sonnet-4-20250514
```

---

## OpenAiCompatibleClient — Shared HTTP Logic

```csharp
// src/AgentSmith.Infrastructure/Services/Providers/Agent/OpenAiCompatibleClient.cs
// internal, only visible within Infrastructure
internal sealed class OpenAiCompatibleClient(
    string endpoint,
    string? apiKey,
    ILogger logger)
{
    Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
}
```

`OpenAiAgentProvider` and `OllamaAgentProvider` receive it via constructor.
Composition, not inheritance.

---

## OllamaAgentProvider : IAgentProvider

```csharp
// Config
public sealed record OllamaConfig(
    string Endpoint,      // default: http://localhost:11434
    string Model,         // e.g. qwen2.5-coder:32b
    bool HasToolCalling); // auto-detected on startup
```

```yaml
agent:
  type: Ollama
  model: qwen2.5-coder:32b
  endpoint: http://ollama-server:11434
```

### Tool Calling Capability

Capability check on startup via `GET {endpoint}/api/show`:

- If `tool_calling: true` → use native tool calling (same as OpenAI)
- If `tool_calling: false` → structured text fallback:
  - Prompt template switches to JSON output mode
  - Agent returns JSON action list instead of tool calls
  - `OllamaAgentProvider` parses JSON and simulates tool-call sequence internally

Transparent for the rest of the pipeline — `IAgentProvider` contract unchanged.

---

## Cost Tracking

Local models have no API cost:
- Ollama tasks: `$0.00 (local)`
- Optional `cost_per_1k_tokens` in `OllamaConfig` for internal chargeback
- Summary: `Total: $0.12 (cloud) + $0.00 (local, 3 tasks)`

`RunCostSummary` / `PhaseCost` handle zero-cost entries.

---

## Docker Compose: Ollama Sidecar

Optional profile in `docker-compose.yml`:

```yaml
services:
  ollama:
    image: ollama/ollama
    profiles: [local-models]
    volumes:
      - ollama-data:/root/.ollama
    ports:
      - "11434:11434"
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
```

Start: `docker compose --profile local-models up`
Pull model: `docker exec ollama ollama pull qwen2.5-coder:32b`

---

## Startup Validation

On startup, if any task in `ModelRegistry` uses `Ollama`:

1. Ping `{endpoint}/api/version` — fail fast with clear error if unreachable
2. Check model availability via `/api/show` — warn if not pulled, suggest command
3. Detect tool calling capability — log which models use native vs. fallback

---

## AgentProviderFactory Extension

```csharp
// In existing switch
"ollama" => CreateOllama(config, assignment),
```

When `ModelAssignment.ProviderType` is set, it overrides `AgentConfig.Type`
for that specific task. This enables hybrid routing.

---

## Files to Create

- `src/AgentSmith.Infrastructure/Services/Providers/Agent/OllamaAgentProvider.cs`
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/OpenAiCompatibleClient.cs`
- Tests: OllamaAgentProvider (mocked HTTP), ModelRegistry hybrid routing

## Files to Modify

- `src/AgentSmith.Contracts/Models/Configuration/ModelRegistryConfig.cs` — extend ModelAssignment
- `src/AgentSmith.Infrastructure/Services/Factories/AgentProviderFactory.cs` — add Ollama case
- `src/AgentSmith.Infrastructure/Services/Providers/Agent/OpenAiAgentProvider.cs` — use OpenAiCompatibleClient
- `docker-compose.yml` — add Ollama sidecar with `local-models` profile

---

## Definition of Done

- [ ] `OllamaAgentProvider` implements `IAgentProvider`
- [ ] `OpenAiCompatibleClient` shared between OpenAI + Ollama (composition)
- [ ] Tool calling capability auto-detected on startup
- [ ] Structured text fallback when tool calling unavailable
- [ ] `ModelAssignment` extended with `ProviderType` + `Endpoint` (backward compatible)
- [ ] Hybrid routing: mix Claude + Ollama per task in single config
- [ ] `OllamaConfig` in `agentsmith.yml` with endpoint + model
- [ ] Cost tracking: local tasks show `$0.00 (local)`
- [ ] Docker Compose: `ollama` sidecar under `local-models` profile
- [ ] Startup validation: ping + model availability + tool calling detection
- [ ] Unit tests: OllamaAgentProvider (mocked HTTP), hybrid routing
- [ ] Integration test: full pipeline run with Ollama (mocked endpoint)
