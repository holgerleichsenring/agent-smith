using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// p0281a: disk-backed durable last-good repo set per connection (mirrors DiskProjectMapStore).
/// Survives process restarts so a discovery outage on a cold process resolves from the last
/// successful run instead of failing. Used by both the server and the CLI.
/// </summary>
public sealed class DiskConnectionRepoSnapshotStore(IAgentSmithPaths paths, ILogger<DiskConnectionRepoSnapshotStore> logger)
    : IConnectionRepoSnapshotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<IReadOnlyList<DiscoveredRepo>?> TryGetAsync(
        string connectionName, CancellationToken cancellationToken)
    {
        var file = SnapshotFile(connectionName);
        if (!File.Exists(file)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            return JsonSerializer.Deserialize<List<DiscoveredRepo>>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Failed to read connection repo snapshot at {Path}; treating as cold", file);
            return null;
        }
    }

    // p0345c: repos + when they were captured. DiscoveredAt is the snapshot
    // file's last-write time — the moment the refresher persisted the last
    // successful discovery; no separate timestamp is kept, so the file IS the fact.
    public async Task<ConnectionRepoDiscovery?> TryGetDiscoveryAsync(
        string connectionName, CancellationToken cancellationToken)
    {
        var repos = await TryGetAsync(connectionName, cancellationToken);
        if (repos is null) return null;
        var discoveredAt = File.GetLastWriteTimeUtc(SnapshotFile(connectionName));
        return new ConnectionRepoDiscovery(new DateTimeOffset(discoveredAt, TimeSpan.Zero), repos);
    }

    public async Task SetAsync(
        string connectionName, IReadOnlyList<DiscoveredRepo> repos, CancellationToken cancellationToken)
    {
        var file = SnapshotFile(connectionName);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(repos, JsonOptions), cancellationToken);
    }

    private string SnapshotFile(string connectionName) =>
        Path.Combine(paths.CacheRoot, "connections", $"{Sanitize(connectionName)}.repos.json");

    private static string Sanitize(string name) =>
        string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
}
