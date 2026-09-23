using AgentSmith.Application.Models;
using AgentSmith.Contracts.Activation;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

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
///
/// 2026-09-23-4711: the fan-out is followed by ONE
/// <see cref="CommandNames.BootstrapRetire"/>, so the tree is made to match the
/// derived set once every round has written.
/// </summary>
public sealed class BootstrapDispatchHandler(
    BootstrapRoundMatch roundMatch,
    Func<PipelineContext, IRunStateConcepts> conceptsFactory)
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
                    var result = roundMatch.For(repoName, component, roles, concepts);
                    if (!result.Success) return Task.FromResult(result.Failure!);
                    commands.Add(result.Command!);
                }
            }
            var description = string.Join(", ", commands.Select(c =>
                $"{c.RepoName}/{c.ContextName}→{c.SkillName}"));
            var message = $"BootstrapDispatch: queued {commands.Count} round(s) [{description}]";
            // 2026-09-23-4711: LAST. What the repository declares and this derivation did not
            // produce is retired from what the rounds ACTUALLY wrote — and a round that fails
            // stops the pipeline here, so a half-written derivation retires nothing.
            commands.Add(PipelineCommand.Simple(CommandNames.BootstrapRetire));
            return Task.FromResult(CommandResult.OkAndContinueWith(message, commands.ToArray()));
        }
        finally
        {
            if (savedLanguage is not null)
                BootstrapRoundMatch.TrySetString(concepts, "project_language", savedLanguage);
        }
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
}
