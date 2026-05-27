using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.RedisDisabled;

/// <summary>
/// Fallback IRedisLeaderLease for Redis-less CLI runs. Leader election is server-only;
/// CLI commands inject the dep transitively but never call it. TryAcquire returns null
/// (lease not acquired); RenewAsync returns false; ReleaseAsync no-ops.
/// </summary>
public sealed class NullRedisLeaderLease : IRedisLeaderLease
{
    public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
        => Task.FromResult<string?>(null);

    public Task<bool> RenewAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken)
        => Task.FromResult(false);

    public Task ReleaseAsync(string key, string token, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
