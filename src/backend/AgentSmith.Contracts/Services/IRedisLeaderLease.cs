namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single-leader election via Redis SETNX + TTL. Holder must renew periodically;
/// renewal is CAS-checked so a process that lost the lease (e.g. after a GC pause)
/// can't accidentally extend another holder's lock.
/// </summary>
public interface IRedisLeaderLease
{
    /// <summary>Attempts to acquire the lease. Returns a token on success.</summary>
    Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Extends the TTL only if the stored value matches our token.</summary>
    Task<bool> RenewAsync(string key, string token, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Releases the lease only if we still hold it (CAS delete).</summary>
    Task ReleaseAsync(string key, string token, CancellationToken cancellationToken);
}
