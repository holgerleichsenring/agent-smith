using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Loads skill role definitions from the configured skills path. Tries the
/// external skill catalog (post-p0103) first since that's where skills now
/// live; falls back to config-dir / repo-root / CWD for dev-from-source
/// setups. The pre-p0103 catalog-blind resolution missed the cache entirely
/// and silently soft-failed with zero skills loaded.
/// </summary>
public sealed class LoadSkillsHandler(
    ISkillLoader skillLoader,
    ISkillsCatalogPath catalogPath,
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

        var vocabulary = skillLoader.LoadVocabulary(skillsDir) ?? ConceptVocabulary.Empty;
        context.Pipeline.Set(ContextKeys.ConceptVocabulary, vocabulary);

        logger.LogInformation(
            "Loaded {Count} skill roles and {VocabCount} concept-vocabulary entries from {Dir}",
            roles.Count, vocabulary.Concepts.Count, skillsDir);
        return Task.FromResult(CommandResult.Ok($"Loaded {roles.Count} skills from {skillsDir}"));
    }

    private string ResolveSkillsDir(LoadSkillsContext context)
    {
        var skillsPath = context.SkillsPath;

        // 1. Absolute path or correct CWD-relative
        if (Directory.Exists(skillsPath))
        {
            logger.LogDebug("Skills path exists as-is: {Dir}", skillsPath);
            return skillsPath;
        }

        // 2. External skill catalog (post-p0103) — typical production path
        try
        {
            var catalogRelative = Path.Combine(catalogPath.Root, skillsPath);
            if (Directory.Exists(catalogRelative))
            {
                logger.LogDebug("Skills path resolved via skill catalog: {Dir}", catalogRelative);
                return catalogRelative;
            }
        }
        catch (InvalidOperationException)
        {
            // Catalog not yet bootstrapped (CLI tooling running before bootstrap).
        }

        // 3. Resolve relative to config file directory
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

        // 4. Try relative to repo root
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

        // 5. Fallback: CWD/config/
        var cwdRelative = Path.Combine("config", skillsPath);
        logger.LogDebug("Skills path fallback to CWD: {Dir}", cwdRelative);
        return cwdRelative;
    }
}
