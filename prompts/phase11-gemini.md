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
