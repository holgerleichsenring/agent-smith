# Phase 40c: AI Provider Extraction (Claude, OpenAI, Gemini)

## Goal

Extract all AI/LLM providers from `AgentSmith.Infrastructure` into dedicated
projects. Each provider encapsulates its SDK dependency and exposes only
Contracts-defined interfaces.

---

## Provider Projects

### AgentSmith.Providers.Claude

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      Anthropic.SDK
```

Contains:
- `ClaudeAgentProvider` — IAgentProvider
- `ClaudeContextCompactor` — IContextCompactor
- `ClaudeLlmClient` — ILlmClient

Config via `IOptions<ClaudeProviderOptions>`:
```csharp
public sealed class ClaudeProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "claude-sonnet-4-20250514";
    public int MaxRetries { get; set; } = 3;
}
```

Registration: `services.AddClaudeProvider(configuration);`

### AgentSmith.Providers.OpenAI

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      OpenAI
```

Contains:
- `OpenAiAgentProvider` — IAgentProvider
- `OpenAiLlmClient` — ILlmClient

Registration: `services.AddOpenAiProvider(configuration);`

### AgentSmith.Providers.Gemini

```
References: AgentSmith.Contracts, AgentSmith.Domain
NuGet:      Google.Cloud.AIPlatform.V1
```

Contains:
- `GeminiAgentProvider` — IAgentProvider

Registration: `services.AddGeminiProvider(configuration);`

---

## Key Design Decision: IContextCompactor stays with Claude

`IContextCompactor` currently lives in Infrastructure because it depends on
Anthropic.SDK types. It moves to `AgentSmith.Providers.Claude` — the only
provider that implements context compaction.

If other providers need compaction in the future, the interface is in Contracts
and they can provide their own implementation.

---

## Migration Steps

Same pattern as p40b:
1. Create project, move classes, add IOptions config
2. Add ServiceCollectionExtensions
3. Remove from Infrastructure
4. Update Host references

---

## Definition of Done

- [ ] 3 AI provider projects created and building
- [ ] Zero AI provider classes remain in Infrastructure
- [ ] IContextCompactor implementation in Claude provider
- [ ] All existing tests pass
- [ ] Each provider has its own `Add{Name}Provider()` extension

---

## Estimation

~80 lines new code per provider. ~250 lines total. Mostly move operations.
