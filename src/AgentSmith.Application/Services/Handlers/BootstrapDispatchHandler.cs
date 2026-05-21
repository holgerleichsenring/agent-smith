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
/// Reads <c>ContextKeys.AvailableRoles</c>, narrows via
/// <see cref="ActivationSkillFilter"/> against the run-state concepts, and
/// emits exactly one <c>SkillRound</c> command for the matching bootstrap
/// skill. Fails loud on 0 / &gt;1 matches — the catalog is supposed to
/// guarantee one bootstrap skill per <c>project_language</c> enum value.
/// No LLM call: the activates_when expressions on the bootstrap skills
/// (<c>pipeline_name = "init-project" AND project_language = "X"</c>) carry
/// the routing decision deterministically.
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

        var matched = activationFilter.Filter(roles, conceptsFactory(context.Pipeline));
        var projectLanguage = conceptsFactory(context.Pipeline).GetString("project_language");

        if (matched.Count == 0)
        {
            var availableNames = string.Join(", ", roles.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));
            return Task.FromResult(CommandResult.Fail(
                $"BootstrapDispatch: no bootstrap skill matched project_language='{projectLanguage}'. " +
                $"Available skills: [{availableNames}]. " +
                "Either the analyzer emitted a language slug with no producer skill, or the skills " +
                "catalog is missing the matching bootstrap-* skill for that slug."));
        }

        if (matched.Count > 1)
            return Task.FromResult(CommandResult.Fail(
                $"BootstrapDispatch: ambiguous match for project_language='{projectLanguage}' — " +
                $"got {matched.Count} skills: [{string.Join(", ", matched.Select(s => s.Name))}]. " +
                "Bootstrap skills must be 1:1 with project_language enum values."));

        var skill = matched[0];
        logger.LogInformation(
            "BootstrapDispatch: project_language={Lang} → skill={Skill}", projectLanguage, skill.Name);

        // p0130c-followup: route to the producer-loop BootstrapRoundCommand, not
        // SkillRoundCommand. SkillRoundHandlerBase runs an observation-only chat
        // with no tools attached, so the producer can't emit context.yaml /
        // coding-principles.md via WriteFile. BootstrapRoundHandler is the
        // tool-bearing path.
        return Task.FromResult(CommandResult.OkAndContinueWith(
            $"BootstrapDispatch: routing to {skill.Name}",
            PipelineCommand.SkillRound(CommandNames.BootstrapRound, skill.Name, round: 1)));
    }
}
