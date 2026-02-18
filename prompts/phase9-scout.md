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
