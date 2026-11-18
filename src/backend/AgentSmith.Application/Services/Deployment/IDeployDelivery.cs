using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Deployment;

/// <summary>2026-11-18-3863e: one way a version reaches an environment, chosen by the template's delivery — a GitOps
/// pull request or a pipeline run. It plans from the step's input and delivers, and its result is the step's.</summary>
public interface IDeployDelivery
{
    /// <summary>The delivery value of deployment.yaml this way answers to.</summary>
    string Delivery { get; }

    Task<CommandResult> DeliverAsync(DeployPlanInput input, CancellationToken cancellationToken);
}
