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
