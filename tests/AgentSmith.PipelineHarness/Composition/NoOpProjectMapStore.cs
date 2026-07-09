using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>
/// p0199b: keeps every test isolated by skipping the Redis-backed
/// ProjectMap cache. Without this a prior test's stale (empty) ProjectMap
/// hides the StubProjectAnalyzer's canned analysis on subsequent runs.
/// </summary>
internal sealed class NoOpProjectMapStore : IProjectMapStore
{
    public Task<ProjectMap?> TryGetAsync(
        string cacheKeyId, string contentHash, CancellationToken cancellationToken) =>
        Task.FromResult<ProjectMap?>(null);

    public Task SetAsync(
        string cacheKeyId, string contentHash, ProjectMap map, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ProjectMap>> ListByPrefixAsync(
        string cacheKeyPrefix, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProjectMap>>([]);
}
