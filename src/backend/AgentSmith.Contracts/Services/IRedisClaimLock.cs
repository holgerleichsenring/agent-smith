namespace AgentSmith.Contracts.Services;

/// <summary>
/// Cross-process mutex via Redis SETNX. Returns a token on acquire; ReleaseAsync
/// uses CAS (Lua) so a caller cannot delete a lock whose TTL expired and was
/// subsequently re-acquired by another process.
/// </summary>
public interface IRedisClaimLock
{
    /// <summary>
    /// Attempts to acquire the lock. Returns a non-null token on success, null on failure.
    /// Caller must pass the same token to ReleaseAsync to avoid releasing a foreign lock.
    /// </summary>
    Task<string?> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Releases the lock only if the stored value matches the provided token (CAS).
    /// Silently succeeds if the lock has already expired or been released.
    /// </summary>
    Task ReleaseAsync(string key, string token, CancellationToken cancellationToken);
}
