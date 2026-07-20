namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0353: a monotonic, cross-replica "config changed" epoch. A config write
/// (Studio import / entity CRUD / settings save) lands on ANY replica, but the
/// poller and the settings enforcers run only on the LEADER, so the signal must
/// cross replicas via shared state, not an in-process event. The writer INCRs the
/// epoch; the leader GETs it between cycles and rebuilds in place when it advances.
/// A plain counter (not a diffed stream) makes the compare an atomic INCR/GET and
/// collapses a burst of writes into one rebuild at the newest epoch.
/// </summary>
public interface IConfigReloadSignal
{
    /// <summary>Reads the current epoch (0 if never bumped / no backing store).</summary>
    Task<long> CurrentEpochAsync(CancellationToken cancellationToken);

    /// <summary>Atomically advances the epoch and returns the new value.</summary>
    Task<long> BumpAsync(CancellationToken cancellationToken);
}
