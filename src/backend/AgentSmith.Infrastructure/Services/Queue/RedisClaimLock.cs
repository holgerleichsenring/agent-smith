using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Queue;

/// <summary>
/// Redis SETNX-based cross-process mutex with TTL. Release uses a CAS Lua script
/// so a caller cannot delete a lock whose TTL expired and was re-acquired elsewhere.
/// </summary>
public sealed class RedisClaimLock(
    IConnectionMultiplexer redis,
    ILogger<RedisClaimLock> logger) : IRedisClaimLock
{
    // Lua: delete only if the stored value matches our token.
    private const string ReleaseScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    public async Task<string?> TryAcquireAsync(
        string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var acquired = await db.StringSetAsync(key, token, ttl, When.NotExists);

        if (!acquired)
        {
            logger.LogDebug("Lock {Key} already held", key);
            return null;
        }

        return token;
    }

    public async Task ReleaseAsync(
        string key, string token, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var result = (RedisResult)await db.ScriptEvaluateAsync(
            ReleaseScript,
            [key],
            [token]);

        var deleted = (long)result;
        if (deleted == 0)
        {
            logger.LogDebug(
                "Release no-op for {Key} (already expired or held by different token)", key);
        }
    }
}
