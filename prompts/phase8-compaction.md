# Phase 8: Context Compaction - Implementierungsdetails

## Überblick
Context Compaction verhindert, dass die Konversationshistorie in der Agentic Loop
unbegrenzt wächst. Alte Tool-Results werden durch LLM-generierte Zusammenfassungen
ersetzt, während die letzten N Iterationen vollständig erhalten bleiben.

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

- `ThresholdIterations`: Ab welcher Iteration die Komprimierung getriggert wird
- `MaxContextTokens`: Alternatives Token-basiertes Limit (noch nicht aktiv, für Phase 9)
- `KeepRecentIterations`: Letzte N Iterationen bleiben IMMER vollständig erhalten
- `SummaryModel`: Günstiges Model für Zusammenfassung (Haiku: $1/MT Input)

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

Interface in Contracts, damit:
1. Clean Architecture gewahrt bleibt (Infrastructure hängt von Contracts ab, nicht umgekehrt)
2. Spätere Implementierungen möglich sind (regelbasiert, ohne LLM)

---

## ClaudeContextCompactor (Infrastructure Layer)

```csharp
// Providers/Agent/ClaudeContextCompactor.cs
```

### Algorithmus
1. **Eingabe**: Vollständige Message-Liste aus der Agentic Loop
2. **Split**: Messages aufteilen in `old` (zu komprimieren) und `recent` (behalten)
   - `recent` = letzte `keepRecentMessages` Messages
   - `old` = alles davor
3. **Extraktion**: Aus `old` alle TextContent-Inhalte und ToolResultContent extrahieren
4. **Zusammenfassung**: Via Haiku-API-Call zusammenfassen
   - System-Prompt: "Summarize the following conversation history. Keep file paths,
     key decisions, and important findings. Omit raw file contents."
   - User-Message: Die extrahierten alten Inhalte
5. **Ergebnis**: Neue Message-Liste = [Summary als User-Message] + `recent`

### Prompt für Zusammenfassung
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

### Wichtig
- Der Compactor nutzt einen EIGENEN AnthropicClient (Haiku), nicht den des Primary-Loops
- PromptCaching wird auch für den Compaction-Call aktiviert
- Keine Rekursion: Wenn die Zusammenfassung selbst zu lang ist, wird sie gekürzt

---

## Integration in AgenticLoop

### Trigger-Logik
```
Nach jeder Iteration:
  if (compaction.Enabled && iteration >= compaction.ThresholdIterations):
    if (iteration % compaction.ThresholdIterations == 0):  // periodisch, nicht nur einmal
      messages = await compactor.CompactAsync(messages, keepRecentMessages)
```

`keepRecentMessages` = `KeepRecentIterations * 2` (jede Iteration = 1 Assistant + 1 User Message)

### Änderungen an AgenticLoop
- Neuer Konstruktor-Parameter: `CompactionConfig compactionConfig, IContextCompactor? compactor`
- Compactor ist nullable (optional, backward-kompatibel)
- Nach Trigger: messages-Liste wird in-place ersetzt
- Logging: "Context compacted: {OldCount} messages → {NewCount} messages"

---

## Backward-Kompatibilität
- CompactionConfig hat sinnvolle Defaults (Enabled=true, Threshold=8)
- Wenn kein Compactor injiziert wird (null), findet keine Komprimierung statt
- AgentConfig bekommt `CompactionConfig Compaction` Property mit `new()` Default
- Bestehende agentsmith.yml funktioniert ohne Änderung
