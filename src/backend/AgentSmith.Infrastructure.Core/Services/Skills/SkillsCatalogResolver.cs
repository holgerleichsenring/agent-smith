using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Concurrency-gated entry point that keeps the resolved skill catalog current
/// on every call. Delegates to the dispatched source handler, which is itself
/// idempotent and self-healing: <see cref="DefaultSourceHandler"/> re-uses the
/// cached extract when the version marker matches and the files are on disk,
/// and re-pulls when the configured version drifts or the cache is gone. The
/// semaphore serialises concurrent pipeline starts so only one pull runs at a
/// time. (Previously latched to "resolve once per process", which defeated the
/// handler's self-healing — a version-pin change or a wiped cache was ignored
/// for the process lifetime.)
/// </summary>
public sealed class SkillsCatalogResolver(
    IEnumerable<ISkillsSourceHandler> handlers,
    SkillsCatalogPath catalogPath,
    ILogger<SkillsCatalogResolver> logger) : ISkillsCatalogResolver
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<CatalogResolution> EnsureResolvedAsync(SkillsConfig config, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var handler = handlers.FirstOrDefault(h => h.Mode == config.Source)
                ?? throw new InvalidOperationException(
                    $"No SkillsSourceHandler registered for source '{config.Source}'");

            var resolution = await handler.ResolveAsync(config, cancellationToken);
            catalogPath.Set(resolution.Root);
            logger.LogDebug(
                "Skill catalog ready at {Root} (source: {Source}, version: {Version}, fromCache: {FromCache})",
                resolution.Root, config.Source, resolution.Version, resolution.FromCache);
            return resolution;
        }
        finally
        {
            _gate.Release();
        }
    }
}
