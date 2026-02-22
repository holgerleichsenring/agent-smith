namespace AgentSmith.Infrastructure.Services.Providers.Agent;

/// <summary>
/// Tracks which files have been read during an agentic session.
/// Enables deduplication: subsequent reads of unchanged files return a short
/// reference instead of the full content, saving significant context tokens.
/// </summary>
public sealed class FileReadTracker
{
    private readonly Dictionary<string, int> _readCounts = new(StringComparer.OrdinalIgnoreCase);

    public bool HasBeenRead(string filePath) => _readCounts.ContainsKey(filePath);

    public void TrackRead(string filePath)
    {
        _readCounts.TryGetValue(filePath, out var count);
        _readCounts[filePath] = count + 1;
    }

    public void InvalidateRead(string filePath) => _readCounts.Remove(filePath);

    public int GetReadCount(string filePath) => _readCounts.GetValueOrDefault(filePath);

    public IReadOnlyCollection<string> GetAllReadFiles() => _readCounts.Keys;
}
