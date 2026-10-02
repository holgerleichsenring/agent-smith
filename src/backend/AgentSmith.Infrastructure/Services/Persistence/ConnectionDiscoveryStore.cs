using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Persistence;

/// <summary>
/// 2026-10-02-b540: the server's connection discovery, composed of the two stores it has. Redis is
/// what replicas share — the repos and the last attempt's outcome every replica must agree on.
/// The pod's disk keeps the last good list, which must survive a Redis flush or outage: the repos
/// discovered yesterday are still the repos until a discovery says otherwise.
/// <para>
/// A success is written to both; a failure to Redis only, never over the disk list. A read asks
/// Redis and, on a missing key or a Redis that cannot be reached, answers from disk marked
/// <see cref="ConnectionDiscoverySource.Local"/>. A Redis outage never throws out of a read or a
/// write — the glob expander reads synchronously inside a configuration load.
/// </para>
/// </summary>
public sealed class ConnectionDiscoveryStore(
    RedisConnectionRepoSnapshot shared,
    DiskConnectionRepoSnapshotStore local,
    ILogger<ConnectionDiscoveryStore> logger) : IConnectionRepoSnapshot, IConnectionRepoSnapshotStore
{
    public ConnectionRepoSnapshotRead? TryRead(string connectionName)
    {
        if (Guarded(() => shared.TryRead(connectionName), connectionName) is { } read) return read;
        var lastGood = local.TryGetAsync(connectionName, CancellationToken.None).GetAwaiter().GetResult();
        return lastGood is null ? null : new ConnectionRepoSnapshotRead(lastGood, ConnectionDiscoverySource.Local);
    }

    public void Set(string connectionName, IReadOnlyList<DiscoveredRepo> repos)
    {
        // Both stores already hold what the refresher recorded through SetAsync.
    }

    public async Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(string connectionName, CancellationToken cancellationToken) =>
        await GuardedAsync(() => shared.TryGetAsync(connectionName, cancellationToken), connectionName)
        ?? await local.TryGetAsync(connectionName, cancellationToken);

    /// <summary>
    /// Redis's status when it holds a success; otherwise the disk's last success, marked local,
    /// beside whatever attempt Redis recorded since.
    /// </summary>
    public async Task<ConnectionDiscoveryStatus?> TryGetDiscoveryAsync(string connectionName, CancellationToken cancellationToken)
    {
        var sharedStatus = await GuardedAsync(() => shared.TryGetDiscoveryAsync(connectionName, cancellationToken), connectionName);
        if (sharedStatus?.DiscoveredAt is not null) return sharedStatus;
        var localStatus = await local.TryGetDiscoveryAsync(connectionName, cancellationToken);
        if (localStatus?.DiscoveredAt is null) return sharedStatus;
        return localStatus with
        {
            LastAttemptAt = sharedStatus?.LastAttemptAt ?? localStatus.LastAttemptAt,
            LastError = sharedStatus?.LastError,
            Source = ConnectionDiscoverySource.Local,
        };
    }

    public async Task SetAsync(
        string connectionName, IReadOnlyList<DiscoveredRepo> repos, DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        await local.SetAsync(connectionName, repos, discoveredAt, cancellationToken);
        await GuardedAsync(() => shared.SetAsync(connectionName, repos, discoveredAt, cancellationToken), connectionName);
    }

    public Task RecordFailureAsync(
        string connectionName, string error, DateTimeOffset attemptedAt, CancellationToken cancellationToken) =>
        GuardedAsync(() => shared.RecordFailureAsync(connectionName, error, attemptedAt, cancellationToken), connectionName);

    private T? Guarded<T>(Func<T?> redisCall, string connectionName) where T : class
    {
        try
        {
            return redisCall();
        }
        catch (Exception ex) when (IsOutage(ex))
        {
            LogOutage(ex, connectionName);
            return null;
        }
    }

    private async Task<T?> GuardedAsync<T>(Func<Task<T?>> redisCall, string connectionName)
    {
        try
        {
            return await redisCall();
        }
        catch (Exception ex) when (IsOutage(ex))
        {
            LogOutage(ex, connectionName);
            return default;
        }
    }

    private async Task GuardedAsync(Func<Task> redisCall, string connectionName) =>
        await GuardedAsync<object>(async () => { await redisCall(); return null; }, connectionName);

    // A connection failure is a RedisException; a timeout is a TimeoutException, not one.
    private static bool IsOutage(Exception ex) => ex is RedisException or TimeoutException;

    private void LogOutage(Exception ex, string connectionName) => logger.LogWarning(ex,
        "ConnectionDiscoveryStore: Redis did not answer for connection '{Connection}'; this server's last good list stands in.",
        connectionName);
}
