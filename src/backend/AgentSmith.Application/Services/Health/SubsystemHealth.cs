using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Health;

/// <summary>
/// Thread-safe mutable ISubsystemHealth. Owners (queue consumer, housekeeping, poller, ...)
/// call SetUp/SetDegraded/SetDown/SetDisabled as their state changes; readers (WebhookListener
/// /health endpoint) read the snapshot through the interface.
/// </summary>
public sealed class SubsystemHealth(string name) : ISubsystemHealth
{
    private readonly object _gate = new();
    private SubsystemState _state = SubsystemState.Down;
    private string? _reason;
    private DateTimeOffset? _lastChangedUtc;

    public string Name { get; } = name;

    public SubsystemState State { get { lock (_gate) return _state; } }

    public string? Reason { get { lock (_gate) return _reason; } }

    public DateTimeOffset? LastChangedUtc { get { lock (_gate) return _lastChangedUtc; } }

    public void SetUp() => Set(SubsystemState.Up, reason: null);

    public void SetDegraded(string reason) => Set(SubsystemState.Degraded, reason);

    public void SetDown(string reason) => Set(SubsystemState.Down, reason);

    public void SetDisabled(string reason) => Set(SubsystemState.Disabled, reason);

    private void Set(SubsystemState state, string? reason)
    {
        lock (_gate)
        {
            if (_state == state && _reason == reason) return;
            _state = state;
            _reason = reason;
            _lastChangedUtc = DateTimeOffset.UtcNow;
        }
    }
}
