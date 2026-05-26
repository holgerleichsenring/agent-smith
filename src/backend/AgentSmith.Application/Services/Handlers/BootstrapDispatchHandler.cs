using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Deterministic dispatcher for the init-project pipeline's bootstrap step.
/// p0161d: iterates <see cref="ContextKeys.DiscoveredComponents"/> (populated
/// by BootstrapDiscoverHandler — cold-init or re-init projection) and emits
/// ONE <see cref="CommandNames.BootstrapRound"/> per (repo, component). Each
/// command carries RepoName + ContextName + Workdir + the matched skill name.
/// Per-iteration concept mutation of <c>project_language</c> drives skill
/// activation; the pre-dispatch value is restored in a finally so later
/// steps don't read a stale per-component slug.
///
/// Refuses to emit any round when BootstrapDiscoverHandler set
/// <see cref="ContextKeys.DiscoveryAmbiguous"/> — the pipeline fails loud with
/// the structured ambiguity message so the operator re-runs interactively
/// (CLI) for ask_human disambiguation.
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
        if (context.Pipeline.TryGet<string>(ContextKeys.DiscoveryAmbiguous, out var ambiguous)
            && !string.IsNullOrEmpty(ambiguous))
            return Task.FromResult(CommandResult.Fail(ambiguous));

        if (!context.Pipeline.TryGet<IReadOnlyList<RoleSkillDefinition>>(
                ContextKeys.AvailableRoles, out var roles) || roles is null || roles.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                "BootstrapDispatch: no available skills loaded. " +
                "Run LoadSkills before BootstrapDispatch."));

        var perRepo = ResolveDiscoveredComponents(context.Pipeline);
        if (perRepo is null || perRepo.Count == 0)
            return Task.FromResult(CommandResult.Fail(
                "BootstrapDispatch: no DiscoveredComponents available. " +
                "BootstrapDiscover must run before this step."));

        var concepts = conceptsFactory(context.Pipeline);
        var savedLanguage = SafeGetString(concepts, "project_language");
        try
        {
            var commands = new List<PipelineCommand>();
            foreach (var (repoName, components) in perRepo)
            {
                foreach (var component in components)
                {
                    var result = TryBuildRound(repoName, component, roles, concepts);
                    if (!result.Success) return Task.FromResult(result.Failure!);
                    commands.Add(result.Command!);
                }
            }
            var description = string.Join(", ", commands.Select(c =>
                $"{c.RepoName}/{c.ContextName}→{c.SkillName}"));
            return Task.FromResult(CommandResult.OkAndContinueWith(
                $"BootstrapDispatch: queued {commands.Count} round(s) [{description}]",
                commands.ToArray()));
        }
        finally
        {
            if (savedLanguage is not null) TrySetString(concepts, "project_language", savedLanguage);
        }
    }

    private (bool Success, PipelineCommand? Command, CommandResult? Failure) TryBuildRound(
        string repoName, DiscoveredComponent component,
        IReadOnlyList<RoleSkillDefinition> roles, IRunStateConcepts concepts)
    {
        var lang = component.Language.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lang))
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: repo '{repoName}' component '{component.Name}' has empty language — discovery must emit a slug."));
        TrySetString(concepts, "project_language", lang);

        var matched = activationFilter.Filter(roles, concepts);
        if (matched.Count == 0)
        {
            var availableNames = string.Join(", ", roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: no bootstrap skill matched project_language='{lang}' for repo '{repoName}' " +
                $"component '{component.Name}'. Available skills: [{availableNames}]."));
        }
        if (matched.Count > 1)
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: ambiguous match for project_language='{lang}' (repo '{repoName}' " +
                $"component '{component.Name}') — got {matched.Count} skills: [{string.Join(", ", matched.Select(s => s.Name))}]."));

        var skill = matched[0];
        logger.LogInformation(
            "BootstrapDispatch: repo={Repo} context={Context} workdir={Workdir} project_language={Lang} → skill={Skill}",
            repoName, component.Name, component.Workdir, lang, skill.Name);
        return (true, PipelineCommand.SkillRound(
            CommandNames.BootstrapRound, skill.Name, round: 1,
            repoName: repoName, contextName: component.Name, workdir: component.Workdir), null);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>? ResolveDiscoveredComponents(
        PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, IReadOnlyList<DiscoveredComponent>>>(
                ContextKeys.DiscoveredComponents, out var dict) && dict is { Count: > 0 })
            return dict;
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
