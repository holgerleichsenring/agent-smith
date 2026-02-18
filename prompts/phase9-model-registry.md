# Phase 9: Model Registry - Implementation Details

## Overview
The Model Registry maps task types to concrete model assignments.
Instead of using the same model everywhere, the agent selects the appropriate model
depending on the task: cheap for discovery, powerful for coding.

---

## TaskType Enum (Contracts Layer)

```csharp
// Providers/TaskType.cs
public enum TaskType
{
    Scout,          // File discovery, codebase exploration (Haiku)
    Primary,        // Main coding tasks (Sonnet)
    Planning,       // Plan generation (Sonnet)
    Reasoning,      // Complex architecture decisions (Opus, optional)
    Summarization   // Context compaction (Haiku)
}
```

---

## ModelAssignment (Contracts Layer)

```csharp
// Configuration/ModelAssignment.cs
public class ModelAssignment
{
    public string Model { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 8192;
}
```

---

## ModelRegistryConfig (Contracts Layer)

```csharp
// Configuration/ModelRegistryConfig.cs
public class ModelRegistryConfig
{
    public ModelAssignment Scout { get; set; } = new()
    {
        Model = "claude-haiku-4-5-20251001",
        MaxTokens = 4096
    };
    public ModelAssignment Primary { get; set; } = new()
    {
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 8192
    };
    public ModelAssignment Planning { get; set; } = new()
    {
        Model = "claude-sonnet-4-20250514",
        MaxTokens = 4096
    };
    public ModelAssignment? Reasoning { get; set; }
    public ModelAssignment Summarization { get; set; } = new()
    {
        Model = "claude-haiku-4-5-20251001",
        MaxTokens = 2048
    };
}
```

---

## IModelRegistry (Contracts Layer)

```csharp
// Providers/IModelRegistry.cs
public interface IModelRegistry
{
    ModelAssignment GetModel(TaskType taskType);
}
```

Simple interface - returns the appropriate ModelAssignment for a given TaskType.

---

## ConfigBasedModelRegistry (Infrastructure Layer)

```csharp
// Providers/Agent/ConfigBasedModelRegistry.cs
```

### Implementation
- Receives `ModelRegistryConfig` in the constructor
- Maps `TaskType` to the corresponding property in the config
- If `Reasoning` is null and requested → falls back to `Primary`
- Logging on model selection (Debug level)

### Fallback Strategy
```
TaskType.Scout          → config.Scout
TaskType.Primary        → config.Primary
TaskType.Planning       → config.Planning
TaskType.Reasoning      → config.Reasoning ?? config.Primary (Fallback)
TaskType.Summarization  → config.Summarization
```

---

## Integration in AgentConfig

```csharp
public class AgentConfig
{
    // ... existing properties ...
    public ModelRegistryConfig? Models { get; set; }  // nullable for backward compatibility
}
```

If `Models` is null, the single `Model` field is used for ALL TaskTypes.
This ensures that existing agentsmith.yml files work without changes.

---

## Integration in ClaudeAgentProvider

The provider uses the registry for:
1. `GeneratePlanAsync` → `TaskType.Planning`
2. `ExecutePlanAsync` → `TaskType.Primary` (for AgenticLoop)
3. Scout → `TaskType.Scout` (when Models is configured)
4. CompactionConfig.SummaryModel is superseded by `TaskType.Summarization`

---

## Config in agentsmith.yml

```yaml
agent:
  type: Claude
  model: claude-sonnet-4-20250514  # Fallback when models is not set
  models:
    scout:
      model: claude-haiku-4-5-20251001
      max_tokens: 4096
    primary:
      model: claude-sonnet-4-20250514
      max_tokens: 8192
    planning:
      model: claude-sonnet-4-20250514
      max_tokens: 4096
    summarization:
      model: claude-haiku-4-5-20251001
      max_tokens: 2048
```
