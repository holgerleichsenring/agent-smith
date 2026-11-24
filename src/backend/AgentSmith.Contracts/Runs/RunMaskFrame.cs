namespace AgentSmith.Contracts.Runs;

/// <summary>
/// 2026-11-23-933dg: the values one run masks beside the installation's — a PoC project's secret variables — on an
/// <see cref="AsyncLocal{T}"/>, exactly as MaterializingSecrets holds a materialization's secrets: a frame begun on a
/// run's flow is seen by that flow only, so a run of another project, on any thread, sees an empty frame. Nothing shared
/// is mutated; ending a frame restores the enclosing one.
/// </summary>
public sealed class RunMaskFrame
{
    private readonly AsyncLocal<IReadOnlyList<string>?> _current = new();

    /// <summary>This flow's values, longest first; empty outside a frame.</summary>
    public IReadOnlyList<string> Current => _current.Value ?? [];

    public IDisposable Begin(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var previous = _current.Value;
        _current.Value = SecretMasker.Order(values);
        return new Scope(() => _current.Value = previous);
    }

    private sealed class Scope(Action end) : IDisposable
    {
        public void Dispose() => end();
    }
}
