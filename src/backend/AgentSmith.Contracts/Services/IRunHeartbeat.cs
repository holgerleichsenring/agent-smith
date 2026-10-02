namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-10-02-5f89e: renews the liveness beat on a run's own row. Every run has a row and
/// a driving process; only a ticket run has an ActiveRun lease, so the lease beat left a
/// ticketless run (init) with no liveness signal at all. The server writes the row; the
/// CLI binds the no-op, because only the server projects runs into the database.
/// </summary>
public interface IRunHeartbeat
{
    Task RenewAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// 2026-10-02-75dc: ids of unfinished runs whose row beat (or start) is within
    /// <paramref name="freshFor"/> — the sandbox reapers union them into their live set.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetFreshRunIdsAsync(TimeSpan freshFor, CancellationToken cancellationToken);
}
