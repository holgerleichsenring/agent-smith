using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// p0182: file-backed ProjectMap cache for the CLI flow where Redis isn't
/// available. Mirrors the pre-p0182 disk-pair layout
/// ({cacheDir}/project-map.json + project-map.cache-key) so an operator's
/// existing on-disk cache from before this slice keeps working without
/// migration. RedisProjectMapStore is the default for server runs.
/// </summary>
public sealed class DiskProjectMapStore : IProjectMapStore
{
    private const string MapFileName = "project-map.json";
    private const string CacheKeyFileName = "project-map.cache-key";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IAgentSmithPaths _paths;
    private readonly ILogger<DiskProjectMapStore> _logger;

    public DiskProjectMapStore(IAgentSmithPaths paths, ILogger<DiskProjectMapStore> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<ProjectMap?> TryGetAsync(
        string cacheKeyId, string contentHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cacheKeyId) || string.IsNullOrEmpty(contentHash))
            return null;

        var cacheDir = _paths.ProjectCacheDir(cacheKeyId);
        var keyPath = Path.Combine(cacheDir, CacheKeyFileName);
        if (!File.Exists(keyPath)) return null;

        var storedHash = (await File.ReadAllTextAsync(keyPath, cancellationToken)).Trim();
        if (!string.Equals(storedHash, contentHash, StringComparison.Ordinal)) return null;

        var mapPath = Path.Combine(cacheDir, MapFileName);
        if (!File.Exists(mapPath)) return null;

        var json = await File.ReadAllTextAsync(mapPath, cancellationToken);
        try
        {
            return JsonSerializer.Deserialize<ProjectMap>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize on-disk ProjectMap at {Path}; treating as cache miss", mapPath);
            return null;
        }
    }

    // p0315b: the on-disk layout hashes the cache key into the directory name
    // (AgentSmithPaths.ProjectCacheDir), so a prefix scan is structurally
    // impossible here. Spec-dialog tier-1 grounding is a server (Redis) flow;
    // the CLI store honestly reports "nothing cached" instead of guessing.
    public Task<IReadOnlyList<ProjectMap>> ListByPrefixAsync(
        string cacheKeyPrefix, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Disk ProjectMap store cannot scan by prefix '{Prefix}' (hashed directory names) — returning empty",
            cacheKeyPrefix);
        return Task.FromResult<IReadOnlyList<ProjectMap>>([]);
    }

    public async Task SetAsync(
        string cacheKeyId, string contentHash, ProjectMap map, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cacheKeyId)) return;

        var cacheDir = _paths.ProjectCacheDir(cacheKeyId);
        Directory.CreateDirectory(cacheDir);
        await File.WriteAllTextAsync(
            Path.Combine(cacheDir, MapFileName),
            JsonSerializer.Serialize(map, JsonOptions),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(cacheDir, CacheKeyFileName),
            contentHash,
            cancellationToken);
    }
}
