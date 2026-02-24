# Phase 8: Context Compaction - Implementation Plan

## Goal
Prevent the conversation history from growing unboundedly. After N iterations,
old tool results are summarized and raw file contents are removed. Enables
25+ iterations for complex multi-file tasks.

---

## Prerequisite
- Phase 7 completed (TokenUsageTracker provides token counts for trigger decisions)

## Steps

### Step 1: CompactionConfig + IContextCompactor + FileReadTracker
See: `prompts/phase8-compaction.md`

Config, interface, and deduplication.
Project: `AgentSmith.Contracts/`, `AgentSmith.Infrastructure/`

### Step 2: ClaudeContextCompactor + Integration
See: `prompts/phase8-file-tracking.md`

LLM-based compression via Haiku + integration into AgenticLoop.
Project: `AgentSmith.Infrastructure/`

### Step 3: Tests + Verify

---

## Dependencies

```
Step 1 (Config + Interface + FileTracker)
    └── Step 2 (Compactor + Loop Integration)
         └── Step 3 (Tests + Verify)
```

---

## NuGet Packages (Phase 8)

No new packages needed.

---

## Definition of Done (Phase 8)
- [ ] `CompactionConfig` class in Contracts
- [ ] `IContextCompactor` interface in Contracts
- [ ] `ClaudeContextCompactor` implementation in Infrastructure (uses Haiku)
- [ ] `FileReadTracker` deduplicates file reads
- [ ] `AgenticLoop` triggers compaction based on iteration count OR token count
- [ ] Last N iterations remain fully preserved
- [ ] All existing tests green
- [ ] New unit tests for Compactor, FileTracker, Compaction trigger
- [ ] E2E test can run 15+ iterations without context explosion


---

# Phase 8: Context Compaction - Implementation Details

## Overview
Context Compaction prevents the conversation history in the Agentic Loop from
growing unboundedly. Old tool results are replaced with LLM-generated summaries,
while the last N iterations remain fully preserved.

---

## CompactionConfig (Contracts Layer)

```csharp
// Configuration/CompactionConfig.cs
public class CompactionConfig
{
    public bool Enabled { get; set; } = true;
    public int ThresholdIterations { get; set; } = 8;
    public int MaxContextTokens { get; set; } = 80000;
    public int KeepRecentIterations { get; set; } = 3;
    public string SummaryModel { get; set; } = "claude-haiku-4-5-20251001";
}
```

- `ThresholdIterations`: At which iteration compaction is triggered
- `MaxContextTokens`: Alternative token-based limit (not yet active, for Phase 9)
- `KeepRecentIterations`: Last N iterations ALWAYS remain fully preserved
- `SummaryModel`: Cheap model for summarization (Haiku: $1/MT Input)

---

## IContextCompactor (Contracts Layer)

```csharp
// Providers/IContextCompactor.cs
public interface IContextCompactor
{
    Task<List<Message>> CompactAsync(
        List<Message> messages,
        int keepRecentMessages,
        CancellationToken cancellationToken = default);
}
```

Interface in Contracts so that:
1. Clean Architecture is maintained (Infrastructure depends on Contracts, not the other way around)
2. Alternative implementations are possible later (rule-based, without LLM)

---

## ClaudeContextCompactor (Infrastructure Layer)

```csharp
// Providers/Agent/ClaudeContextCompactor.cs
```

### Algorithm
1. **Input**: Complete message list from the Agentic Loop
2. **Split**: Divide messages into `old` (to be compressed) and `recent` (to keep)
   - `recent` = last `keepRecentMessages` messages
   - `old` = everything before that
3. **Extraction**: Extract all TextContent and ToolResultContent from `old`
4. **Summarization**: Summarize via Haiku API call
   - System prompt: "Summarize the following conversation history. Keep file paths,
     key decisions, and important findings. Omit raw file contents."
   - User message: The extracted old contents
5. **Result**: New message list = [Summary as User Message] + `recent`

### Prompt for Summarization
```
You are a context compactor. Summarize the following conversation history between
an AI assistant and tool calls. Preserve:
- File paths that were read or modified
- Key decisions and reasoning
- Error messages and how they were resolved
- The current state of the implementation

Omit:
- Raw file contents (just note which files were read)
- Redundant tool call/result pairs
- Verbose command output (just note the outcome)

Be concise but complete. The summary will be used as context for continuing the work.
```

### Important
- The Compactor uses its OWN AnthropicClient (Haiku), not the one from the Primary Loop
- PromptCaching is also activated for the compaction call
- No recursion: If the summary itself is too long, it gets truncated

---

## Integration in AgenticLoop

