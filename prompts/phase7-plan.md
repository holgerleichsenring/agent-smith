# Phase 7: Prompt Caching - Implementation Plan

## Goal
Activate Anthropic Prompt Caching so that System Prompt, Tool Definitions, and Coding
Principles are cached server-side. Cached tokens do NOT count against the ITPM
Rate Limit - the single biggest lever for our Rate Limit problems.

---

## Prerequisite
- Phase 6 completed (Retry logic in place as safety net for cache misses)

## SDK Findings

Anthropic.SDK 5.9.0 provides:
- `MessageParameters.PromptCaching` Property (Type: `PromptCacheType`)
  - `None` = 0 (no caching)
  - `FineGrained` = 1 (manual via `CacheControl` on SystemMessages/Content)
  - `AutomaticToolsAndSystem` = 2 (automatic for all System Messages and Tools)
- `SystemMessage(text, cacheControl)` Constructor
- `CacheControl { Type = CacheControlType.ephemeral, TTL = null }` (5 min default)
- `Usage.CacheCreationInputTokens` / `Usage.CacheReadInputTokens` on Response

## Steps

### Step 1: CacheConfig + TokenUsageTracker
See: `prompts/phase7-caching.md`

New config class and token tracking for observability.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: Activate Prompt Caching
See: `prompts/phase7-token-tracking.md`

Set `PromptCaching = PromptCacheType.AutomaticToolsAndSystem` on all API calls.
Restructure System Prompt for optimal cache prefix.
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (CacheConfig + Tracker)
    └── Step 2 (Activate Caching)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 7)

No new packages needed. Everything is included in Anthropic.SDK 5.9.0.

---

## Definition of Done (Phase 7)
- [ ] `CacheConfig` class in Contracts
- [ ] `AgentConfig.Cache` Property
- [ ] `PromptCaching = AutomaticToolsAndSystem` on all API calls (when enabled)
- [ ] System Prompt optimally structured (Coding Principles first = longest stable prefix)
- [ ] `TokenUsageTracker` logs cumulative usage incl. cache metrics
- [ ] Cache Hit Rate visible in logs
- [ ] All existing tests green
- [ ] New unit tests for TokenUsageTracker
- [ ] E2E shows cache hits from Iteration 2 onwards
