namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Shared state bag passed between pipeline steps.
/// Each command handler reads from and writes to this context.
/// p0128c adds an opt-in read-gate hook: when PipelineExecutor attaches a
/// gate for the duration of a step, every Get/TryGet consults it and may
/// throw or warn on undeclared reads. Default (no gate attached) is the
/// original free-for-all behavior.
/// </summary>
public sealed class PipelineContext
{
    private readonly Dictionary<string, object> _data = new();
    private readonly object _lock = new();
    private IPipelineContextReadGate? _readGate;

    /// <summary>
    /// Attaches a read gate. Returns an IDisposable that detaches on Dispose so
    /// PipelineExecutor can scope the gate to a single step (using-statement).
    /// Re-attaching while a gate is active throws — reads are flat, not nested.
    /// </summary>
    public IDisposable AttachReadGate(IPipelineContextReadGate gate)
    {
        lock (_lock)
        {
            if (_readGate is not null)
                throw new InvalidOperationException(
                    "A read gate is already attached; existing scope must dispose first.");
            _readGate = gate;
            return new GateScope(this);
        }
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        lock (_lock) _data[key] = value;
    }

    public T Get<T>(string key)
    {
        IPipelineContextReadGate? gate;
        lock (_lock) gate = _readGate;
        gate?.OnRead(key);

        lock (_lock)
        {
            if (!_data.TryGetValue(key, out var value))
                throw new KeyNotFoundException($"Key '{key}' not found in pipeline context.");
            return (T)value;
        }
    }

    public bool TryGet<T>(string key, out T? value)
    {
        IPipelineContextReadGate? gate;
        lock (_lock) gate = _readGate;
        gate?.OnRead(key);

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

    private sealed class GateScope(PipelineContext owner) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._lock) owner._readGate = null;
        }
    }

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