### Trigger Logic
```
After each iteration:
  if (compaction.Enabled && iteration >= compaction.ThresholdIterations):
    if (iteration % compaction.ThresholdIterations == 0):  // periodic, not just once
      messages = await compactor.CompactAsync(messages, keepRecentMessages)
```

`keepRecentMessages` = `KeepRecentIterations * 2` (each iteration = 1 Assistant + 1 User Message)

### Changes to AgenticLoop
- New constructor parameter: `CompactionConfig compactionConfig, IContextCompactor? compactor`
- Compactor is nullable (optional, backward-compatible)
- After trigger: messages list is replaced in-place
- Logging: "Context compacted: {OldCount} messages → {NewCount} messages"

---

## Backward Compatibility
- CompactionConfig has sensible defaults (Enabled=true, Threshold=8)
- If no Compactor is injected (null), no compaction takes place
- AgentConfig gets `CompactionConfig Compaction` property with `new()` default
- Existing agentsmith.yml works without changes


---

# Phase 8: File Read Tracker - Deduplication

## Overview
The FileReadTracker prevents the same file from being fully inserted into the
conversation history multiple times. When an already-read file is requested again,
only a short note is returned.

---

## FileReadTracker (Infrastructure Layer)

```csharp
// Providers/Agent/FileReadTracker.cs
public sealed class FileReadTracker
{
    private readonly Dictionary<string, int> _readCounts = new(StringComparer.OrdinalIgnoreCase);

    public bool HasBeenRead(string filePath)
    {
        return _readCounts.ContainsKey(filePath);
    }

    public void TrackRead(string filePath)
    {
        _readCounts.TryGetValue(filePath, out var count);
        _readCounts[filePath] = count + 1;
    }

    public int GetReadCount(string filePath)
    {
        return _readCounts.GetValueOrDefault(filePath);
    }

    public IReadOnlyCollection<string> GetAllReadFiles()
    {
        return _readCounts.Keys;
    }
}
```

---

## Integration in ToolExecutor

### Changes to ReadFile
```
In the ReadFile tool:
  1. Read file (as before)
  2. tracker.TrackRead(path)
  3. If this is the FIRST read: return full file
  4. If already read: "[File previously read: {path}. Use the content from the earlier read.]"
```

### Why?
- In a typical Agentic Session, Claude reads the same file 3-5 times
- Each read inserts the complete file content into the history again
- With 10 iterations and 2-3 file reads each: 60-80% redundant tokens
- The tracker reduces this to a one-line note

### Edge Case: Modified Files
When a file has been modified via `write_file`, the next `read_file` MUST
return the new version. Solution:
- ToolExecutor notifies FileReadTracker on write_file: `tracker.InvalidateRead(path)`
- After invalidation, the next read returns the full content again

```csharp
public void InvalidateRead(string filePath)
{
    _readCounts.Remove(filePath);
}
```

---

## Integration in ToolExecutor

### Constructor Change
```csharp
public sealed class ToolExecutor(
    string repositoryPath,
    ILogger logger,
    FileReadTracker? fileReadTracker = null)
```

Nullable for backward compatibility. If null, deduplication is skipped.

### ReadFile Change
```csharp
private string ReadFile(JsonNode? input)
{
    var path = GetStringParam(input, "path");
    ValidatePath(path);
    var fullPath = Path.Combine(repositoryPath, path);

    if (!File.Exists(fullPath))
        return $"Error: File not found: {path}";

    // Deduplication check
    if (fileReadTracker is not null && fileReadTracker.HasBeenRead(path))
    {
        fileReadTracker.TrackRead(path); // count re-reads
        return $"[File previously read: {path}. Content unchanged since last read.]";
    }

    // ... existing read logic ...

    fileReadTracker?.TrackRead(path);
    return content;
}
```

### WriteFile Change
```csharp
private string WriteFile(JsonNode? input)
{
    // ... existing write logic ...

    fileReadTracker?.InvalidateRead(path);  // allow re-read of modified file

    return $"File written: {path}";
}
```

---

## Tests

### FileReadTrackerTests
- `HasBeenRead_ReturnsFalse_WhenNotRead`
- `HasBeenRead_ReturnsTrue_AfterTrackRead`
- `TrackRead_IncrementsCount`
- `InvalidateRead_ResetsState`
- `GetAllReadFiles_ReturnsTrackedPaths`
- `CaseInsensitive_PathComparison`

### ToolExecutor Integration Tests
- `ReadFile_ReturnsFullContent_OnFirstRead`
- `ReadFile_ReturnsShortMessage_OnSecondRead`
- `ReadFile_ReturnsFullContent_AfterWriteInvalidation`
