using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Pulls the official skill catalog release from the agentsmith-skills repo,
/// caches it under <see cref="SkillsConfig.CacheDir"/>, and re-uses the cached
/// extract on subsequent boots when the version marker matches.
/// </summary>
public sealed class DefaultSourceHandler(
    ISkillsRepositoryClient repositoryClient,
    ISkillsCacheMarker marker,
    ILogger<DefaultSourceHandler> logger) : ISkillsSourceHandler
{
    public SkillsSourceMode Mode => SkillsSourceMode.Default;

    public async Task<string> ResolveAsync(SkillsConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Version))
            throw new InvalidOperationException("skills.version is required when skills.source is 'default'");

        var cached = marker.Read(config.CacheDir);
        if (cached == config.Version && Directory.Exists(Path.Combine(config.CacheDir, "skills")))
        {
            logger.LogInformation(
                "Skill catalog {Version} already cached at {CacheDir} — skipping pull",
                config.Version, config.CacheDir);
            return config.CacheDir;
        }

        var url = repositoryClient.ResolveReleaseUrl(config.Version);
        await repositoryClient.PullAsync(url, config.CacheDir, config.Sha256, cancellationToken);
        marker.Write(config.CacheDir, config.Version);
        logger.LogInformation("Pulled skill catalog {Version} into {CacheDir}", config.Version, config.CacheDir);
        return config.CacheDir;
    }
}
