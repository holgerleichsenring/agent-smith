# Phase 11: Factory, Config & Tests - Implementation Details

## Overview
Wire up the new providers in AgentProviderFactory, update configuration examples,
add provider-agnostic token tracking, and write factory tests.

---

## AgentProviderFactory Changes

```csharp
public IAgentProvider Create(AgentConfig config)
{
    return config.Type.ToLowerInvariant() switch
    {
        "claude" or "anthropic" => CreateClaude(config),
        "openai" => CreateOpenAi(config),
        "gemini" or "google" => CreateGemini(config),
        _ => throw new ConfigurationException(
            $"Unknown agent provider type: '{config.Type}'. Supported: claude, openai, gemini")
    };
}
```

Each factory method:
- Reads the appropriate API key from SecretsProvider (ANTHROPIC_API_KEY, OPENAI_API_KEY, GEMINI_API_KEY)
- Creates the model registry if configured
- Constructs the provider with all dependencies

---

## TokenUsageTracker - Provider-Agnostic Overload

```csharp
public void Track(int inputTokens, int outputTokens,
    int cacheCreateTokens = 0, int cacheReadTokens = 0)
```

Existing `Track(MessageResponse)` delegates to this new overload.
OpenAI and Gemini providers use this directly with raw token counts.

---

## Config Examples

### agentsmith.example.yml
Shows all three provider options (Claude active, OpenAI/Gemini commented out):
- Claude with full model registry, caching, compaction
- OpenAI with GPT-4.1 / GPT-4.1-mini
- Gemini with 2.5 Pro / 2.5 Flash
- Pricing section covers all models

### .env.example
Add `GEMINI_API_KEY` alongside existing keys.

### agentsmith.yml
Add `gemini_api_key: ${GEMINI_API_KEY}` to secrets section.

---

## Factory Tests

```csharp
[Theory]
[InlineData("claude", "Claude")]
[InlineData("anthropic", "Claude")]
[InlineData("openai", "OpenAI")]
[InlineData("gemini", "Gemini")]
[InlineData("google", "Gemini")]
public void Create_ValidType_ReturnsCorrectProvider(string type, string expected)
```

Also test: unknown type throws, case insensitivity works.
Tests set environment variables for API keys in constructor, clean up in Dispose.
