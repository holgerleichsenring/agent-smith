using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0358: gives a skills.version change immediate, visible effect. Runs are
/// already correct (every run resolves the catalog lazily), but a config-studio
/// update produced NO log line and NO pull until the next run — the operator
/// stood in front of a silent log. On a config reload this compares the pinned
/// version against the cache marker, logs the verdict either way, and eagerly
/// pulls when they differ so a broken release (404, bad sha256) surfaces AT
/// UPDATE TIME instead of on the next ticket. Fail-soft: a pull failure is
/// logged and swallowed — the next run retries via the same self-healing path.
/// </summary>
public sealed class SkillsCatalogRefresher(
    ISkillsCatalogResolver resolver,
    ISkillsCacheMarker marker,
    ILogger<SkillsCatalogRefresher> logger)
{
    public async Task RefreshAsync(SkillsConfig skills, CancellationToken cancellationToken)
    {
        if (skills.Source != SkillsSourceMode.Default)
        {
            logger.LogDebug(
                "Config reload: skills.source is {Source} — nothing to refresh (live path)", skills.Source);
            return;
        }

        var cached = marker.Read(skills.CacheDir);
        if (string.Equals(cached, skills.Version, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Config reload: skills.version unchanged ({Version}) — catalog kept", skills.Version);
            return;
        }

        logger.LogInformation(
            "Config reload: skills.version changed {From} -> {To} — pulling now",
            cached ?? "(no cache)", skills.Version);
        await TryPullAsync(skills, cancellationToken);
    }

    private async Task TryPullAsync(SkillsConfig skills, CancellationToken cancellationToken)
    {
        try
        {
            var resolution = await resolver.EnsureResolvedAsync(skills, cancellationToken);
            logger.LogInformation(
                "Skill catalog refreshed to {Version} (fromCache: {FromCache})",
                resolution.Version, resolution.FromCache);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Skill catalog refresh to {Version} failed — the next run retries via lazy resolution",
                skills.Version);
        }
    }
}
