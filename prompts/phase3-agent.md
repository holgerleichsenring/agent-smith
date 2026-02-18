# Phase 3 - Schritt 4: Agent Provider (Agentic Loop)

## Ziel
ClaudeAgentProvider mit Anthropic SDK implementieren. Das Herzstück des Systems.
Projekt: `AgentSmith.Infrastructure/Providers/Agent/`

---

## Architektur

Der Agent Provider hat zwei Aufgaben:
1. **Plan generieren** (`GeneratePlanAsync`) - Einmaliger API Call
2. **Plan ausführen** (`ExecutePlanAsync`) - Agentic Loop mit Tool Calling

Die Agentic Loop ist das komplexeste Feature. Der Agent entscheidet selbst welche Dateien
er liest, ändert und in welcher Reihenfolge.

---

## ClaudeAgentProvider
```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/ClaudeAgentProvider.cs
```

**NuGet:** `Anthropic.SDK`

**Constructor:**
- `string apiKey`
- `string model` (z.B. `"claude-sonnet-4-20250514"`)
- `ILogger<ClaudeAgentProvider> logger`

### GeneratePlanAsync

1. Baue System Prompt mit Coding Principles
2. Baue User Prompt mit Ticket Details + Code Analysis
3. Sende an Claude API (kein Tool Calling, nur Text)
4. Parse Antwort → Domain `Plan`

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

Das ist der Kern. Siehe `prompts/phase3-agentic-loop.md` für Details.

---

## Handler Updates

**GeneratePlanHandler:** Stub → echt
```csharp
var provider = factory.Create(context.AgentConfig);
var plan = await provider.GeneratePlanAsync(
    context.Ticket, context.CodeAnalysis, context.CodingPrinciples, cancellationToken);
context.Pipeline.Set(ContextKeys.Plan, plan);
```

**AgenticExecuteHandler:** Stub → echt
```csharp
var provider = factory.Create(context.AgentConfig);
var changes = await provider.ExecutePlanAsync(
    context.Plan, context.Repository, context.CodingPrinciples, cancellationToken);
context.Pipeline.Set(ContextKeys.CodeChanges, changes);
```

---

## Tests

**ClaudeAgentProviderTests:**
- `GeneratePlanAsync_ValidInput_ReturnsPlan` (gemockter HTTP Client)
- `ExecutePlanAsync_WithToolCalls_ReturnsChanges` (gemockter HTTP Client)
