using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single entry point for starting a pipeline from a ticket event.
/// Performs pre-checks, acquires a claim lock, transitions ticket status to Enqueued,
/// and enqueues a PipelineRequest onto IRedisJobQueue.
/// </summary>
public interface ITicketClaimService
{
    Task<ClaimResult> ClaimAsync(
        ClaimRequest request,
        AgentSmithConfig config,
        CancellationToken cancellationToken);
}
