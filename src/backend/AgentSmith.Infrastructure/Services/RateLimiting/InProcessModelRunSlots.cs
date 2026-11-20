using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-20-24bd: the model places of a host without a database (the CLI, a test composition) — the same
/// contract as the database store, held in this process. The server replaces it with the database store, which
/// holds the bound across replicas.
/// </summary>
public sealed class InProcessModelRunSlots(ModelRunSlotSignal signal, TimeProvider? clock = null) : IModelRunSlots
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Dictionary<int, string>> _places = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (DateTimeOffset? Since, TimeSpan Waited)> _waits = new(StringComparer.Ordinal);

    public Task<bool> AcquireAsync(string key, int limit, string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(runId);
        lock (_gate)
        {
            if (!_places.Values.Any(held => held.ContainsValue(runId))) // one place per run
            {
                if (!_places.TryGetValue(key, out var places)) _places[key] = places = [];
                if (places.Count >= limit) return Task.FromResult(false);
                places[Enumerable.Range(0, limit).First(i => !places.ContainsKey(i))] = runId;
            }
            if (_waits.ContainsKey(runId)) _waits[runId] = (null, WaitedNow(runId)); // a place ends the wait
            return Task.FromResult(true);
        }
    }

    public Task ReleaseAsync(string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);
        lock (_gate)
        {
            foreach (var held in _places.Values)
                foreach (var slot in held.Where(p => p.Value == runId).Select(p => p.Key).ToList())
                    held.Remove(slot);
            _waits.Remove(runId);
        }
        signal.Released(runId);
        return Task.CompletedTask;
    }

    public Task WaitStartedAsync(string runId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var wait = _waits.GetValueOrDefault(runId);
            _waits[runId] = (wait.Since ?? _clock.GetUtcNow(), wait.Waited);
        }
        return Task.CompletedTask;
    }

    public Task WaitEndedAsync(string runId, CancellationToken cancellationToken)
    {
        lock (_gate) _waits[runId] = (null, WaitedNow(runId));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, TimeSpan>> WaitedAsync(
        IReadOnlyCollection<string> runIds, CancellationToken cancellationToken)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyDictionary<string, TimeSpan>>(
                runIds.Where(_waits.ContainsKey).Distinct().ToDictionary(id => id, WaitedNow, StringComparer.Ordinal));
    }

    private TimeSpan WaitedNow(string runId)
    {
        var wait = _waits.GetValueOrDefault(runId);
        return wait.Waited + (wait.Since is { } since ? _clock.GetUtcNow() - since : TimeSpan.Zero);
    }
}
