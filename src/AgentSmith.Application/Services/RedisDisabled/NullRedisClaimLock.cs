using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// Fallback IRedisClaimLock for Redis-less CLI runs. TryAcquireAsync throws — anything that
/// genuinely needs cross-process claim semantics is broken without Redis. ReleaseAsync is a
/// no-op so cleanup paths don't fail unnecessarily. Pipelines that never touch the claim
/// lock (manual security-scan, fix without ticket lifecycle) inject the dep but never call
/// these methods, so resolution succeeds without Redis.
/// </summary>
public sealed class NullRedisClaimLock : IRedisClaimLock
{
    public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
        => throw new RedisUnavailableException("Claim-lock acquisition");

    public Task ReleaseAsync(string key, string token, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
