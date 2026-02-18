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
