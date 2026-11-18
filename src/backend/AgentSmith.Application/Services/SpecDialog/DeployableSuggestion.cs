using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-18-3863g: when a proposal creates a context — one its drafts name that neither exists in the grounded
/// repositories nor carries a deployment binding — in a project whose other contexts are deployable, the line that
/// suggests binding it to the template its siblings use and cutting a make-deployable spec. Nothing is guessed: no
/// grounding, no contexts in the drafts or no deployment binding gives no line.
/// </summary>
public static class DeployableSuggestion
{
    public static string? Of(OutcomeProposal proposal, ResolvedProject? project, IReadOnlyList<ContextDocument>? grounded)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        var bound = project?.Templates.Where(t => t.IsDeployment).ToList() ?? [];
        if (bound.Count == 0 || grounded is null) return null;
        var known = grounded.Select(d => d.ContextName).Concat(bound.Select(b => b.Context))
            .Where(n => !string.IsNullOrWhiteSpace(n)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fresh = ProposalSpecSet.Drafts(proposal).SelectMany(d => d.Contexts)
            .Where(c => !string.IsNullOrWhiteSpace(c) && !known.Contains(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (fresh.Count == 0) return null;
        var names = string.Join(", ", fresh.Select(c => $"`{c}`"));
        var templates = string.Join(" or ", bound.Select(b => $"`{b.TemplateContext}` of `{b.Repo.Name}`").Distinct());
        var siblings = string.Join(", ", bound.Select(b => $"`{b.Context}`").Distinct());
        return $"Deployable: {names} {(fresh.Count == 1 ? "is a new context" : "are new contexts")} without a deployment "
            + $"template, while {siblings} {(bound.Count == 1 ? "is" : "are")} delivered after {templates}. To deliver "
            + $"{(fresh.Count == 1 ? "it" : "them")} the same way, bind {names} to that template in the project's templates "
            + "(kind deployment, with its environments), then cut a spec that makes it deployable.";
    }
}
