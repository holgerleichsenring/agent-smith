# Phase 9: Model Registry & Scout Pattern - Implementation Plan

## Goal
Task-specific model routing. Haiku for file discovery (Scout), Sonnet for
coding (Primary), optionally Opus for complex architecture decisions (Reasoning).
60-80% cost reduction for discovery iterations.

---

## Prerequisite
- Phase 8 completed (Multi-model pattern proven through Haiku compaction)

## Steps

### Step 1: ModelRegistryConfig + IModelRegistry + TaskType
See: `prompts/phase9-model-registry.md`

Config, interface, and enum for task-based model routing.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: ScoutAgent + Integration
See: `prompts/phase9-scout.md`

Haiku-based file discovery with read-only tools, integration into ClaudeAgentProvider.
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (Config + Registry)
    └── Step 2 (Scout + Provider Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 9)

No new packages needed.

---

## Key Decisions

1. Scout is an implementation detail of ClaudeAgentProvider, not a new interface
2. `IAgentProvider` interface remains UNCHANGED - all optimizations are internal
3. Backward-compatible: If `Models` is null, the single `Model` field is used
4. ScoutTools are read-only (no write_file, no run_command) - Scout cannot break anything

---

## Definition of Done (Phase 9)
- [ ] `ModelRegistryConfig` + `ModelAssignment` in Contracts
- [ ] `TaskType` Enum in Contracts
- [ ] `IModelRegistry` interface in Contracts
- [ ] `ConfigBasedModelRegistry` in Infrastructure
- [ ] `ScoutAgent` in Infrastructure (Haiku, read-only tools)
- [ ] `ScoutResult` Record (RelevantFiles, ContextSummary, TokensUsed)
- [ ] `ToolDefinitions.ScoutTools` (only read_file + list_files)
- [ ] ClaudeAgentProvider: Run Scout before Primary (when configured)
- [ ] All existing tests green
- [ ] New unit tests for Registry, Scout, TaskType
- [ ] E2E test shows Scout + Primary model usage in logs


---

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


---

# Phase 9: Scout Agent - Implementation Details

## Overview
The Scout Agent is a lightweight, Haiku-based file discovery phase that runs
BEFORE the actual coding phase. It identifies relevant files and gathers context
so that the more expensive Primary Agent (Sonnet) can work directly with the
right files.

---

## ScoutResult Record (Infrastructure Layer)

```csharp
// Providers/Agent/ScoutResult.cs
public sealed record ScoutResult(
    IReadOnlyList<string> RelevantFiles,
    string ContextSummary,
    int TokensUsed);
```

---

## ScoutAgent (Infrastructure Layer)

```csharp
// Providers/Agent/ScoutAgent.cs
```

### Constructor
```csharp
public sealed class ScoutAgent(
    AnthropicClient client,
    string model,
    int maxTokens,
    ILogger logger)
```

### Method
```csharp
public async Task<ScoutResult> DiscoverAsync(
    Plan plan,
    string repositoryPath,
    CancellationToken cancellationToken = default)
```

### Algorithm
1. Creates its own ToolExecutor with ONLY read-only tools
2. Runs a short Agentic Loop (max 5 iterations)
3. System prompt instructs: "Explore the codebase, identify relevant files"
4. User prompt contains: Plan summary, plan steps, repository path
5. Collects all read file paths via FileReadTracker
6. Extracts the final text response as ContextSummary
7. Returns ScoutResult

### System Prompt
```
You are a codebase scout. Your job is to explore the repository and identify
all files relevant to the implementation plan below.

Instructions:
- Use list_files to understand the project structure
- Use read_file to examine files that might be relevant
- Focus on files that will need to be created or modified
- Also examine related files (imports, dependencies, tests)
- When done, provide a brief summary of what you found and which files are relevant

Do NOT modify any files. You are read-only.
```

---

## ScoutTools (ToolDefinitions Extension)

```csharp
// Addition to ToolDefinitions.cs
public static List<Tool> ScoutTools => new()
{
    // read_file - same as existing
    // list_files - same as existing
    // NO write_file
    // NO run_command
};
```

Scout gets only 2 of the 4 tools. No write access, no command execution.

---

## Integration in ClaudeAgentProvider.ExecutePlanAsync

### Flow with Scout
```
1. When Models is configured AND Scout model is set:
   a. ScoutAgent.DiscoverAsync(plan, repoPath)
   b. Scout result (RelevantFiles + ContextSummary) is added to the user prompt
   c. Primary Agent starts with preloaded context

2. When no Scout is configured:
   a. Behavior as before (Primary does everything)
```

### Extended User Prompt for Primary
```
Execute the following implementation plan in repository at: {repoPath}
Branch: {branch}

## Scout Results
The following files have been identified as relevant:
{relevantFiles}

Scout Summary: {contextSummary}

## Plan
...
```

### Advantage
- Scout (Haiku): ~$1/MT Input, 5 iterations file discovery → ~$0.01
- Without Scout: Primary (Sonnet) does file discovery itself → ~$0.10 for the same iterations
- 10x cost reduction for the discovery phase
- Primary starts directly with relevant context → fewer iterations overall

---

## Backward Compatibility
- `AgentConfig.Models` is nullable
- If null → no Scout, no Registry, everything as in Phase 8
- Existing agentsmith.yml works without changes
- Scout phase is only activated when explicitly configured
