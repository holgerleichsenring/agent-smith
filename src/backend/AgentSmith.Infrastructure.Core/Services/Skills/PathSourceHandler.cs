using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// Operator-managed catalog: validates that a pre-mounted directory exists and
/// contains the expected <c>skills/</c> subtree. No download.
/// </summary>
public sealed class PathSourceHandler(ILogger<PathSourceHandler> logger) : ISkillsSourceHandler
{
    public SkillsSourceMode Mode => SkillsSourceMode.Path;

    public Task<string> ResolveAsync(SkillsConfig config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.Path))
            throw new InvalidOperationException("skills.path is required when skills.source is 'path'");

        if (!Directory.Exists(config.Path))
            throw new DirectoryNotFoundException(
                $"skills.path directory does not exist: {config.Path}");

        var skillsSub = Path.Combine(config.Path, "skills");
        if (!Directory.Exists(skillsSub))
            throw new DirectoryNotFoundException(
                $"skills.path must contain a 'skills/' subdirectory: {config.Path}");

        logger.LogInformation("Using mounted skill catalog at {Path}", config.Path);
        return Task.FromResult(config.Path);
    }
}
