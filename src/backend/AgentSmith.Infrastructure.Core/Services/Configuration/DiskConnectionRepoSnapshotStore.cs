using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: disk-backed durable last-good repo set per connection (mirrors DiskProjectMapStore).
/// Survives process restarts so a discovery outage on a cold process resolves from the last
/// successful run instead of failing. The CLI's store; the server keeps one Redis hash per
/// connection instead (2026-10-02-5f89c). The outcome of the last attempt lives in a status
/// file beside the repos file.
/// </summary>
public sealed class DiskConnectionRepoSnapshotStore(IAgentSmithPaths paths, ILogger<DiskConnectionRepoSnapshotStore> logger)
    : IConnectionRepoSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(
        string connectionName, CancellationToken cancellationToken) =>
        ReadAsync<IReadOnlyList<DiscoveredRepo>>(SnapshotFile(connectionName), cancellationToken);

    // p0345c: a repos file written before 2026-10-02-5f89c has no status file; its last-write
    // time is when the last success was persisted.
    public async Task<ConnectionDiscoveryStatus?> TryGetDiscoveryAsync(
        string connectionName, CancellationToken cancellationToken)
    {
        var repos = await TryGetAsync(connectionName, cancellationToken);
        var status = await ReadAsync<DiscoveryStatusFile>(StatusFile(connectionName), cancellationToken);
        if (repos is null && status is null) return null;
        var discoveredAt = status?.DiscoveredAt
            ?? (repos is null ? null : new DateTimeOffset(File.GetLastWriteTimeUtc(SnapshotFile(connectionName)), TimeSpan.Zero));
        return new ConnectionDiscoveryStatus(discoveredAt, repos ?? [], status?.LastAttemptAt, status?.LastError);
    }

    public async Task SetAsync(
        string connectionName, IReadOnlyList<DiscoveredRepo> repos, DateTimeOffset discoveredAt,
        CancellationToken cancellationToken)
    {
        await WriteAsync(SnapshotFile(connectionName), repos, cancellationToken);
        await WriteAsync(StatusFile(connectionName), new DiscoveryStatusFile(discoveredAt, discoveredAt, null), cancellationToken);
    }

    public async Task RecordFailureAsync(
        string connectionName, string error, DateTimeOffset attemptedAt, CancellationToken cancellationToken)
    {
        var status = await ReadAsync<DiscoveryStatusFile>(StatusFile(connectionName), cancellationToken);
        await WriteAsync(StatusFile(connectionName),
            new DiscoveryStatusFile(status?.DiscoveredAt, attemptedAt, error), cancellationToken);
    }

    private async Task<T?> ReadAsync<T>(string file, CancellationToken cancellationToken) where T : class
    {
        if (!File.Exists(file)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(file, cancellationToken), JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Failed to read connection discovery file at {Path}; treating as cold", file);
            return null;
        }
    }

    private static async Task WriteAsync<T>(string file, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(value, JsonOptions), cancellationToken);
    }

    private string SnapshotFile(string connectionName) => FileFor(connectionName, "repos");

    private string StatusFile(string connectionName) => FileFor(connectionName, "status");

    private string FileFor(string connectionName, string kind) =>
        Path.Combine(paths.CacheRoot, "connections", $"{Sanitize(connectionName)}.{kind}.json");

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));

    private sealed record DiscoveryStatusFile(DateTimeOffset? DiscoveredAt, DateTimeOffset? LastAttemptAt, string? LastError);
}
