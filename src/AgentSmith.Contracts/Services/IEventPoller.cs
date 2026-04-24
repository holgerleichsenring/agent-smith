using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Discovers tickets eligible for the claim flow by listing tickets with the trigger
/// label in Pending status. Label-idempotent — no cursor, the ticket status decides
/// whether to act. Orchestration (calling ITicketClaimService) is the host's job.
/// </summary>
public interface IEventPoller
{
    string PlatformName { get; }
    string ProjectName { get; }
    int IntervalSeconds { get; }
    Task<IReadOnlyList<ClaimRequest>> PollAsync(CancellationToken cancellationToken);
}
