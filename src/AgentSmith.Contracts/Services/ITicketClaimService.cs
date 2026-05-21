using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single entry point for starting a pipeline from a ticket event. Performs the
/// pre-check, acquires a per-(platform, ticket) Redis claim-lock, transitions
/// the ticket lifecycle Pending -> Enqueued once, and enqueues a single
/// PipelineRequest onto IRedisJobQueue. The unified-run model: one ticket =
/// one pipeline run = one enqueue (no per-repo fan-out).
/// </summary>
public interface ITicketClaimService
{
    Task<ClaimResult> ClaimAsync(
        ClaimRequest request,
        AgentSmithConfig config,
        CancellationToken cancellationToken);
}
