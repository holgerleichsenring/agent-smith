using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Persistence;

/// <summary>
/// p0182: Redis-backed ProjectMap cache. Replaces the prior container-disk
/// cache so the analyzer's per-repo LLM cost (~$0.40 across a 5-repo project)
/// survives container restarts and docker-compose --force-recreate. One key
/// per sandbox cache id, value is a JSON envelope pairing the validation
/// hash with the serialized map; TTL refreshed on every read+write.
/// </summary>
public sealed class RedisProjectMapStore : IProjectMapStore
{
    private const string KeyNamespace = "agentsmith:projectmap";
    private static readonly TimeSpan Ttl = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisProjectMapStore> _logger;

    public RedisProjectMapStore(
        IConnectionMultiplexer redis,
        ILogger<RedisProjectMapStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<ProjectMap?> TryGetAsync(
        string cacheKeyId, string contentHash, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cacheKeyId) || string.IsNullOrEmpty(contentHash))
            return null;

        var db = _redis.GetDatabase();
        var key = BuildKey(cacheKeyId);
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return null;

        Envelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>((string)value!, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize ProjectMap envelope at {Key}; treating as cache miss", key);
            return null;
        }

        if (envelope is null || !string.Equals(envelope.Hash, contentHash, StringComparison.Ordinal))
            return null;

        await db.KeyExpireAsync(key, Ttl);
        return envelope.Map;
    }

    public async Task SetAsync(
        string cacheKeyId, string contentHash, ProjectMap map, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cacheKeyId)) return;

        var db = _redis.GetDatabase();
        var envelope = new Envelope(contentHash, map);
        var json = JsonSerializer.Serialize(envelope, JsonOptions);
        await db.StringSetAsync(BuildKey(cacheKeyId), json, Ttl);
    }

    private static string BuildKey(string cacheKeyId) => $"{KeyNamespace}:{cacheKeyId}";

    private sealed record Envelope(string Hash, ProjectMap Map);
}
