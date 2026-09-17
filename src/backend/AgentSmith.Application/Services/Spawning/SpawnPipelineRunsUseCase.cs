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
/// p0336: admission is now PREDICTABLE. The run's COMPLETE footprint (every
/// toolchain-group sandbox at its resolved limit + the orchestrator) is computed
/// from the remote context inventory, recorded for visibility, and reserved
/// atomically against the capacity budget BEFORE the claim — a run only starts
/// when its full footprint fits, so it can never fail for capacity mid-run.
/// A denied ticket (footprint does not fit) or any ticket behind a non-empty
/// queue (strict FIFO, no overtaking) is upserted as ONE queue entry with ONE
/// visible "queued" Run row and returns Queued without claiming. The reservation
/// is freed on terminal status (RunEventApplier) or if the claim fails.
///
/// 2026-09-13-a72a: an epic child whose predecessor has not left the working set is
/// declined BEFORE any of that — it stays in the tracker, holds no run row and no
/// reservation, and is offered again on the next poll. The capacity queue is strict
/// FIFO across all projects, so a dependency waiting at its head would stall the estate.
/// </summary>
public sealed class SpawnPipelineRunsUseCase(
    ITicketClaimService claimService,
    IRunFootprintCalculator footprintCalculator,
    ICapacityBudget capacityBudget,
    ICapacityQueue capacityQueue,
    ISandboxCorpseReaper corpseReaper,
    ISandboxCapacityProbe capacityProbe,
    IPredecessorGate predecessorGate,
    Specs.ApprovedSpecSetCarrier approvedSets, // 2026-09-17-0e79a: the run carries what was approved
    ILogger<SpawnPipelineRunsUseCase> logger) : ISpawnPipelineRunsUseCase
{
    public async Task<SpawnResult> ExecuteAsync(
        AgentSmithConfig config,
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        CancellationToken ct,
        Dictionary<string, string>? planAnswers = null)
    {
        // 2026-09-13-a72a: FIRST — before validation, before the footprint, before any
        // enqueue. A ticket arriving behind a non-empty queue is deferred with a queued Run
        // row and a budget record, and the pump then claims it DIRECTLY; a gate placed after
        // that branch would leave state behind and be bypassed by a second, ungated door.
        var predecessors = await predecessorGate.CheckAsync(project, envelope, ct);
        if (predecessors.Blocked)
            return new SpawnResult([ClaimResult.Queued(predecessors.Reason!)]);

        ValidateForSpawn(project, envelope);
        var footprint = await footprintCalculator.CalculateAsync(project, pipelineName, ct);

        // p0320c: strict FIFO — a fitting smaller run never overtakes a non-empty queue.
        var head = await capacityQueue.PeekHeadAsync(ct);
        var isHead = head is not null && head.Project == project.Name && head.TicketId == envelope.TicketId;
        var runId = isHead ? head!.ReservedRunId! : RunIdGenerator.Generate(DateTimeOffset.UtcNow);
        var behindQueue = head is not null && !isHead;

        if (!behindQueue && await ReserveAsync(runId, footprint, ct))
            return await StartAsync(
                config, project, pipelineName, envelope, matchedTrigger, planAnswers, runId, isHead, ct);

        return await DeferToQueueAsync(
            project, pipelineName, envelope, matchedTrigger, planAnswers, footprint, head, runId, ct);
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

    // p0355: reconcile-then-admit. BEFORE reserving, reap corpse sandbox pods (a
    // crashed replica's pod still holds the namespace ResourceQuota) so headroom
    // reflects reality, then reconcile with the REAL namespace quota — QUEUE a run
    // k8s can't fit instead of admitting it and having the pod-create killed with
    // "exceeded quota". The internal budget ledger stays the lag-free gate on top.
    private async Task<bool> ReserveAsync(string runId, RunFootprintBreakdown footprint, CancellationToken ct)
    {
        await capacityBudget.RecordAsync(runId, footprint, ct);
        await corpseReaper.ReapCorpsesAsync(ct);
        var quota = await capacityProbe.HasCapacityAsync(RunFootprint.From(footprint), ct);
        if (!quota.Admitted)
        {
            logger.LogInformation("Admission denied by namespace quota for run {RunId}: {Reason}", runId, quota.Reason);
            return false;
        }
        return await capacityBudget.TryReserveAsync(runId, ct);
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

    private async Task<SpawnResult> DeferToQueueAsync(
        ResolvedProject project, string pipelineName, IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger, Dictionary<string, string>? planAnswers,
        RunFootprintBreakdown footprint, CapacityQueueEntry? head, string candidateRunId, CancellationToken ct)
    {
        var isHead = head is not null && head.Project == project.Name && head.TicketId == envelope.TicketId;
        var reason = head is not null && !isHead
            ? $"waiting in line behind {head!.Project}/#{head.TicketId}"
            : $"waiting for capacity — footprint {footprint.TotalMemLimit} / {footprint.TotalCpuLimit} cpu "
              + "exceeds the remaining budget";
        var candidate = SpawnRequestBuilder.BuildCandidate(
            project, pipelineName, envelope, matchedTrigger, planAnswers, candidateRunId, reason,
            approvedSetJson: await approvedSets.JsonForAsync(project.Tracker.Name, envelope.Platform, envelope.TicketId, ct));
        var reservedRunId = await capacityQueue.EnqueueAsync(candidate, ct);
        await capacityBudget.RecordAsync(reservedRunId, footprint, ct);

        logger.LogInformation(
            "Spawn deferred to capacity queue for project={Project} pipeline={Pipeline} "
            + "ticket={Ticket} run={RunId}: {Reason}",
            project.Name, pipelineName, envelope.TicketId, reservedRunId, reason);
        return new SpawnResult(new[] { ClaimResult.Queued(reason) });
    }
}
