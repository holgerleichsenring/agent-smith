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
    IJobHeartbeatService heartbeat,
    IActiveRunLease lease,
    ILogger logger)
{
    public async Task<ClaimResult> ExecuteAsync(
        ClaimRequest request, TrackerConnection tracker, CancellationToken ct)
    {
        var transitioner = transitionerFactory.Create(tracker);

        var current = await transitioner.ReadCurrentAsync(request.TicketId, ct);
        if (current is not null and not TicketLifecycleStatus.Pending)
            return ClaimResult.AlreadyClaimed();

        // p0238 active-run guard: a live heartbeat means a run is already in flight
        // for this ticket, even if the lifecycle label was reverted to Pending by
        // the stale detector. Refuse the duplicate — this is the invariant that
        // survives a label revert and stops the run-swarm by construction.
        if (await heartbeat.IsAliveAsync(request.TicketId, ct))
        {
            logger.LogInformation(
                "Claim refused for ticket {Ticket}: a run is already active (heartbeat alive)",
                request.TicketId.Value);
            return ClaimResult.AlreadyClaimed();
        }

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
            // p0238: mark the ticket active at claim time so the Enqueued→InProgress
            // queue window is covered — the running job's heartbeat renewal takes
            // over once it dequeues; if it never starts, the marker lapses by TTL.
            await heartbeat.MarkClaimedAsync(request.TicketId, ct);
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
