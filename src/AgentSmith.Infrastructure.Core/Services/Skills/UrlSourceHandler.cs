using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Pulls the catalog from an explicit URL (custom mirror, internal artefact
/// store). Honours the optional SHA256 verification.
/// </summary>
public sealed class UrlSourceHandler(
    ISkillsRepositoryClient repositoryClient,
    ILogger<UrlSourceHandler> logger) : ISkillsSourceHandler
{
    public SkillsSourceMode Mode => SkillsSourceMode.Url;

    public async Task<string> ResolveAsync(SkillsConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Url))
            throw new InvalidOperationException("skills.url is required when skills.source is 'url'");

        if (!Uri.TryCreate(config.Url, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"skills.url is not a valid URI: {config.Url}");

        await repositoryClient.PullAsync(uri, config.CacheDir, config.Sha256, cancellationToken);
        logger.LogInformation("Pulled skill catalog from custom URL {Url} into {CacheDir}", uri, config.CacheDir);
        return config.CacheDir;
    }
}
