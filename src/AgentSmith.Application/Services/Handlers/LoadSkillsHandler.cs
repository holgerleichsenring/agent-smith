using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads skill role definitions from the configured skills path.
/// Resolves paths relative to the config file directory.
/// </summary>
public sealed class LoadSkillsHandler(
    ISkillLoader skillLoader,
    ILogger<LoadSkillsHandler> logger)
    : ICommandHandler<LoadSkillsContext>
{
    public Task<CommandResult> ExecuteAsync(
        LoadSkillsContext context, CancellationToken cancellationToken)
    {
        var skillsDir = ResolveSkillsDir(context);

        if (!Directory.Exists(skillsDir))
        {
            logger.LogWarning("Skills directory not found: {Dir} (resolved from '{Raw}')",
                skillsDir, context.SkillsPath);
            return Task.FromResult(CommandResult.Ok($"Skills directory not found: {skillsDir}"));
        }

        logger.LogDebug("Loading skills from {Dir}", skillsDir);
        var roles = skillLoader.LoadRoleDefinitions(skillsDir);
        context.Pipeline.Set<IReadOnlyList<RoleSkillDefinition>>(ContextKeys.AvailableRoles, roles);

        logger.LogInformation("Loaded {Count} skill roles from {Dir}", roles.Count, skillsDir);
        return Task.FromResult(CommandResult.Ok($"Loaded {roles.Count} skills from {skillsDir}"));
    }

    private string ResolveSkillsDir(LoadSkillsContext context)
    {
        var skillsPath = context.SkillsPath;

        // 1. If the path exists as-is (absolute or already correct relative), use it
        if (Directory.Exists(skillsPath))
        {
            logger.LogDebug("Skills path exists as-is: {Dir}", skillsPath);
            return skillsPath;
        }

        // 2. Resolve relative to config file directory
        if (context.Pipeline.TryGet<string>(ContextKeys.ConfigDir, out var configDir)
            && !string.IsNullOrEmpty(configDir))
        {
            var configRelative = Path.Combine(configDir, skillsPath);
            if (Directory.Exists(configRelative))
            {
                logger.LogDebug("Skills path resolved relative to config: {Dir}", configRelative);
                return configRelative;
            }
        }

        // 3. Try relative to repo root
        if (context.Pipeline.TryGet<Domain.Entities.Repository>(ContextKeys.Repository, out var repo)
            && repo is not null)
        {
            var repoRelative = Path.Combine(repo.LocalPath, "config", skillsPath);
            if (Directory.Exists(repoRelative))
            {
                logger.LogDebug("Skills path resolved relative to repo: {Dir}", repoRelative);
                return repoRelative;
            }
        }

        // 4. Fallback: CWD/config/
        var cwdRelative = Path.Combine("config", skillsPath);
        logger.LogDebug("Skills path fallback to CWD: {Dir}", cwdRelative);
        return cwdRelative;
    }
}
