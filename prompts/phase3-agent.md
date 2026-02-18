# Phase 3 - Step 4: Agent Provider (Agentic Loop)

## Goal
Implement ClaudeAgentProvider with Anthropic SDK. The core piece of the system.
Project: `AgentSmith.Infrastructure/Providers/Agent/`

---

## Architecture

The Agent Provider has two responsibilities:
1. **Generate plan** (`GeneratePlanAsync`) - Single API call
2. **Execute plan** (`ExecutePlanAsync`) - Agentic loop with tool calling

The agentic loop is the most complex feature. The agent decides on its own which files
it reads, modifies, and in what order.

---

## ClaudeAgentProvider
```
File: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

**NuGet:** `Anthropic.SDK`

**Constructor:**
- `string apiKey`
- `string model` (e.g. `"claude-sonnet-4-20250514"`)
- `ILogger<ClaudeAgentProvider> logger`

### GeneratePlanAsync

1. Build system prompt with Coding Principles
2. Build user prompt with ticket details + code analysis
3. Send to Claude API (no tool calling, text only)
4. Parse response → Domain `Plan`

**System Prompt Template:**
```
You are a senior software engineer. Analyze the following ticket and codebase,
then create a detailed implementation plan.

## Coding Principles
{codingPrinciples}

## Respond in JSON format:
{
  "summary": "...",
  "steps": [
    { "order": 1, "description": "...", "target_file": "...", "change_type": "Create|Modify|Delete" }
  ]
}
```

### ExecutePlanAsync (Agentic Loop)

This is the core. See `prompts/phase3-agentic-loop.md` for details.

---

## Handler Updates

**GeneratePlanHandler:** Stub → real
```csharp
var provider = factory.Create(context.AgentConfig);
var plan = await provider.GeneratePlanAsync(
    context.Ticket, context.CodeAnalysis, context.CodingPrinciples, cancellationToken);
context.Pipeline.Set(ContextKeys.Plan, plan);
```

**AgenticExecuteHandler:** Stub → real
```csharp
var provider = factory.Create(context.AgentConfig);
var changes = await provider.ExecutePlanAsync(
    context.Plan, context.Repository, context.CodingPrinciples, cancellationToken);
context.Pipeline.Set(ContextKeys.CodeChanges, changes);
```

---

## Tests

**ClaudeAgentProviderTests:**
- `GeneratePlanAsync_ValidInput_ReturnsPlan` (mocked HTTP client)
- `ExecutePlanAsync_WithToolCalls_ReturnsChanges` (mocked HTTP client)
