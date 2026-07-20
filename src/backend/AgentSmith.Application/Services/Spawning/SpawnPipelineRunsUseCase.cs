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
/// </summary>
public sealed class SpawnPipelineRunsUseCase(
    ITicketClaimService claimService,
    IRunFootprintCalculator footprintCalculator,
    ICapacityBudget capacityBudget,
    ICapacityQueue capacityQueue,
    ISandboxCorpseReaper corpseReaper,
    ISandboxCapacityProbe capacityProbe,
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
        var quota = await capacityProbe.HasCapacityAsync(ToRunFootprint(footprint), ct);
        if (!quota.Admitted)
        {
            logger.LogInformation("Admission denied by namespace quota for run {RunId}: {Reason}", runId, quota.Reason);
            return false;
        }
        return await capacityBudget.TryReserveAsync(runId, ct);
    }

    // The breakdown carries per-pod k8s LIMITs; the probe reserves against them
    // (request folded to the limit — conservative). The synthetic "orchestrator" pod
    // maps to the footprint's orchestrator slot, the rest to sandboxes.
    private static RunFootprint ToRunFootprint(RunFootprintBreakdown footprint)
    {
        ResourceLimits? orchestrator = null;
        var sandboxes = new List<ResourceLimits>();
        foreach (var pod in footprint.Pods)
        {
            var limits = new ResourceLimits(pod.CpuLimit, pod.CpuLimit, pod.MemLimit, pod.MemLimit);
            if (pod.Repo == "orchestrator" && orchestrator is null) orchestrator = limits;
            else sandboxes.Add(limits);
        }
        return new RunFootprint(orchestrator, sandboxes);
    }

    private async Task<SpawnResult> StartAsync(
        AgentSmithConfig config, ResolvedProject project, string pipelineName,
        IncomingTicketEnvelope envelope, WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers, string runId, bool isHead, CancellationToken ct)
    {
        var request = SpawnRequestBuilder.BuildRequest(
            project, pipelineName, envelope, matchedTrigger, planAnswers, existingRunId: runId);
        var result = await claimService.ClaimAsync(request, config, ct);
        if (result.Outcome == ClaimOutcome.Claimed)
        {
            if (isHead) await capacityQueue.RemoveAsync(project.Name, envelope.TicketId!, ct);
        }
        else
        {
            // The run never started (already-claimed / error) — free the reservation.
            await capacityBudget.ReleaseAsync(runId, ct);
        }
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
            project, pipelineName, envelope, matchedTrigger, planAnswers, candidateRunId, reason);
        var reservedRunId = await capacityQueue.EnqueueAsync(candidate, ct);
        await capacityBudget.RecordAsync(reservedRunId, footprint, ct);

        logger.LogInformation(
            "Spawn deferred to capacity queue for project={Project} pipeline={Pipeline} "
            + "ticket={Ticket} run={RunId}: {Reason}",
            project.Name, pipelineName, envelope.TicketId, reservedRunId, reason);
        return new SpawnResult(new[] { ClaimResult.Queued(reason) });
    }
}
