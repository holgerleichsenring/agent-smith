using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-26-0f19c: one leader lease every replica of a test competes for, as they do for the Redis
/// key — acquire when free, renew and release only with the token that acquired it.
/// </summary>
internal sealed class SharedLeaderLease : IRedisLeaderLease
{
    private readonly Dictionary<string, string> _holders = new(StringComparer.Ordinal);

    public Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        lock (_holders)
        {
            if (_holders.ContainsKey(key)) return Task.FromResult<string?>(null);
            var token = Guid.NewGuid().ToString("N");
            _holders[key] = token;
            return Task.FromResult<string?>(token);
        }
    }

    public Task<bool> RenewAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken)
    {
        lock (_holders) return Task.FromResult(_holders.TryGetValue(key, out var held) && held == token);
    }

    public Task ReleaseAsync(string key, string token, CancellationToken cancellationToken)
    {
        lock (_holders)
            if (_holders.TryGetValue(key, out var held) && held == token) _holders.Remove(key);
        return Task.CompletedTask;
    }

    /// <summary>Another process holds the key.</summary>
    public void HeldElsewhere(string key)
    {
        lock (_holders) _holders[key] = "elsewhere";
    }
}
