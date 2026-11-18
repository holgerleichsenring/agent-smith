using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863e: the pipeline planned and started. A redeploy of the version an environment runs starts it
/// again: a person asked for it, and the pipeline, not a manifest, is what delivers.</summary>
public sealed class PipelineDeployDelivery(DeployPipelinePlan plans, PipelineDelivery delivery) : IDeployDelivery
{
    public string Delivery => DeploymentDeclaration.PipelineTrigger;

    public async Task<CommandResult> DeliverAsync(DeployPlanInput input, CancellationToken cancellationToken)
    {
        var (order, planError) = await plans.PlanAsync(input, cancellationToken);
        if (order is null) return CommandResult.Fail(planError!);
        var (started, sentence) = await delivery.DeliverAsync(order, cancellationToken);
        return started ? CommandResult.Ok($"{order.Headline}: {sentence}.") : CommandResult.Fail($"{order.Headline} failed: {sentence}.");
    }
}
