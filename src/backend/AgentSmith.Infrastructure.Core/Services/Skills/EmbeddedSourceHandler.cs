using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0325: materializes the skills release embedded in the binary to the
/// catalog root (<see cref="SkillsConfig.CacheDir"/>) — an extraction point,
/// not a second read path: <c>SkillCatalogPromptCatalog</c> and downstream
/// loaders keep reading from the same on-disk root as every other source mode.
/// This is the effective default when no explicit skills source is
/// configured; it needs no network access and no version pin.
/// </summary>
public sealed class EmbeddedSourceHandler(
    IEmbeddedSkillsCatalog catalog,
    ICatalogTarballExtractor extractor,
    ISkillsCacheMarker marker,
    ILogger<EmbeddedSourceHandler> logger) : ISkillsSourceHandler
{
    public SkillsSourceMode Mode => SkillsSourceMode.Embedded;

    public Task<CatalogResolution> ResolveAsync(SkillsConfig config, CancellationToken cancellationToken)
    {
        var version = catalog.Version;
        var sourceUrl = $"embedded://agentsmith-skills/{version}";
        var cached = marker.Read(config.CacheDir);
        if (cached == version && Directory.Exists(Path.Combine(config.CacheDir, "skills")))
        {
            logger.LogInformation(
                "Embedded skill catalog {Version} already materialized at {CacheDir} — skipping extraction",
                version, config.CacheDir);
            return Task.FromResult(
                new CatalogResolution(config.CacheDir, version, Mode, sourceUrl, FromCache: true));
        }

        Materialize(config.CacheDir, version);
        return Task.FromResult(
            new CatalogResolution(config.CacheDir, version, Mode, sourceUrl, FromCache: false));
    }

    private void Materialize(string cacheDir, string version)
    {
        try
        {
            using var stream = catalog.Open();
            extractor.Extract(stream, cacheDir);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                $"Cannot write to skill catalog directory '{cacheDir}'. " +
                "Set 'skills.cache_dir' in agentsmith.yml to a writable path " +
                "(default per-user is $HOME/.cache/agentsmith/skills); the " +
                "configured directory needs write permission for the running user.",
                ex);
        }

        marker.Write(cacheDir, version);
        logger.LogInformation(
            "Materialized embedded skill catalog {Version} into {CacheDir}", version, cacheDir);
    }
}
