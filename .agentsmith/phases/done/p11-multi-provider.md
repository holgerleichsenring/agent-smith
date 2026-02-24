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


---

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


---

# Phase 11: Gemini Provider - Implementation Details

## Overview
Google Gemini API provider with function calling support.
Uses the `Google_GenerativeAI` NuGet package (3.6.3).

---

## GeminiAgentProvider (Infrastructure Layer)

```csharp
public sealed class GeminiAgentProvider(
    string apiKey,
    string model,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<GeminiAgentProvider> logger) : IAgentProvider
```

### ProviderType
Returns `"Gemini"`.

### GeneratePlanAsync
Single `GenerateContentAsync` call with system instruction + user content.
Parses JSON response from `candidate.Content.Parts` text.

### ExecutePlanAsync
Creates `GeminiAgenticLoop` with `GenerativeModel`, shared `ToolExecutor` and `TokenUsageTracker`.
No scout phase, no prompt caching, no context compaction (Claude-specific).

### CreateModel
```csharp
var googleAi = new GoogleAi(apiKey);
return googleAi.CreateGenerativeModel(modelId);
```

---

## GeminiAgenticLoop (Infrastructure Layer)

```csharp
public sealed class GeminiAgenticLoop(
    GenerativeModel model,
    ToolExecutor toolExecutor,
    ILogger logger,
    TokenUsageTracker tracker,
    int maxIterations = 25)
```

### Loop Flow
1. Build contents list with `new Content(userMessage, Roles.User)`
2. Create `GenerateContentRequest` with system instruction, contents, and tools
3. Call `model.GenerateContentAsync(request)`
4. Track usage from `response.UsageMetadata` (PromptTokenCount, CandidatesTokenCount)
5. Add assistant response (`candidate.Content`) to contents
6. Check for function calls in `candidate.Content.Parts` where `p.FunctionCall is not null`
7. If function calls: execute each via `ToolExecutor`, build `FunctionResponse` parts
8. Add function responses as `new Content(responseParts, Roles.Function)`
9. Continue loop

### Function Call Processing
```csharp
var argsJson = JsonSerializer.Serialize(call.Args);
var input = JsonNode.Parse(argsJson);
var result = await toolExecutor.ExecuteAsync(call.Name, input);
parts.Add(new Part
{
    FunctionResponse = new FunctionResponse
    {
        Name = call.Name,
        Response = JsonSerializer.SerializeToNode(new { result = toolResult })
    }
});
```

---

## GeminiToolDefinitions (Infrastructure Layer)

Defines the same 4 tools using Gemini's `FunctionDeclaration` + `Schema` format.
Schema types are strings ("OBJECT", "STRING") not enum values.

### Namespace Note
`GenerativeAI.Types.TaskType` conflicts with `AgentSmith.Contracts.Providers.TaskType`.
Use alias: `using TaskType = AgentSmith.Contracts.Providers.TaskType;`

---

## Key Types
- `GenerativeAI.GoogleAi` - Entry point
- `GenerativeAI.GenerativeModel` - Model instance
- `GenerativeAI.Types.GenerateContentRequest` / `GenerateContentResponse` - Request/response
- `GenerativeAI.Types.FunctionCall` / `FunctionResponse` - Function calling types
- `GenerativeAI.Types.Content`, `Part`, `Schema`, `Tool` - Content building blocks
- `GenerativeAI.Roles` - Role constants (User, Model, Function)


---

# Phase 11: OpenAI Provider - Implementation Details

## Overview
OpenAI Chat Completions API provider with tool calling support.
Uses the official `OpenAI` NuGet package (2.8.0).

---

## OpenAiAgentProvider (Infrastructure Layer)

```csharp
public sealed class OpenAiAgentProvider(
    string apiKey,
    string model,
    RetryConfig retryConfig,
    IModelRegistry? modelRegistry,
    PricingConfig pricingConfig,
    ILogger<OpenAiAgentProvider> logger) : IAgentProvider
```

### ProviderType
Returns `"OpenAI"`.

### GeneratePlanAsync
Single Chat Completions call with system + user messages, no tools.
Parses JSON response into `Plan` (same logic as Claude provider).

### ExecutePlanAsync
Creates `OpenAiAgenticLoop` with `ChatClient`, shared `ToolExecutor` and `TokenUsageTracker`.
No scout phase (Claude-specific optimization).
No prompt caching (OpenAI doesn't support it in the same way).

### CreateChatClient
Uses `ResilientHttpClientFactory` for retry via Polly, then creates
`OpenAIClient` with custom `HttpClientPipelineTransport` and gets `ChatClient`.

---

## OpenAiAgenticLoop (Infrastructure Layer)

```csharp
public sealed class OpenAiAgenticLoop(
    ChatClient client,
    ToolExecutor toolExecutor,
    ILogger logger,
    TokenUsageTracker tracker,
    int maxIterations = 25)
```

### Loop Flow
1. Build messages list: `SystemChatMessage` + `UserChatMessage`
2. Add all tools via `ChatCompletionOptions.Tools`
3. Call `client.CompleteChatAsync(messages, options)`
4. Track usage: `completion.Usage.InputTokenCount` / `OutputTokenCount`
5. Add `AssistantChatMessage(completion)` to messages
6. Check `completion.FinishReason == ChatFinishReason.ToolCalls`
7. If tool calls: execute each via `ToolExecutor`, add `ToolChatMessage(callId, result)`
8. If no tool calls (`ChatFinishReason.Stop`): agent done, return changes

### Tool Call Processing
```csharp
foreach (var toolCall in completion.ToolCalls)
{
    JsonNode? input = JsonNode.Parse(toolCall.FunctionArguments.ToString());
    var result = await toolExecutor.ExecuteAsync(toolCall.FunctionName, input);
    results.Add(new ToolChatMessage(toolCall.Id, result));
}
```

---

## OpenAiToolDefinitions (Infrastructure Layer)

Defines the same 4 tools (read_file, write_file, list_files, run_command) using
OpenAI's `ChatTool.CreateFunctionTool()` format with `BinaryData.FromString()` JSON schemas.

---

## Key Types
- `OpenAI.Chat.ChatClient` - Main client
- `OpenAI.Chat.ChatCompletion` - Response
- `OpenAI.Chat.ChatFinishReason.ToolCalls` - Indicates tool calls needed
- `OpenAI.Chat.ChatTool` - Tool definition
- `OpenAI.Chat.ToolChatMessage` - Tool result message
