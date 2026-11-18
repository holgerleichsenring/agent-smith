using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863e: the pipeline planned and started. A redeploy of the version an environment runs starts it
/// again: a person asked for it, and the pipeline, not a manifest, is what delivers. 2026-11-18-3863f: a started run is
/// published for the server to follow (one the host did not name is published too, and recorded as not followable).</summary>
public sealed class PipelineDeployDelivery(DeployPipelinePlan plans, PipelineDelivery delivery, DeployDeliveredPublisher published)
    : IDeployDelivery
{
    public string Delivery => DeploymentDeclaration.PipelineTrigger;

    public async Task<CommandResult> DeliverAsync(DeployPlanInput input, CancellationToken cancellationToken)
    {
        var (order, planError) = await plans.PlanAsync(input, cancellationToken);
        if (order is null) return CommandResult.Fail(planError!);
        var (started, sentence, run) = await delivery.DeliverAsync(order, cancellationToken);
        if (!started) return CommandResult.Fail($"{order.Headline} failed: {sentence}.");
        await published.PublishAsync(input, new DeployDelivered(order.Version, order.Repo, run!.Url, PipelineRunId: run.RunId), cancellationToken);
        return CommandResult.Ok($"{order.Headline}: {sentence}.");
    }
}
