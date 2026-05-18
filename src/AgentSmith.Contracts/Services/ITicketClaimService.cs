using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Single entry point for starting a pipeline from a ticket event.
/// Performs pre-checks, acquires a claim lock, transitions ticket status to Enqueued,
/// and enqueues a PipelineRequest onto IRedisJobQueue. ClaimSpawnAsync extends this for
/// multi-repo spawn: one pre-check + one lock + one lifecycle transition + N enqueues.
/// </summary>
public interface ITicketClaimService
{
    Task<ClaimResult> ClaimAsync(
        ClaimRequest request,
        AgentSmithConfig config,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claim-region for multi-repo spawn. All requests must share Platform + TicketId; they
    /// differ in RepoName (single-repo projects pass a 1-element list). Performs ClaimPreChecker
    /// on every request, acquires ONE Redis claim-lock on (platform, ticket-id), reads ticket
    /// lifecycle once, transitions Pending -> Enqueued ONCE, then enqueues N PipelineRequests.
    /// Returns one ClaimResult per input request, in input order.
    /// </summary>
    Task<IReadOnlyList<ClaimResult>> ClaimSpawnAsync(
        IReadOnlyList<ClaimRequest> requests,
        AgentSmithConfig config,
        CancellationToken cancellationToken);
}
