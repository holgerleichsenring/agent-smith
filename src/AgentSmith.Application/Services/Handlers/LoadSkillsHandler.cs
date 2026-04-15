using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads skill role definitions from the configured skills path.
/// Used by repo-less pipelines (api-security-scan) that don't run BootstrapProject.
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
        return Task.FromResult(CommandResult.Ok($"Loaded {roles.Count} skills from {context.SkillsPath}"));
    }

    private string ResolveSkillsDir(LoadSkillsContext context)
    {
        // Try relative to repo root first (if repo is in pipeline context)
        if (context.Pipeline.TryGet<Domain.Entities.Repository>(ContextKeys.Repository, out var repo)
            && repo is not null)
        {
            var repoRelative = Path.Combine(repo.LocalPath, "config", context.SkillsPath);
            if (Directory.Exists(repoRelative))
            {
                logger.LogDebug("Skills path resolved relative to repo: {Dir}", repoRelative);
                return repoRelative;
            }
        }

        // Fallback to CWD-relative
        var cwdRelative = Path.Combine("config", context.SkillsPath);
        logger.LogDebug("Skills path resolved relative to CWD: {Dir}", cwdRelative);
        return cwdRelative;
    }
}
