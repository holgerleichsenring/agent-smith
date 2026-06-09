using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// Owns the in-lock work of the claim region: read the ticket lifecycle, transition
/// Pending -> Enqueued, enqueue one PipelineRequest. Extracted from TicketClaimService
/// to keep both classes under the 120-line limit and to keep the orchestrator
/// (TicketClaimService) cleanly separated from the region body.
/// </summary>
internal sealed class SingleClaimRegionExecutor(
    ITicketStatusTransitionerFactory transitionerFactory,
    IRedisJobQueue jobQueue,
    IActiveRunLease lease,
    ILogger logger)
{
    public async Task<ClaimResult> ExecuteAsync(
        ClaimRequest request, TrackerConnection tracker, CancellationToken ct)
    {
        var transitioner = transitionerFactory.Create(tracker);

        // p0258: block ONLY when a run is genuinely IN FLIGHT (Enqueued/InProgress).
        // A TERMINAL status (Done/Failed) is a PRIOR run — re-triggering the ticket
        // on the tracker is a legitimate NEW run and must be claimable. The old
        // `not Pending` gate froze every re-run: the DB-authoritative status (p0246d)
        // stays Failed/Done after a run, the operator's tracker-label edit never
        // resets that DB row, so the ticket could never be claimed again (the
        // recurring "goes straight to failed / never starts / not in the UI" bug).
        // The claim below transitions the status forward, overwriting the stale row.
        var current = await transitioner.ReadCurrentAsync(request.TicketId, ct);
        if (current is TicketLifecycleStatus.Enqueued or TicketLifecycleStatus.InProgress)
            return ClaimResult.AlreadyClaimed();

        // p0251: single-run is now PURELY DB-authoritative — the heartbeat IsAliveAsync
        // pre-gate that used to sit here is gone. It was redundant with the lease below
        // AND harmful: the Redis heartbeat is released on finish via the lifecycle scope
        // (JobHeartbeatService compare-and-delete), but lingers ~2min in edge cases (a
        // run that never starts leaves MarkClaimedAsync's "claimed" bridge, which the
        // run-id compare-and-delete cannot clear). As a hard gate that linger blocked the
        // NEXT claim with AlreadyClaimed, stranding the ticket "pending" until TTL — the
        // recurring bug that had to be cleared by hand. The lease (released on finish,
        // p0242) is the right single guard.
        //
        // p0246b: the AUTHORITATIVE single-run guard — INSERT the ActiveRun lease.
        // The UNIQUE(Project,TicketId) index rejects a duplicate as AlreadyClaimed
        // by construction (survives a label revert AND a flushed Redis, which the
        // heartbeat alone does not). DB-free composition binds NoOpActiveRunLease.
        var leaseOutcome = await lease.TryClaimAsync(request.ProjectName, request.TicketId, ct);
        if (leaseOutcome == LeaseClaimOutcome.AlreadyClaimed)
            return ClaimResult.AlreadyClaimed();
        if (leaseOutcome == LeaseClaimOutcome.Error)
            return ClaimResult.Failed("Active-run lease could not be acquired (database error).");

        var transition = await transitioner.TransitionAsync(
            request.TicketId, TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, ct);

        return transition.Outcome switch
        {
            TransitionOutcome.Succeeded => await EnqueueAsync(request, ct),
            TransitionOutcome.PreconditionFailed => await ReleaseAndAsync(
                request, ClaimResult.AlreadyClaimed(), ct),
            _ => await ReleaseAndAsync(
                request, ClaimResult.Failed(transition.Error ?? transition.Outcome.ToString()), ct)
        };
    }

    private async Task<ClaimResult> EnqueueAsync(ClaimRequest request, CancellationToken ct)
    {
        try
        {
            // p0252: the Enqueued→InProgress queue window is covered by the DB lease
            // (TryClaimAsync INSERTed it just above with a fresh HeartbeatAt) — no
            // Redis "claimed" bridge anymore. ExecutePipelineUseCase renews the lease
            // heartbeat once the job dequeues; a never-started run goes stale and is
            // re-enqueued by EnqueuedReconciler / reaped, all off the one lease.
            await jobQueue.EnqueueAsync(ToPipelineRequest(request), ct);
            return ClaimResult.Claimed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enqueue failed for ticket {Ticket}", request.TicketId.Value);
            // The lease was taken but the run will never run — release it so the
            // ticket is not deadlocked until the reaper's threshold elapses.
            return await ReleaseAndAsync(request, ClaimResult.Failed($"Enqueue failed: {ex.Message}"), ct);
        }
    }

    // Roll the lease back when the claim region fails AFTER taking it, so a failed
    // claim leaves no orphan lease behind.
    private async Task<ClaimResult> ReleaseAndAsync(ClaimRequest request, ClaimResult result, CancellationToken ct)
    {
        await lease.ReleaseAsync(request.ProjectName, request.TicketId, ct);
        return result;
    }

    private static PipelineRequest ToPipelineRequest(ClaimRequest r) => new(
        r.ProjectName, r.PipelineName, TicketId: r.TicketId, Headless: true,
        Context: r.InitialContext, PlanAnswers: r.PlanAnswers);
}
