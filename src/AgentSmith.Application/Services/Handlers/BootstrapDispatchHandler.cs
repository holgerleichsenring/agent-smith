using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Deterministic dispatcher for the init-project pipeline's bootstrap step.
/// p0158g: iterates ContextKeys.RepoProjectMaps and emits ONE
/// <see cref="CommandNames.BootstrapRound"/> per repo, each scoped to that
/// repo's language via per-iteration mutation of the project_language
/// concept (restored after the last emission). Skill activation reads
/// project_language from the current concept state, so each iteration's
/// ActivationSkillFilter call returns only the bootstrap skill matching
/// that repo's language. Fails loud on 0 / &gt;1 matches per repo.
///
/// Single-repo / legacy fallback: when ContextKeys.RepoProjectMaps is
/// absent, falls back to ContextKeys.ProjectMap (one round with empty
/// repoName, preserves the p0130c single-stack flow).
/// </summary>
public sealed class BootstrapDispatchHandler(
    ActivationSkillFilter activationFilter,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory,
    ILogger<BootstrapDispatchHandler> logger)
    : ICommandHandler<BootstrapDispatchContext>
{
    public Task<CommandResult> ExecuteAsync(
        BootstrapDispatchContext context, CancellationToken cancellationToken)
    {
        if (!context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null || roles.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                "BootstrapDispatch: no available skills loaded. " +
                "Run LoadSkills before BootstrapDispatch."));

        var perRepo = ResolvePerRepoMaps(context.Pipeline);
        if (perRepo is null || perRepo.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                "BootstrapDispatch: no ProjectMap available. " +
                "AnalyzeProject must run before this step."));

        var concepts = conceptsFactory(context.Pipeline);
        var savedLanguage = SafeGetString(concepts, "project_language");
        try
        {
            var commands = new List<PipelineCommand>(perRepo.Count);
            foreach (var (repoName, map) in perRepo)
            {
                var result = TryBuildRoundForRepo(repoName, map, roles, concepts);
                if (!result.Success) return Task.FromResult(result.Failure!);
                commands.Add(result.Command!);
            }
            var description = string.Join(", ", commands.Select(c => $"{c.RepoName}→{c.SkillName}"));
            return Task.FromResult(CommandResult.OkAndContinueWith(
                $"BootstrapDispatch: queued {commands.Count} round(s) [{description}]",
                commands.ToArray()));
        }
        finally
        {
            if (savedLanguage is not null) TrySetString(concepts, "project_language", savedLanguage);
        }
    }

    private (bool Success, PipelineCommand? Command, CommandResult? Failure) TryBuildRoundForRepo(
        string repoName, ProjectMap map, IReadOnlyList<RoleSkillDefinition> roles, IRunStateConcepts concepts)
    {
        var lang = map.PrimaryLanguage?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(lang))
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: repo '{repoName}' has empty PrimaryLanguage — analyzer must emit a slug."));
        TrySetString(concepts, "project_language", lang);

        var matched = activationFilter.Filter(roles, concepts);
        if (matched.Count == 0)
        {
            var availableNames = string.Join(", ", roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: no bootstrap skill matched project_language='{lang}' for repo '{repoName}'. " +
                $"Available skills: [{availableNames}]."));
        }
        if (matched.Count > 1)
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: ambiguous match for project_language='{lang}' (repo '{repoName}') — " +
                $"got {matched.Count} skills: [{string.Join(", ", matched.Select(s => s.Name))}]."));

        var skill = matched[0];
        logger.LogInformation(
            "BootstrapDispatch: repo={Repo} project_language={Lang} → skill={Skill}", repoName, lang, skill.Name);
        return (true, PipelineCommand.SkillRound(
            CommandNames.BootstrapRound, skill.Name, round: 1, repoName: repoName), null);
    }

    // Multi-repo path uses ContextKeys.RepoProjectMaps; single-repo back-compat
    // synthesizes a one-entry dict from ContextKeys.ProjectMap keyed by "".
    private static IReadOnlyDictionary<string, ProjectMap>? ResolvePerRepoMaps(PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var dict) && dict is { Count: > 0 })
            return dict;
        if (pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var single) && single is not null)
            return new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [string.Empty] = single };
        return null;
    }

    private static string? SafeGetString(IRunStateConcepts concepts, string name)
    {
        try { return concepts.GetString(name); }
        catch { return null; }
    }

    private static void TrySetString(IRunStateConcepts concepts, string name, string value)
    {
        try { concepts.SetString(name, value); }
        catch { /* concept not declared in vocab — caller continues */ }
    }
}
