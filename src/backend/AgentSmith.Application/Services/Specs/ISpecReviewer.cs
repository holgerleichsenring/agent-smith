using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// Challenges a derived phase against the repository BEFORE it becomes the run's acceptance
/// contract. The mirror of <see cref="ISpecAccountant"/>: same shape, opposite end — the
/// account judges the work against the contract, this judges the contract against the world.
/// </summary>
public interface ISpecReviewer
{
    Task<SpecReview> ReviewAsync(
        SpecPhase phase, AgentConfig agent, BranchSearch? search,
        PipelineCostTracker costTracker, CancellationToken cancellationToken);
}
