namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-20-24bd: the places runs hold on one model deployment (<see cref="Models.Configuration.ModelRunSlotKey"/>).
/// A place is a row with a unique (key, slot) pair, taken by insert, so two replicas racing for the last place admit
/// exactly one. A run holds at most ONE place, on whichever key it took first, and keeps it until it stops
/// executing. The store also keeps how long each run waited for its place, so neither wall-time clock counts the wait.
/// </summary>
public interface IModelRunSlots
{
    /// <summary>True when <paramref name="runId"/> already holds a place (on any key), or took a free one of the
    /// <paramref name="limit"/> places on <paramref name="key"/> now; false while other runs hold
    /// <paramref name="limit"/> or more places there (a lowered limit admits nobody until enough have left). A true
    /// answer closes the run's open wait in the same store operation, so a lost end-of-wait never keeps one open.</summary>
    Task<bool> AcquireAsync(string key, int limit, string runId, CancellationToken cancellationToken);

    /// <summary>Gives back every place <paramref name="runId"/> holds and forgets the time it waited — it stops
    /// executing, and a resumed run's clocks start again; a run holding none is no error.</summary>
    Task ReleaseAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Records that <paramref name="runId"/> started waiting for a place.</summary>
    Task WaitStartedAsync(string runId, CancellationToken cancellationToken);

    /// <summary>Adds the wait <paramref name="runId"/> just ended to the time it waited.</summary>
    Task WaitEndedAsync(string runId, CancellationToken cancellationToken);

    /// <summary>How long each of <paramref name="runIds"/> has waited for a place, a wait still going on included;
    /// a run that never waited is absent.</summary>
    Task<IReadOnlyDictionary<string, TimeSpan>> WaitedAsync(
        IReadOnlyCollection<string> runIds, CancellationToken cancellationToken);

    /// <summary>Deletes places and waits no executing run can own — its run ended, or is queued or parked and has not
    /// beaten for <paramref name="staleAfter"/>, or has had no row for a generous grace — and wakes this process's
    /// waiters. A running row is never swept: the liveness reaper ends a dead one first. Answers the places freed.</summary>
    Task<int> SweepAsync(TimeSpan staleAfter, CancellationToken cancellationToken) => Task.FromResult(0);
}
