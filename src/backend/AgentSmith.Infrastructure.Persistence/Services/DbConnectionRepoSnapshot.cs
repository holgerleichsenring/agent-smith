using System.Collections.Concurrent;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Persistence.Services;

/// <summary>
/// 2026-10-02-5ab2a: the server's connection discovery — one database row per connection, read
/// by every replica, so a Redis flush or restart loses no repos and no last error.
/// <para>
/// The glob expander reads <see cref="TryGet"/> synchronously on the config path, which cannot
/// afford a database round trip per expansion: a row read is cached in this process for
/// 30 seconds. <see cref="Set"/> refreshes this process's entry at once, so a replica sees its
/// own refresh immediately and every other replica within 30 seconds. A missing row, and a
/// failure-only row with no repos, read as a miss and are not cached, so the expander's
/// blocking first refresh stays.
/// </para>
/// </summary>
public sealed class DbConnectionRepoSnapshot(IServiceScopeFactory scopeFactory, TimeProvider clock)
    : IConnectionRepoSnapshot, IConnectionRepoSnapshotStore
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly ConcurrentDictionary<string, (IReadOnlyList<DiscoveredRepo> Repos, DateTimeOffset CachedAt)> _cache =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string connectionName, out IReadOnlyList<DiscoveredRepo> repos)
    {
        if (_cache.TryGetValue(connectionName, out var hit) && clock.GetUtcNow() - hit.CachedAt < FreshFor)
        {
            repos = hit.Repos;
            return true;
        }

        var row = InScope(r => r.Find(connectionName));
        if (row?.ReposJson is null)
        {
            _cache.TryRemove(connectionName, out _);
            repos = [];
            return false;
        }

        repos = Repos(row.ReposJson);
        Set(connectionName, repos);
        return true;
    }

    public void Set(string connectionName, IReadOnlyList<DiscoveredRepo> repos) =>
        _cache[connectionName] = (repos, clock.GetUtcNow());

    public async Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(string connectionName, CancellationToken cancellationToken)
    {
        var row = await InScopeAsync(r => r.FindAsync(connectionName, cancellationToken));
        return row?.ReposJson is null ? null : Repos(row.ReposJson);
    }

    public async Task<ConnectionDiscoveryStatus?> TryGetDiscoveryAsync(string connectionName, CancellationToken cancellationToken)
    {
        var row = await InScopeAsync(r => r.FindAsync(connectionName, cancellationToken));
        return row is null ? null : Status(row);
    }

    public Task SetAsync(
        string connectionName, IReadOnlyList<DiscoveredRepo> repos, DateTimeOffset discoveredAt,
        CancellationToken cancellationToken) =>
        InScopeAsync(r => r.RecordSuccessAsync(
            connectionName, JsonSerializer.Serialize(repos, JsonOptions), discoveredAt, cancellationToken));

    public Task RecordFailureAsync(
        string connectionName, string error, DateTimeOffset attemptedAt, CancellationToken cancellationToken) =>
        InScopeAsync(r => r.RecordFailureAsync(connectionName, error, attemptedAt, cancellationToken));

    private static ConnectionDiscoveryStatus Status(ConnectionDiscovery row) => new(
        row.DiscoveredAt, row.ReposJson is null ? [] : Repos(row.ReposJson), row.LastAttemptAt, row.LastError);

    private static IReadOnlyList<DiscoveredRepo> Repos(string json) =>
        JsonSerializer.Deserialize<List<DiscoveredRepo>>(json, JsonOptions) ?? [];

    private T InScope<T>(Func<ConnectionDiscoveryRepository, T> operation)
    {
        using var scope = scopeFactory.CreateScope();
        return operation(scope.ServiceProvider.GetRequiredService<ConnectionDiscoveryRepository>());
    }

    private async Task<T> InScopeAsync<T>(Func<ConnectionDiscoveryRepository, Task<T>> operation)
    {
        using var scope = scopeFactory.CreateScope();
        return await operation(scope.ServiceProvider.GetRequiredService<ConnectionDiscoveryRepository>());
    }

    private async Task InScopeAsync(Func<ConnectionDiscoveryRepository, Task> operation)
    {
        using var scope = scopeFactory.CreateScope();
        await operation(scope.ServiceProvider.GetRequiredService<ConnectionDiscoveryRepository>());
    }
}
