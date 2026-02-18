# Phase 6: Resilience - Retry with Exponential Backoff - Implementation Plan

## Goal
Agent Smith survives Rate Limits (429) and transient errors (500/502/503).
Prerequisite for all further optimizations (Caching, Compaction, Scout).

---

## Prerequisite
- Phase 5 completed (CLI functional)
- First E2E test documented (run-log-001): Crash at Iteration 6 due to Rate Limit

## Findings from SDK Analysis

Anthropic.SDK 5.9.0 provides:
- **No** built-in RetryInterceptor
- `AnthropicClient(string apiKey, HttpClient httpClient)` → HttpClient injection possible
- `RateLimitsExceeded` Exception (catchable)
- `RateLimits` Type on Response (RequestsLimit, etc.)
- `Usage` with InputTokens, OutputTokens, CacheCreationInputTokens, CacheReadInputTokens

Strategy: **Polly via `Microsoft.Extensions.Http.Resilience`** as DelegatingHandler on the HttpClient.

## Steps

### Step 1: RetryConfig + Resilient HttpClient Factory
See: `prompts/phase6-retry.md`

New config class for retry settings and a factory that creates HttpClients with
Polly-based retry handler.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Integration in ClaudeAgentProvider + AgenticLoop
See: `prompts/phase6-retry.md` (second part)

Adapt existing classes: Use resilient client, log token usage.
Project: `AgentSmith.Infrastructure/`

### Step 3: Config + Tests + Verify
Extend config, write tests, validate E2E.

---

## Dependencies

```
Step 1 (RetryConfig + Factory)
    └── Step 2 (Integration Provider + Loop)
         └── Step 3 (Config + Tests + Verify)
```

Strictly sequential.

---

## NuGet Packages (Phase 6)

| Project | Package | Purpose |
|---------|---------|---------|
| AgentSmith.Infrastructure | Microsoft.Extensions.Http.Resilience | Polly-based HttpClient Retry |
| AgentSmith.Infrastructure | Microsoft.Extensions.Http | HttpClientFactory |

---

## Definition of Done (Phase 6)
- [ ] `RetryConfig` class in Contracts with sensible defaults
- [ ] `AgentConfig.Retry` Property
- [ ] Resilient HttpClient with Polly: Retry on 429, 500, 502, 503, 504
- [ ] Exponential Backoff with Jitter
- [ ] `ClaudeAgentProvider` uses resilient client for Plan + Execution
- [ ] `AgenticLoop` logs token usage per iteration
- [ ] YAML Config supports `retry:` section (backward-compatible)
- [ ] All existing tests green
- [ ] New unit tests for Retry Config
- [ ] E2E test survives Rate Limits
