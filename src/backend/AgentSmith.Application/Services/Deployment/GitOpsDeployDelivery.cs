using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c, a delivery of its own since 2026-11-18-3863e: the manifest change planned and delivered as a
/// pull request; an environment already at the version is done with nothing written.</summary>
public sealed class GitOpsDeployDelivery(DeployManifestPlan plans, GitOpsDelivery delivery) : IDeployDelivery
{
    public string Delivery => DeploymentDeclaration.GitOpsPullRequest;

    public async Task<CommandResult> DeliverAsync(DeployPlanInput input, CancellationToken cancellationToken)
    {
        var (plan, planError) = await plans.PlanAsync(input, cancellationToken);
        if (plan is null) return CommandResult.Fail(planError!);
        if (plan.Order is null) return CommandResult.Ok(plan.Headline);
        var (opened, sentence) = await delivery.DeliverAsync(plan.Order, cancellationToken);
        return opened ? CommandResult.Ok($"{plan.Headline}: {sentence}.") : CommandResult.Fail($"{plan.Headline} failed: {sentence}.");
    }
}
