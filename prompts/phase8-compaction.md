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
