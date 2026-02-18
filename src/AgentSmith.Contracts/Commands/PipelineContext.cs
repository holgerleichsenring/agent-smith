namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Shared state bag passed between pipeline steps.
/// Each command handler reads from and writes to this context.
/// </summary>
public sealed class PipelineContext
{
    private readonly Dictionary<string, object> _data = new();

    public void Set<T>(string key, T value) where T : notnull
    {
        _data[key] = value;
    }

    public T Get<T>(string key)
    {
        if (!_data.TryGetValue(key, out var value))
            throw new KeyNotFoundException($"Key '{key}' not found in pipeline context.");

        return (T)value;
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (_data.TryGetValue(key, out var obj) && obj is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public bool Has(string key) => _data.ContainsKey(key);
}
