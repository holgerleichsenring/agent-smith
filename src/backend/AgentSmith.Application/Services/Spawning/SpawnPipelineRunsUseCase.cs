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

    // Record the footprint (for the dashboard) then try to reserve it against the
    // budget — true only when the FULL footprint fits the remaining budget.
    private async Task<bool> ReserveAsync(string runId, RunFootprintBreakdown footprint, CancellationToken ct)
    {
        await capacityBudget.RecordAsync(runId, footprint, ct);
        return await capacityBudget.TryReserveAsync(runId, ct);
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
