# Phase 11: Multi-Provider (OpenAI + Gemini) - Implementation Plan

## Goal
Agent Smith works with Anthropic Claude, OpenAI, and Google Gemini. Selectable via config.
Biggest cost lever: Gemini Flash as scout/summarization, GPT-4.1-mini as cheap alternative.

---

## Prerequisite
- Phase 10 completed (Container Production-Ready)

## Steps

### Step 1: OpenAI Provider
See: `prompts/phase11-openai.md`

OpenAI Chat Completions API with tool calling. Own agentic loop and tool definitions.
Project: `AgentSmith.Infrastructure/`

### Step 2: Gemini Provider
See: `prompts/phase11-gemini.md`

Google Gemini API with function calling. Own agentic loop and tool definitions.
Project: `AgentSmith.Infrastructure/`

### Step 3: Factory + Config + Tests
See: `prompts/phase11-factory.md`

Wire up AgentProviderFactory, update config examples, add factory tests.
Project: `AgentSmith.Infrastructure/`, `config/`

### Step 4: Verify

---

## Dependencies

```
Step 1 (OpenAI) ──────┐
Step 2 (Gemini) ──────├── Step 3 (Factory + Config)
                       └── Step 4 (Verify)
```

Steps 1 and 2 are independent and can be implemented in parallel.

---

## NuGet Packages (Phase 11)
- `OpenAI` 2.8.0 (official Microsoft/OpenAI SDK)
- `Google_GenerativeAI` 3.6.3 (community SDK, most complete)

---

## Key Decisions

1. **Each provider is self-contained** - no shared "LLM abstraction layer". APIs are too different (tool format, streaming, caching). Each provider has its own loop.
2. **IAgentProvider interface stays stable** - `GeneratePlanAsync` + `ExecutePlanAsync`. Internally each provider can solve scout/compaction differently.
3. **ToolExecutor is shared** - File operations and command execution are provider-independent.
4. **TokenUsageTracker gets provider-agnostic overload** - `Track(int inputTokens, int outputTokens, int cacheCreate, int cacheRead)` alongside the Claude-specific `Track(MessageResponse)`.
5. **Prompt caching and context compaction are Claude-only** for now (OpenAI/Gemini don't have equivalent APIs).
6. **Scout is Claude-only** for now - OpenAI/Gemini providers do direct execution without scout phase.

---

## Cost Comparison (estimated per run)
| Config | Scout | Primary | Cost/Run |
|--------|-------|---------|----------|
| Anthropic (current) | Haiku | Sonnet | ~$5 |
| OpenAI | - | GPT-4.1 | ~$2-3 |
| Google | - | Gemini 2.5 Pro | ~$1-2 |

---

## Definition of Done (Phase 11)
- [ ] `OpenAiAgentProvider` implementing IAgentProvider
- [ ] `OpenAiAgenticLoop` with tool calling
- [ ] `OpenAiToolDefinitions` (ChatTool format)
- [ ] `GeminiAgentProvider` implementing IAgentProvider
- [ ] `GeminiAgenticLoop` with function calling
- [ ] `GeminiToolDefinitions` (FunctionDeclaration format)
- [ ] `TokenUsageTracker.Track(int, int, int, int)` provider-agnostic overload
- [ ] `AgentProviderFactory` supports "claude"/"anthropic", "openai", "gemini"/"google"
- [ ] Config examples for all three providers
- [ ] .env.example with GEMINI_API_KEY
- [ ] Factory tests for all provider types
- [ ] All existing tests green
