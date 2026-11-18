using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>
/// 2026-11-18-3863f: tells the server what a deploy delivered — the pull request it opened, or the pipeline run it
/// started — on the run's event stream, which is how a step reaches the database. A run without a run id (a CLI run)
/// has nothing to follow it, so nothing is published. 2026-11-18-3863h: with the environments it may promote into.
/// </summary>
public sealed class DeployDeliveredPublisher(IEventPublisher events, TimeProvider clock)
{
    public async Task PublishAsync(DeployPlanInput input, DeployDelivered delivered, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(delivered);
        if (!input.Context.Pipeline.TryGet<string>(ContextKeys.RunId, out var runId) || string.IsNullOrEmpty(runId)) return;
        var request = input.Context.Request!;
        await events.PublishAsync(new DeployDeliveredEvent(runId, clock.GetUtcNow(), input.Context.Project.Name,
            request.Context, request.ContextRepo, request.Environment, delivered.Version, input.Declaration.Delivery,
            delivered.Repo.Name, delivered.Url, delivered.PullRequestUrl, delivered.PipelineRunId, PromotesTo(input)), cancellationToken);
    }

    /// <summary>2026-11-18-3863h: the environments the template promotes from this one that the binding delivers without
    /// approval — the only ones the server may deploy once this one settles green.</summary>
    public static IReadOnlyList<string> PromotesTo(DeployPlanInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var source = input.Context.Request!.Environment;
        return [.. input.Declaration.Environments
            .Where(e => string.Equals(e.PromotesFrom, source, StringComparison.OrdinalIgnoreCase))
            .Where(e => DeploymentTarget.Bound(input.Target.Binding, e.Name) is { Approval: false })
            .Select(e => e.Name)];
    }
}
