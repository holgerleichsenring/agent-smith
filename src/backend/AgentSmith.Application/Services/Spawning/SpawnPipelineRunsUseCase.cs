using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// Builds exactly one ClaimRequest per ticket and submits it through
/// ITicketClaimService.ClaimAsync. The unified-run model: one ticket = one
/// pipeline run over all configured repos (no per-repo fan-out).
///
/// p0336: admission is now PREDICTABLE. The run's COMPLETE footprint (every toolchain-group
/// sandbox at its resolved limit + the orchestrator) is computed from the remote context
/// inventory, recorded for visibility, and reserved atomically against the capacity budget
/// BEFORE the claim — a run only starts when its full footprint fits, so it can never fail for
/// capacity mid-run. A denied ticket (footprint does not fit) or any ticket behind a non-empty
/// queue (strict FIFO, no overtaking) is upserted as ONE queue entry with ONE visible "queued"
/// Run row and returns Queued without claiming. The reservation is freed on terminal status
/// (RunEventApplier) or if the claim fails.
///
/// 2026-09-22-766b: the funnel no longer asks whether a ticket's predecessors have left the
/// working set. Nothing the framework files has carried a predecessor stamp since
/// 2026-09-17-0e79d — the order inside an approved cut is the sequence's, phase by phase, a
/// successor held until its predecessor VERIFIED — and the gate could only ever HOLD a spawn,
/// never refuse one. What that costs is stated rather than hidden: two legacy children of the
/// withdrawn N-children shape may now run CONCURRENTLY and cut from the same parent rung.
///
/// 2026-09-21-77d6: the decision is three ways, not two — start, REFUSE, defer. A ticket the
/// claim would refuse outright is refused instead of queued; queuing it mints a waiting run the
/// pump then drops, and the next poll mints another.
/// </summary>
public sealed class SpawnPipelineRunsUseCase(
    ITicketClaimService claimService,
    IRunFootprintCalculator footprintCalculator,
    ICapacityBudget capacityBudget,
    ICapacityQueue capacityQueue,
    ISandboxCorpseReaper corpseReaper,
    IHeldSandboxRegister heldSandboxes, // 2026-09-22-2d11a: a hold never denies a run
    ISandboxCapacityProbe capacityProbe,
    Specs.ApprovedSpecSetCarrier approvedSets, // 2026-09-17-0e79a: the run carries what was approved
    IRunListNudge runListNudge, // 2026-09-20-9f00: a deferred row announces itself
    IUnmovedTicketStore unmovedTickets, // 2026-09-21-77d6: what the claim would refuse
    ILogger<SpawnPipelineRunsUseCase> logger) : ISpawnPipelineRunsUseCase
{
    private readonly CapacityDeferral _deferral =
        new(capacityQueue, capacityBudget, approvedSets, runListNudge, logger);

    public async Task<SpawnResult> ExecuteAsync(
        AgentSmithConfig config,
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        CancellationToken ct,
        Dictionary<string, string>? planAnswers = null)
    {
        ValidateForSpawn(project, envelope);
        var footprint = await footprintCalculator.CalculateAsync(project, pipelineName, ct);

        // p0320c: strict FIFO — a fitting smaller run never overtakes a non-empty queue.
        var head = await capacityQueue.PeekHeadAsync(ct);
        var isHead = head is not null && head.Project == project.Name && head.TicketId == envelope.TicketId;
        var runId = isHead ? head!.ReservedRunId! : RunIdGenerator.Generate(DateTimeOffset.UtcNow);
        var behindQueue = head is not null && !isHead;

        // Still NOT attempted behind the queue: no footprint record, no corpse reap, no pod listing.
        var admission = behindQueue ? null : await ReserveAsync(runId, footprint, ct);
        if (admission is { Admitted: true })
            return await StartAsync(
                config, project, pipelineName, envelope, matchedTrigger, planAnswers, runId, isHead, ct);

        // 2026-09-21-77d6: IMMEDIATELY before the deferral, not at the top of the funnel — the
        // branch above goes on to claim, which asks this same seam itself. A ticket the claim
        // would refuse must not be queued: the pump claims the head, is refused, drops the entry
        // as permanent, and the next poll finds no head and mints another waiting run.
        var probe = SpawnRequestBuilder.BuildRequest(
            project, pipelineName, envelope, matchedTrigger, planAnswers, existingRunId: null);
        if (await new Claim.ClaimRefusal(unmovedTickets).ForAsync(probe, config, ct) is { } refusal)
        {
            logger.LogInformation(
                "Spawn refused rather than queued for project={Project} ticket={Ticket}: {Rejection}",
                project.Name, envelope.TicketId, refusal.Rejection);
            return new SpawnResult([refusal]);
        }

        return await _deferral.DeferAsync(
            project, pipelineName, envelope, matchedTrigger, planAnswers, footprint, head, runId, admission, ct);
    }

    private static void ValidateForSpawn(ResolvedProject project, IncomingTicketEnvelope envelope)
    {
        if (string.IsNullOrEmpty(envelope.TicketId))
            throw new ArgumentException("envelope.TicketId is required for spawn.", nameof(envelope));
        if (string.IsNullOrEmpty(envelope.Platform))
            throw new ArgumentException("envelope.Platform is required for spawn.", nameof(envelope));
        if (project.Repos.Count == 0)
            throw new InvalidOperationException(
                $"Project '{project.Name}' has no repos; cannot spawn pipeline runs.");
    }

    // p0355: reconcile-then-admit. BEFORE reserving, reap corpse sandbox pods (a crashed
    // replica's pod still holds the namespace ResourceQuota) so headroom reflects reality, then
    // reconcile with the REAL namespace quota — QUEUE a run k8s can't fit instead of admitting it
    // and having the pod-create killed with "exceeded quota". The internal budget ledger stays
    // the lag-free gate on top.
    // 2026-09-21-5c17: the answer is the same decision the manual init door answers with, so the
    // deferral names the gate that actually refused instead of composing a budget sentence for a
    // gate that may have said yes. The probe's words are carried; the ledger's are composed.
    private async Task<CapacityDecision> ReserveAsync(
        string runId, RunFootprintBreakdown footprint, CancellationToken ct)
    {
        await capacityBudget.RecordAsync(runId, footprint, ct);
        // 2026-09-22-2d11a: the release joins the reconcile that already precedes the probe.
        // 2026-09-24-81ea: and only when the room is short — a run that fits anyway must not cost
        // a design conversation the sandboxes it is holding for its next turn.
        await heldSandboxes.EvictIfShortAsync(
            async c => (await capacityProbe.HasCapacityAsync(RunFootprint.From(footprint), c)).Admitted, ct);
        await corpseReaper.ReapCorpsesAsync(ct);
        var quota = await capacityProbe.HasCapacityAsync(RunFootprint.From(footprint), ct);
        if (!quota.Admitted)
        {
            logger.LogInformation("Admission denied by the sandbox host for run {RunId}: {Reason}", runId, quota.Reason);
            return CapacityDecision.Deny(CapacityReasons.Carried(quota.Reason));
        }
        return await capacityBudget.TryReserveAsync(runId, ct)
            ? CapacityDecision.Admit() : CapacityDecision.Deny(CapacityReasons.LedgerFull(footprint));
    }

    private async Task<SpawnResult> StartAsync(
        AgentSmithConfig config, ResolvedProject project, string pipelineName,
        IncomingTicketEnvelope envelope, WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers, string runId, bool isHead, CancellationToken ct)
    {
        var request = SpawnRequestBuilder.BuildRequest(
            project, pipelineName, envelope, matchedTrigger, planAnswers, existingRunId: runId,
            approvedSetJson: await approvedSets.JsonForAsync(project.Tracker.Name, envelope.Platform, envelope.TicketId, ct));
        var result = await claimService.ClaimAsync(request, config, ct);
        // The run never started (already-claimed / error) — free the reservation.
        if (result.Outcome != ClaimOutcome.Claimed) await capacityBudget.ReleaseAsync(runId, ct);
        else if (isHead) await capacityQueue.RemoveAsync(project.Name, envelope.TicketId!, ct);
        logger.LogInformation(
            "Spawn for project={Project} pipeline={Pipeline} ticket={Ticket} → outcome={Outcome}",
            project.Name, pipelineName, envelope.TicketId, result.Outcome);
        return new SpawnResult(new[] { result });
    }
}
