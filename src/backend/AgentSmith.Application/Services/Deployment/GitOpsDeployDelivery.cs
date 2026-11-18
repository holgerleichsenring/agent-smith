using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863c, a delivery of its own since 2026-11-18-3863e: the manifest change planned and delivered as a
/// pull request; an environment already at the version is done with nothing written. 2026-11-18-3863f: an opened pull
/// request is published for the server to follow.</summary>
public sealed class GitOpsDeployDelivery(DeployManifestPlan plans, GitOpsDelivery delivery, DeployDeliveredPublisher published)
    : IDeployDelivery
{
    public string Delivery => DeploymentDeclaration.GitOpsPullRequest;

    public async Task<CommandResult> DeliverAsync(DeployPlanInput input, CancellationToken cancellationToken)
    {
        var (plan, planError) = await plans.PlanAsync(input, cancellationToken);
        if (plan is null) return CommandResult.Fail(planError!);
        if (plan.Order is not { } order) return CommandResult.Ok(plan.Headline);
        var (opened, sentence, url) = await delivery.DeliverAsync(order, cancellationToken);
        if (!opened) return CommandResult.Fail($"{plan.Headline} failed: {sentence}.");
        await published.PublishAsync(input, new DeployDelivered(order.Version, order.Repo, url!, PullRequestUrl: url), cancellationToken);
        return CommandResult.Ok($"{plan.Headline}: {sentence}.");
    }
}
