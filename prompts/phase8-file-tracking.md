# Phase 8: File Read Tracker - Deduplizierung

## Überblick
Der FileReadTracker verhindert, dass dieselbe Datei mehrfach vollständig in die
Konversationshistorie eingefügt wird. Wenn eine bereits gelesene Datei erneut
angefragt wird, wird nur ein kurzer Hinweis zurückgegeben.

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

### Änderungen an ReadFile
```
Im ReadFile-Tool:
  1. Datei lesen (wie bisher)
  2. tracker.TrackRead(path)
  3. Wenn dies der ERSTE Read ist: volle Datei zurückgeben
  4. Wenn bereits gelesen: "[File previously read: {path}. Use the content from the earlier read.]"
```

### Warum?
- In einer typischen Agentic Session liest Claude dieselbe Datei oft 3-5 mal
- Jeder Read fügt den vollständigen Dateiinhalt erneut in die History ein
- Bei 10 Iterationen mit je 2-3 File-Reads: 60-80% redundante Tokens
- Der Tracker reduziert dies auf einen einzeiligen Hinweis

### Edge Case: Modifizierte Dateien
Wenn eine Datei per `write_file` modifiziert wurde, MUSS der nächste `read_file`
die neue Version zurückgeben. Lösung:
- ToolExecutor benachrichtigt FileReadTracker bei write_file: `tracker.InvalidateRead(path)`
- Nach Invalidierung wird der nächste Read wieder vollständig zurückgegeben

```csharp
public void InvalidateRead(string filePath)
{
    _readCounts.Remove(filePath);
}
```

---

## Integration in ToolExecutor

### Konstruktor-Änderung
```csharp
public sealed class ToolExecutor(
    string repositoryPath,
    ILogger logger,
    FileReadTracker? fileReadTracker = null)
```

Nullable für Backward-Kompatibilität. Wenn null, wird Deduplizierung übersprungen.

### ReadFile-Änderung
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

### WriteFile-Änderung
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
