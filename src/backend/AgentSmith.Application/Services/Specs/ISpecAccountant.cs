using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0422: accounts for a phase against the branch. A port, for the same reason as
/// <see cref="ISpecCutReviewer"/>: it is the framework's own call, not the master's.
/// </summary>
public interface ISpecAccountant
{
    Task<SpecAccount> AccountAsync(
        string repoKey, IReadOnlyList<string> criteria, string diff,
        IReadOnlyList<string> commandResults, AgentConfig agent,
        BranchSearch? branchSearch,
        PipelineCostTracker costTracker, CancellationToken cancellationToken,
        int windowBudgetChars = DiffWindows.DefaultBudgetChars);
}
