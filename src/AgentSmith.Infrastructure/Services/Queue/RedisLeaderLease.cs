using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AgentSmith.Infrastructure.Services.Queue;

/// <summary>
/// Redis SETNX+TTL leader lease with CAS renewal and release. Two scripts:
/// renewal extends PEXPIRE only if stored value matches token; release deletes
/// only on match. Both prevent a process that lost the lease from affecting
/// the new holder.
/// </summary>
public sealed class RedisLeaderLease(
    IConnectionMultiplexer redis,
    ILogger<RedisLeaderLease> logger) : IRedisLeaderLease
{
    private const string RenewScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('PEXPIRE', KEYS[1], ARGV[2]) else return 0 end";

    private const string ReleaseScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    public async Task<string?> TryAcquireAsync(
        string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var token = Guid.NewGuid().ToString("N");
        var ok = await redis.GetDatabase().StringSetAsync(key, token, ttl, When.NotExists);
        if (ok) logger.LogInformation("Leader lease acquired for {Key} (token: {Token})", key, token);
        return ok ? token : null;
    }

    public async Task<bool> RenewAsync(
        string key, string token, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var result = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            RenewScript, [key], [token, (long)ttl.TotalMilliseconds]);
        if (result == 0)
            logger.LogWarning("Leader lease renewal failed for {Key} — lost to another holder", key);
        return result == 1;
    }

    public async Task ReleaseAsync(
        string key, string token, CancellationToken cancellationToken)
    {
        var deleted = (long)await redis.GetDatabase().ScriptEvaluateAsync(
            ReleaseScript, [key], [token]);
        if (deleted == 1)
            logger.LogInformation("Leader lease released for {Key}", key);
    }
}
