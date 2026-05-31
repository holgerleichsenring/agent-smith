using AgentSmith.Domain.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0182: cache backend for per-sandbox ProjectMap entries. Implementations
/// pair the stored map with a caller-computed content hash; a read returns
/// the map only when the stored hash matches the requested one. The caller
/// owns invalidation semantics (the hash is whatever they want to validate
/// the cached map against — typically a digest of the sandbox file
/// inventory). The store stays dumb.
/// </summary>
public interface IProjectMapStore
{
    /// <summary>
    /// Returns the cached <see cref="ProjectMap"/> for <paramref name="cacheKeyId"/>
    /// when the stored content hash matches <paramref name="contentHash"/>;
    /// otherwise returns null. Implementations may refresh storage-level TTL
    /// on a successful read.
    /// </summary>
    Task<ProjectMap?> TryGetAsync(string cacheKeyId, string contentHash, CancellationToken cancellationToken);

    /// <summary>
    /// Persists <paramref name="map"/> under <paramref name="cacheKeyId"/> with
    /// the validation hash <paramref name="contentHash"/>. Overwrites any prior
    /// entry. Implementations may set or refresh storage-level TTL.
    /// </summary>
    Task SetAsync(string cacheKeyId, string contentHash, ProjectMap map, CancellationToken cancellationToken);
}
