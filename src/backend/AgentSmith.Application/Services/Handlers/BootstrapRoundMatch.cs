using AgentSmith.Application.Services.Activation;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// One component's producer round: the bootstrap skill its language activates, and the
/// <see cref="CommandNames.BootstrapRound"/> command that runs it — or the refusal naming
/// what was and was not available.
/// <para>
/// 2026-09-23-4711 extracted it from <see cref="BootstrapDispatchHandler"/>, which sits at
/// its file-length baseline. Matching one component to its producer is a job of its own;
/// the dispatcher's job is what the fan-out consists of and what follows it.
/// </para>
/// </summary>
public sealed class BootstrapRoundMatch(
    ActivationSkillFilter activationFilter, ILogger<BootstrapRoundMatch> logger)
{
    public (bool Success, PipelineCommand? Command, CommandResult? Failure) For(
        string repoName, DiscoveredComponent component,
        IReadOnlyList<RoleSkillDefinition> roles, IRunStateConcepts concepts)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(roles);
        var lang = component.Language.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(lang))
            return (false, null, CommandResult.Fail(
                $"BootstrapDispatch: repo '{repoName}' component '{component.Name}' has empty language — discovery must emit a slug."));
        TrySetString(concepts, "project_language", lang);

        // p0175-fix: only consider skills with output_schema=bootstrap.
        // Without this filter, project-discovery (output_schema=discovery)
        // competes with project-bootstrap (output_schema=bootstrap) on
        // the same project_language predicate and produces an ambiguous
        // match. BootstrapDiscoverHandler already does the same scoping
        // for the discovery phase (line 133).
        var bootstrapRoles = roles.Where(r => r.OutputSchema == "bootstrap").ToList();
        var matched = activationFilter.Filter(bootstrapRoles, concepts);
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

    internal static void TrySetString(IRunStateConcepts concepts, string name, string value)
    {
        try { concepts.SetString(name, value); }
        catch { /* concept not declared in vocab — caller continues */ }
    }
}
