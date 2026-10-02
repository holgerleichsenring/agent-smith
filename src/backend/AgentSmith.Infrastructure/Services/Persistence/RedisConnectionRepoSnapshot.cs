using System.Globalization;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Persistence;

/// <summary>
/// 2026-10-02-5f89c: the server's connection discovery — one Redis hash per connection, no TTL,
/// read by every replica. A Refresh now on one replica is read back on another, and a flushed
/// key is re-discovered at once on the first read instead of after a sweep.
/// 2026-10-02-b540: the shared half of <see cref="ConnectionDiscoveryStore"/>, which serves this
/// server's disk last-good list when the key is missing or Redis cannot be reached.
/// <para>
/// FIELD-WISE WRITES. A success sets repos, discovered_at and last_attempt_at and deletes
/// last_error in one transaction; a failure sets only last_attempt_at and last_error. A slow
/// failure on one replica can therefore never overwrite a newer success on another, and no
/// failure erases the last success.
/// </para>
/// <para>
/// The hash is also the hot snapshot the glob expander reads synchronously, so
/// <see cref="Set"/> has nothing to add: the refresher records every outcome in the store first,
/// and a second write of the repos alone could pair one replica's list with another's time.
/// </para>
/// </summary>
public sealed class RedisConnectionRepoSnapshot(IConnectionMultiplexer redis)
    : IConnectionRepoSnapshot, IConnectionRepoSnapshotStore
{
    private const string KeyNamespace = "agentsmith:discovery";
    private const string ReposField = "repos";
    private const string DiscoveredAtField = "discovered_at";
    private const string LastAttemptAtField = "last_attempt_at";
    private const string LastErrorField = "last_error";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public ConnectionRepoSnapshotRead? TryRead(string connectionName)
    {
        var value = redis.GetDatabase().HashGet(Key(connectionName), ReposField);
        return value.IsNullOrEmpty ? null : new ConnectionRepoSnapshotRead(Repos(value));
    }

    public void Set(string connectionName, IReadOnlyList<DiscoveredRepo> repos)
    {
        // The hash already holds what the refresher recorded; see the class remarks.
    }

    public async Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(string connectionName, CancellationToken cancellationToken)
    {
        var value = await redis.GetDatabase().HashGetAsync(Key(connectionName), ReposField);
        return value.IsNullOrEmpty ? null : Repos(value);
    }

    public async Task<ConnectionDiscoveryStatus?> TryGetDiscoveryAsync(string connectionName, CancellationToken cancellationToken)
    {
        var fields = (await redis.GetDatabase().HashGetAllAsync(Key(connectionName)))
            .ToDictionary(e => e.Name.ToString(), e => e.Value);
        if (fields.Count == 0) return null;
        return new ConnectionDiscoveryStatus(
            Time(fields, DiscoveredAtField),
            fields.TryGetValue(ReposField, out var repos) && !repos.IsNullOrEmpty ? Repos(repos) : [],
            Time(fields, LastAttemptAtField),
            fields.TryGetValue(LastErrorField, out var error) && !error.IsNullOrEmpty ? error.ToString() : null);
    }

    public async Task SetAsync(
        string connectionName, IReadOnlyList<DiscoveredRepo> repos, DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        var key = Key(connectionName);
        var transaction = redis.GetDatabase().CreateTransaction();
        _ = transaction.HashSetAsync(key,
        [
            new HashEntry(ReposField, JsonSerializer.Serialize(repos, JsonOptions)),
            new HashEntry(DiscoveredAtField, discoveredAt.ToString("O")),
            new HashEntry(LastAttemptAtField, discoveredAt.ToString("O")),
        ]);
        _ = transaction.HashDeleteAsync(key, LastErrorField);
        await transaction.ExecuteAsync();
    }

    public Task RecordFailureAsync(
        string connectionName, string error, DateTimeOffset attemptedAt, CancellationToken cancellationToken) =>
        redis.GetDatabase().HashSetAsync(Key(connectionName),
        [
            new HashEntry(LastAttemptAtField, attemptedAt.ToString("O")),
            new HashEntry(LastErrorField, error),
        ]);

    private static string Key(string connectionName) => $"{KeyNamespace}:{connectionName.ToLowerInvariant()}";

    private static IReadOnlyList<DiscoveredRepo> Repos(RedisValue json) =>
        JsonSerializer.Deserialize<List<DiscoveredRepo>>(json.ToString(), JsonOptions) ?? [];

    private static DateTimeOffset? Time(IReadOnlyDictionary<string, RedisValue> fields, string field) =>
        fields.TryGetValue(field, out var value) && DateTimeOffset.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}
