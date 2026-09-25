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
    ClaimedTicketRegistrar registrar,
    ILogger logger)
{
    public async Task<ClaimResult> ExecuteAsync(
        ClaimRequest request, TrackerConnection tracker, CancellationToken ct)
    {
        var transitioner = transitionerFactory.Create(tracker);

        // p0262: the claim is LEASE-ONLY. The p0258 ReadCurrent Enqueued/InProgress
        // pre-gate is gone — lifecycle status is no longer stored or read as authority;
        // the ActiveRun lease INSERT below is the sole single-run guard. "Already in
        // flight?" = a held lease, not a tag/DB read. "Already serviced?" is the
        // poller's job (native status outside trigger_statuses), not the claim's.
        //
        // p0246b: the AUTHORITATIVE single-run guard — INSERT the ActiveRun lease.
        // The UNIQUE(Project,TicketId) index rejects a duplicate as AlreadyClaimed
        // by construction (survives a label edit AND a flushed Redis). DB-free
        // composition binds NoOpActiveRunLease. 2026-09-25-b4d9: the registrar writes
        // the durable taken-ticket record beside it, before any tracker call.
        var leaseOutcome = await registrar.TakeAsync(request, ct);
        if (leaseOutcome == LeaseClaimOutcome.AlreadyClaimed)
            return ClaimResult.AlreadyClaimed();
        if (leaseOutcome == LeaseClaimOutcome.Error)
            return ClaimResult.Failed("Active-run lease could not be acquired (database error).");

        var transition = await transitioner.TransitionAsync(
            request.TicketId, TicketLifecycleStatus.Pending, TicketLifecycleStatus.Enqueued, ct);

        return transition.Outcome switch
        {
            TransitionOutcome.Succeeded => await EnqueueAsync(request, ct),
            // 2026-09-25-3c7ab: a CONCURRENCY refusal is not a statement about the work. Its
            // three sources are a GitHub ETag mismatch, an Azure DevOps rev mismatch or 409, and
            // a Redis label-lock already held — every one of them means somebody else wrote to
            // this ticket in the same second, and an operator typing a comment is enough. The
            // index granted this claim; reporting it as AlreadyClaimed was a lie about it, and
            // releasing the lease handed the ticket back over a label nobody reads as authority.
            TransitionOutcome.PreconditionFailed => await ProceedUnwrittenAsync(request, transition, ct),
            _ => await ReleaseAndAsync(
                request, ClaimResult.Failed(transition.Error ?? transition.Outcome.ToString()), ct)
        };
    }

    /// <summary>
    /// The claim stands and the board simply does not say so. NotFound and a failed write keep
    /// releasing: a ticket that is not there produces work nobody can deliver, and a tracker that
    /// cannot be written to at all will not take this run's result either.
    /// </summary>
    private async Task<ClaimResult> ProceedUnwrittenAsync(
        ClaimRequest request, TransitionResult transition, CancellationToken ct)
    {
        logger.LogWarning(
            "Ticket {Ticket} could not be marked enqueued ({Reason}) — the lease is held and the "
            + "run proceeds; the board is display, not the guard",
            request.TicketId.Value, transition.Error ?? nameof(TransitionOutcome.PreconditionFailed));
        return await EnqueueAsync(request, ct);
    }

    private async Task<ClaimResult> EnqueueAsync(ClaimRequest request, CancellationToken ct)
    {
        try
        {
            // p0252: the Enqueued→InProgress queue window is covered by the DB lease
            // (TryClaimAsync INSERTed it just above with a fresh HeartbeatAt) — no
            // Redis "claimed" bridge anymore. ExecutePipelineUseCase renews the lease
            // heartbeat once the job dequeues; a never-started run goes stale, is reaped off
            // that lease and re-enqueued by EnqueuedReconciler off the taken-ticket record.
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

    // Roll what the claim wrote back when the region fails AFTER taking it, so a failed
    // claim leaves neither an orphan lease nor a record of work nobody is doing.
    private async Task<ClaimResult> ReleaseAndAsync(ClaimRequest request, ClaimResult result, CancellationToken ct)
    {
        await registrar.RollBackAsync(request, ct);
        return result;
    }

    // p0320c: ExistingRunId rides into the PipelineRequest so a capacity-queued
    // ticket's launch reuses its reserved "queued" Run row instead of minting a
    // new run id per attempt.
    private static PipelineRequest ToPipelineRequest(ClaimRequest r) => new(
        r.ProjectName, r.PipelineName, TicketId: r.TicketId, Headless: true,
        Context: r.InitialContext, PlanAnswers: r.PlanAnswers, RunId: r.ExistingRunId);
}
