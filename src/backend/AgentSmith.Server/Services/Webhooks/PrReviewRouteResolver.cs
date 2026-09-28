using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// p0167a: resolves which (project, repo, pipeline) a pr-opened / pr-synchronize
/// event routes to. Shared by the three platform PrEvent webhook handlers so the
/// routing rule lives in one place: the event's repo URL must match a configured
/// project repo (unconfigured repos never trigger), the pipeline defaults to
/// pr-review, and the matched project's platform trigger may override the route
/// per PR via pipeline_from_label (the operator's opt-out lever — a mapped PR
/// label wins over the default, keys checked in config order per p0072).
/// </summary>
public sealed class PrReviewRouteResolver(IConfiguredRepoFinder repoFinder)
{
    public const string DefaultPipeline = "pr-review";

    public PrReviewRoute? Resolve(
        AgentSmithConfig config, string platformKind, string repoUrl,
        IReadOnlyList<string> prLabels)
    {
        var match = repoFinder.Find(config, repoUrl);
        if (match is null) return null;

        var trigger = TriggerSelectionHelper.ByKind(match.Project, platformKind);
        var pipeline = ResolveLabelOverride(trigger, prLabels) ?? DefaultPipeline;
        return new PrReviewRoute(match.ProjectName, match.Repo.Name, pipeline);
    }

    private static string? ResolveLabelOverride(
        WebhookTriggerConfig? trigger, IReadOnlyList<string> prLabels)
    {
        if (trigger?.PipelineFromLabel is not { Count: > 0 } map) return null;
        foreach (var (label, pipeline) in map)
            if (prLabels.Contains(label, StringComparer.OrdinalIgnoreCase))
                return pipeline;
        return null;
    }
}
