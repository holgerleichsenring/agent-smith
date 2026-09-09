using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// 2026-09-09-6a77: the ProjectMap cache for a harness run, held in that harness's own
/// memory. It replaces the production DiskProjectMapStore (fast tier) and the Redis one
/// (docker tier) for the same reason in both: the cache key is the context name, so every
/// preset over the shared fixture — "default", "primary", "secondary" — addressed ONE
/// entry, and the presets do not agree on an analyzer. Whichever test missed first wrote
/// the map that decided the verify commands for all the others, and the fast tier wrote
/// that entry into the machine's real cache root.
/// <para>
/// In-memory rather than a no-op so the caching PATH still runs — a second context in one
/// sandbox hits, exactly as in production — while nothing outlives the harness that
/// produced it.
/// </para>
/// </summary>
internal sealed class HarnessProjectMapStore : IProjectMapStore
{
    private readonly ConcurrentDictionary<string, (string Hash, ProjectMap Map)> _entries = new(StringComparer.Ordinal);

    public Task<ProjectMap?> TryGetAsync(
        string cacheKeyId, string contentHash, CancellationToken cancellationToken) =>
        Task.FromResult(
            _entries.TryGetValue(cacheKeyId, out var entry)
            && string.Equals(entry.Hash, contentHash, StringComparison.Ordinal)
                ? entry.Map
                : null);

    public Task SetAsync(
        string cacheKeyId, string contentHash, ProjectMap map, CancellationToken cancellationToken)
    {
        _entries[cacheKeyId] = (contentHash, map);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProjectMap>> ListByPrefixAsync(
        string cacheKeyPrefix, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectMap>>(
            [.. _entries
                .Where(e => e.Key.StartsWith(cacheKeyPrefix, StringComparison.Ordinal))
                .Select(e => e.Value.Map)]);
}
