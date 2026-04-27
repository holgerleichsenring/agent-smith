namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Shared state bag passed between pipeline steps.
/// Each command handler reads from and writes to this context.
/// </summary>
public sealed class PipelineContext
{
    private readonly Dictionary<string, object> _data = new();
    private readonly object _lock = new();

    public void Set<T>(string key, T value) where T : notnull
    {
        lock (_lock) _data[key] = value;
    }

    public T Get<T>(string key)
    {
        lock (_lock)
        {
            if (!_data.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found in pipeline context.");
            return (T)value;
        }
    }

    public bool TryGet<T>(string key, out T? value)
    {
        lock (_lock)
        {
            if (_data.TryGetValue(key, out var obj) && obj is T typed)
            {
                value = typed;
                return true;
            }
            value = default;
            return false;
        }
    }

    public bool Has(string key) { lock (_lock) return _data.ContainsKey(key); }

    /// <summary>
    /// Tracks a command execution in the execution trail.
    /// </summary>
    public void TrackCommand(
        string commandName, bool success, string message,
        TimeSpan duration, int? insertedCount)
    {
        var trail = TryGet<List<ExecutionTrailEntry>>(ContextKeys.ExecutionTrail, out var existing)
            ? existing!
            : [];

        string? skill = TryGet<string>(ContextKeys.ActiveSkill, out var s) ? s : null;

        trail.Add(new ExecutionTrailEntry(
            commandName, skill, success, message,
            DateTimeOffset.UtcNow, duration, insertedCount));

        Set(ContextKeys.ExecutionTrail, trail);
    }
}
