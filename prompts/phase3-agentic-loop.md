# Phase 3 - Agentic Loop (Detail)

## Goal
Detailed description of the agentic loop in `ClaudeAgentProvider.ExecutePlanAsync`.
This is the most complex logic in the entire system.

---

## Flow

```
┌─────────────────────────────────────────────────────┐
│  1. Build initial message with plan + principles    │
│  2. Define tools (read_file, write_file, etc.)      │
│  3. Send to Claude API                              │
│                                                     │
│  ┌──── LOOP (max iterations) ──────────────────┐    │
│  │  4. Receive response                        │    │
│  │  5. If no tool_use → agent is done → break  │    │
│  │  6. For each tool_use block:                │    │
│  │     - Execute tool locally                  │    │
│  │     - Collect tool result                   │    │
│  │  7. Send tool results back to Claude        │    │
│  │  8. Track file changes                      │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  9. Return collected CodeChange objects              │
└─────────────────────────────────────────────────────┘
```

---

## Tool Definitions

### read_file
```json
{
  "name": "read_file",
  "description": "Read the contents of a file in the repository",
  "input_schema": {
    "type": "object",
    "properties": {
      "path": { "type": "string", "description": "Relative path from repo root" }
    },
    "required": ["path"]
  }
}
```
**Execution:** `File.ReadAllText(Path.Combine(repoRoot, path))`
**Error:** File not found → return error message as tool result

### write_file
```json
{
  "name": "write_file",
  "description": "Write or overwrite a file in the repository",
  "input_schema": {
    "type": "object",
    "properties": {
      "path": { "type": "string", "description": "Relative path from repo root" },
      "content": { "type": "string", "description": "Complete file content" }
    },
    "required": ["path", "content"]
  }
}
```
**Execution:** `File.WriteAllText(Path.Combine(repoRoot, path), content)`
**Side effect:** Track as `CodeChange(path, content, "Create"/"Modify")`

### list_files
```json
{
  "name": "list_files",
  "description": "List files in a directory",
  "input_schema": {
    "type": "object",
    "properties": {
      "path": { "type": "string", "description": "Relative directory path, empty for root" }
    },
    "required": ["path"]
  }
}
```
**Execution:** `Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories)`
**Return:** Newline-separated relative paths

### run_command
```json
{
  "name": "run_command",
  "description": "Run a shell command in the repository directory",
  "input_schema": {
    "type": "object",
    "properties": {
      "command": { "type": "string", "description": "Shell command to execute" }
    },
    "required": ["command"]
  }
}
```
**Execution:** `Process.Start("bash", "-c", command)` in the repo directory
**Return:** stdout + stderr, exit code
**Security:** Timeout (60s), no access outside the repo

---

## Tool Executor

```
File: src/AgentSmith.Infrastructure/Providers/Agent/ToolExecutor.cs
```

Central class that dispatches tool calls:

```csharp
public sealed class ToolExecutor(string repositoryPath, ILogger logger)
{
    private readonly List<CodeChange> _changes = new();

    public async Task<string> ExecuteAsync(string toolName, JsonElement input)
    {
        return toolName switch
        {
            "read_file" => await ReadFile(input),
            "write_file" => await WriteFile(input),
            "list_files" => ListFiles(input),
            "run_command" => await RunCommand(input),
            _ => $"Unknown tool: {toolName}"
        };
    }

    public IReadOnlyList<CodeChange> GetChanges() => _changes.AsReadOnly();
}
```

**Notes:**
- `_changes` tracks all write_file calls
- Path validation: No `..`, no absolute paths (security)
- run_command: Timeout, working directory = repo root
- Errors in tools → error message returned as string (no throw)

---

## Agentic Loop Implementation

```
File: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

Separate class for the loop logic, decoupled from the provider.

**Constructor:**
- `AnthropicClient client`
- `string model`
- `ToolExecutor toolExecutor`
- `ILogger logger`
- `int maxIterations = 25`

**RunAsync(string systemPrompt, string userMessage):**
1. Create messages list: `[{role: "user", content: userMessage}]`
2. Loop:
   a. API call with messages + tools
   b. Append assistant response to messages
   c. If `stop_reason == "end_turn"` → break
   d. For each `tool_use` block:
      - Execute via ToolExecutor
      - Append `tool_result` to messages
   e. Iteration counter check
3. Return: `toolExecutor.GetChanges()`

**Important:**
- Messages list grows with each step (conversation history)
- `stop_reason == "tool_use"` → continue
- `stop_reason == "end_turn"` → agent is done
- Max iterations as safety net (default: 25)

---

## Directory Structure

```
src/AgentSmith.Infrastructure/Providers/Agent/
├── ClaudeAgentProvider.cs    ← IAgentProvider Implementation
├── AgenticLoop.cs            ← Loop logic
├── ToolExecutor.cs           ← Tool dispatch + execution
└── ToolDefinitions.cs        ← Tool JSON schemas as constants
```

---

## Security Notes

- **Path Traversal:** Validate all paths - no `..`, no absolute paths
- **Command Injection:** run_command has a timeout (60s) and runs in the repo directory
- **API Key:** Never log, never leak in responses
- **Max Iterations:** Prevents infinite loops (default: 25, configurable)
- **File Size:** Truncate large files (>100KB) on read_file
