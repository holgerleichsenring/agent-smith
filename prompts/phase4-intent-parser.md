# Phase 4 - Step 1: IntentParser

## Goal
Convert user input like `"fix #123 in todo-list"` into a structured `ParsedIntent`.
Project: `AgentSmith.Application/Services/`

---

## RegexIntentParser

```
File: src/AgentSmith.Application/Services/RegexIntentParser.cs
```

Implements `IIntentParser` from Contracts.

**Supported Patterns:**
```
"fix #123 in todo-list"      → #123, todo-list
"#34237 todo-list"            → #34237, todo-list
"todo-list #123"              → #123, todo-list
"fix 123 in todo-list"        → 123, todo-list
"resolve ticket #42 in api" → #42, api
```

**Regex Strategy:**
1. Extract TicketId: `#?(\d+)` - Number with optional `#`
2. Extract ProjectName: Remove known noise words (`fix`, `resolve`, `in`, `ticket`, etc.)
   → remaining word = ProjectName

**Alternative Approach (simpler):**
- Regex 1: `#?(\d+)\s+(?:in\s+)?(\w+)` → Ticket first
- Regex 2: `(\w+)\s+#?(\d+)` → Project first
- Both are tried, first match wins

**Validation:**
- No match → `ConfigurationException("Could not parse intent from input: ...")`
- TicketId must be numeric
- ProjectName must be non-empty

**Constructor:**
- `ILogger<RegexIntentParser> logger`

---

## Extensibility

Later a `ClaudeIntentParser` can be registered as an alternative:
```csharp
// DI: Swap via config or feature flag
services.AddTransient<IIntentParser, RegexIntentParser>();
// or:
services.AddTransient<IIntentParser, ClaudeIntentParser>();
```

---

## Tests

**RegexIntentParserTests:**
- `ParseAsync_FixHashInProject_ReturnsCorrectIntent`
- `ParseAsync_ProjectFirst_ReturnsCorrectIntent`
- `ParseAsync_NoHash_ReturnsCorrectIntent`
- `ParseAsync_InvalidInput_ThrowsConfigurationException`
- `ParseAsync_OnlyNumber_ThrowsConfigurationException`
