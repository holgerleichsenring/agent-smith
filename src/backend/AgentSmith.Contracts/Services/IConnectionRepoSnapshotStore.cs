using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0281a: durable per-connection last-good repo set, surviving process restarts so a
/// discovery outage on a cold process still resolves from the last successful run.
/// 2026-10-02-5f89c: it also keeps the outcome of the last attempt. A success writes its repos
/// and time and clears the error; a failure writes only its time and error — so neither can
/// erase the other's half, whichever replica writes last. On the server it is one Redis hash
/// per connection; the CLI keeps files.
/// </summary>
public interface IConnectionRepoSnapshotStore
{
    Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(string connectionName, CancellationToken cancellationToken);

    /// <summary>
    /// p0345c, 2026-10-02-5f89c: the last success and the last attempt — what the config studio
    /// serves. Null when nothing was ever recorded for the connection.
    /// </summary>
    Task<ConnectionDiscoveryStatus?> TryGetDiscoveryAsync(string connectionName, CancellationToken cancellationToken);

    /// <summary>Records a successful discovery: repos and time together, the last error cleared.</summary>
    Task SetAsync(
        string connectionName, IReadOnlyList<DiscoveredRepo> repos, DateTimeOffset discoveredAt,
        CancellationToken cancellationToken);

    /// <summary>2026-10-02-5f89c: records a failed attempt; the last success stays as it was.</summary>
    Task RecordFailureAsync(
        string connectionName, string error, DateTimeOffset attemptedAt, CancellationToken cancellationToken);
}
