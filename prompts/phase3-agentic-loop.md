# Phase 3 - Agentic Loop (Detail)

## Ziel
Detailbeschreibung der Agentic Loop in `ClaudeAgentProvider.ExecutePlanAsync`.
Das ist die komplexeste Logik im gesamten System.

---

## Ablauf

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
**Execution:** `Process.Start("bash", "-c", command)` im Repo-Verzeichnis
**Return:** stdout + stderr, exit code
**Security:** Timeout (60s), kein Zugriff außerhalb Repo

---

## Tool Executor

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/ToolExecutor.cs
```

Zentrale Klasse die Tool Calls dispatcht:

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

**Hinweise:**
- `_changes` trackt alle write_file Aufrufe
- Path Validation: Kein `..`, kein absoluter Pfad (Security)
- run_command: Timeout, Working Directory = Repo Root
- Fehler in Tools → Error Message als String zurück (kein Throw)

---

## Agentic Loop Implementation

```
Datei: src/AgentSmith.Infrastructure/Providers/Agent/AgenticLoop.cs
```

Eigene Klasse für die Loop-Logik, getrennt vom Provider.

**Constructor:**
- `AnthropicClient client`
- `string model`
- `ToolExecutor toolExecutor`
- `ILogger logger`
- `int maxIterations = 25`

**RunAsync(string systemPrompt, string userMessage):**
1. Erstelle Messages-Liste: `[{role: "user", content: userMessage}]`
2. Loop:
   a. API Call mit messages + tools
   b. Append assistant response zu messages
   c. Wenn `stop_reason == "end_turn"` → break
   d. Für jeden `tool_use` Block:
      - Execute via ToolExecutor
      - Append `tool_result` zu messages
   e. Iteration counter check
3. Return: `toolExecutor.GetChanges()`

**Wichtig:**
- Messages-Liste wächst mit jedem Schritt (Konversationshistorie)
- `stop_reason == "tool_use"` → weitermachen
- `stop_reason == "end_turn"` → Agent ist fertig
- Max Iterations als Safety Net (Default: 25)

---

## Verzeichnisstruktur

```
src/AgentSmith.Infrastructure/Providers/Agent/
├── ClaudeAgentProvider.cs    ← IAgentProvider Implementation
├── AgenticLoop.cs            ← Loop-Logik
├── ToolExecutor.cs           ← Tool Dispatch + Execution
└── ToolDefinitions.cs        ← Tool JSON Schemas als Konstanten
```

---

## Sicherheitshinweise

- **Path Traversal:** Alle Pfade validieren - kein `..`, kein absoluter Pfad
- **Command Injection:** run_command hat Timeout (60s) und läuft im Repo-Verzeichnis
- **API Key:** Nie loggen, nie in Responses leaken
- **Max Iterations:** Verhindert Endlosschleifen (Default: 25, konfigurierbar)
- **File Size:** Große Dateien (>100KB) abschneiden beim read_file
