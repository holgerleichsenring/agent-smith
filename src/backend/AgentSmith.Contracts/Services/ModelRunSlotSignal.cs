using System.Collections.Concurrent;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-20-24bd: what this process knows about model places beside the store — which run it saw take its one
/// place and when the store last confirmed it, so a later call of the same run costs no query, and a wake for the
/// runs waiting here when a place is given back. A release on another replica is not seen; the waiters' poll and the
/// holders' re-check cover it. One per process.
/// </summary>
public sealed class ModelRunSlotSignal
{
    private readonly ConcurrentDictionary<string, (string Key, DateTimeOffset ConfirmedAt)> _held = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _entering = new(StringComparer.Ordinal);
    private TaskCompletionSource _released = NewRelease();

    /// <summary>The one door a run takes its place through, so its sub-agents starting at once insert one row. It is
    /// dropped by the run's own release on its way out of execution (<see cref="Released"/>), never by a sweep or a
    /// delete (<see cref="Forgotten"/>), which can happen while the run's calls still use it. Should a door be dropped
    /// early anyway, the store still gives a run one place: a second taker of the same run finds the first's row.</summary>
    public SemaphoreSlim Entering(string runId) => _entering.GetOrAdd(runId, _ => new SemaphoreSlim(1, 1));

    /// <summary>The key <paramref name="runId"/> holds its place on, and when the store last confirmed it; null when
    /// this process knows of none.</summary>
    public (string Key, DateTimeOffset ConfirmedAt)? HeldBy(string runId) =>
        _held.TryGetValue(runId, out var held) ? held : null;

    /// <summary>Whether this process knows <paramref name="runId"/> holds its place on <paramref name="key"/>.</summary>
    public bool Holds(string runId, string key) => HeldBy(runId)?.Key == key;

    /// <summary>Records that the store confirmed <paramref name="runId"/>'s place on <paramref name="key"/> at
    /// <paramref name="at"/>.</summary>
    public void Held(string runId, string key, DateTimeOffset at) => _held[runId] = (key, at);

    /// <summary>The run let its place go on its way out of execution: forget the hold and the door, wake every waiter.</summary>
    public void Released(string runId)
    {
        _entering.TryRemove(runId, out _);
        Forgotten(runId);
    }

    /// <summary>A sweep or a delete removed the run's place: forget the hold (the run's next call takes a place again)
    /// and wake every waiter; the door stays, the run may still be calling.</summary>
    public void Forgotten(string runId)
    {
        _held.TryRemove(runId, out _);
        Interlocked.Exchange(ref _released, NewRelease()).TrySetResult();
    }

    /// <summary>Completes at the next <see cref="Released"/> in this process.</summary>
    public Task NextRelease => Volatile.Read(ref _released).Task;

    private static TaskCompletionSource NewRelease() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
