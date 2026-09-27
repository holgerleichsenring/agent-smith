using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-09-25-b4d9: everything taking a ticket up writes down — the ActiveRun lease, which is
/// a LIVENESS fact the reaper deletes three minutes after a crash, and the taken-ticket record,
/// which states that the work is owed and outlives it. Extracted from SingleClaimRegionExecutor
/// so the region body stays the region body, and so both writes roll back in one place.
/// </summary>
internal sealed class ClaimedTicketRegistrar(IActiveRunLease lease, ITakenTicketStore takenTickets)
{
    /// <summary>
    /// Takes the lease and — only when the index granted it — records the claim. The record
    /// is written HERE rather than after the tracker transition: the crash window this phase
    /// closes is the one between taking the ticket and having somewhere durable that says so,
    /// and the narrowest that window gets is one local write after the INSERT that won.
    /// A record store that cannot be written to fails the claim; a ticket this framework
    /// cannot write down is a ticket it cannot recover.
    /// </summary>
    public async Task<LeaseClaimOutcome> TakeAsync(ClaimRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outcome = await lease.TryClaimAsync(request.ProjectName, request.TicketId, ct);
        if (outcome != LeaseClaimOutcome.Claimed) return outcome;
        await takenTickets.TakeAsync(
            new TakenTicketFact(
                request.ProjectName, request.TicketId.Value, request.Platform, request.PipelineName),
            ct);
        return outcome;
    }

    /// <summary>
    /// Rolls both writes back when the claim region fails AFTER taking them, so a failed claim
    /// leaves neither an orphan lease nor a record of work nobody is doing.
    /// </summary>
    public async Task RollBackAsync(ClaimRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        // p0459: runId null — the claim was taken moments ago and no run has attached
        // to it, so the rollback only drops an UNATTACHED row.
        await lease.ReleaseAsync(request.ProjectName, request.TicketId, runId: null, ct);
        await takenTickets.ClearAsync(request.ProjectName, request.TicketId.Value, ct);
    }
}
