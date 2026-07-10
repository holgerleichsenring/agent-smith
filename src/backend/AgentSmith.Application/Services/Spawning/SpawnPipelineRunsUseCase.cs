using AgentSmith.Application.Services.Orchestrator;
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
/// p0269a/p0320b: before claiming, a capacity probe pre-flights the run's REAL
/// footprint (orchestrator pod + one sandbox per repo). p0320c: this is now the
/// single spawn FUNNEL into the persistent capacity queue — a denied ticket (or
/// any ticket behind a non-empty queue: strict FIFO, no overtaking) is upserted
/// as ONE queue entry with ONE visible "queued" Run row and returns Queued
/// without claiming. The head ticket, once admitted, claims with its reserved
/// run id so the queued row becomes the running row, and its entry is deleted.
/// </summary>
public sealed class SpawnPipelineRunsUseCase(
    ITicketClaimService claimService,
    ISandboxResourceResolver resourceResolver,
    IOrchestratorResourceResolver orchestratorResourceResolver,
    ISandboxCapacityProbe capacityProbe,
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
        if (string.IsNullOrEmpty(envelope.TicketId))
            throw new ArgumentException("envelope.TicketId is required for spawn.", nameof(envelope));
        if (string.IsNullOrEmpty(envelope.Platform))
            throw new ArgumentException("envelope.Platform is required for spawn.", nameof(envelope));
        if (project.Repos.Count == 0)
            throw new InvalidOperationException(
                $"Project '{project.Name}' has no repos; cannot spawn pipeline runs.");

        var sandboxSize = resourceResolver.Resolve(project, pipelineName);
        var footprint = new RunFootprint(
            orchestratorResourceResolver.Resolve(project),
            Enumerable.Repeat(sandboxSize, project.Repos.Count).ToList());
        var capacity = await capacityProbe.HasCapacityAsync(footprint, ct);

        // p0320c: strict FIFO — denied → queue; admitted but not the head of a
        // non-empty queue → queue (a fitting smaller run never overtakes).
        var head = await capacityQueue.PeekHeadAsync(ct);
        var isHead = head is not null
            && head.Project == project.Name && head.TicketId == envelope.TicketId;
        if (!capacity.Admitted || (head is not null && !isHead))
            return await DeferToQueueAsync(project, pipelineName, envelope, matchedTrigger,
                planAnswers, capacity, head, isHead, ct);

        var request = SpawnRequestBuilder.BuildRequest(
            project, pipelineName, envelope, matchedTrigger, planAnswers,
            existingRunId: isHead ? head!.ReservedRunId : null);
        var result = await claimService.ClaimAsync(request, config, ct);
        if (isHead && result.Outcome == ClaimOutcome.Claimed)
            await capacityQueue.RemoveAsync(project.Name, envelope.TicketId!, ct);

        logger.LogInformation(
            "Spawn for project={Project} pipeline={Pipeline} ticket={Ticket} → outcome={Outcome}",
            project.Name, pipelineName, envelope.TicketId, result.Outcome);
        return new SpawnResult(new[] { result });
    }

    private async Task<SpawnResult> DeferToQueueAsync(
        ResolvedProject project, string pipelineName, IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger, Dictionary<string, string>? planAnswers,
        CapacityDecision capacity, CapacityQueueEntry? head, bool isHead, CancellationToken ct)
    {
        var reason = !capacity.Admitted
            ? capacity.Reason ?? "waiting for sandbox capacity"
            : $"waiting in line behind {head!.Project}/#{head.TicketId}";
        var candidate = SpawnRequestBuilder.BuildCandidate(
            project, pipelineName, envelope, matchedTrigger, planAnswers,
            RunIdGenerator.Generate(DateTimeOffset.UtcNow), reason);
        var reservedRunId = await capacityQueue.EnqueueAsync(candidate, ct);

        logger.LogInformation(
            "Spawn deferred to capacity queue for project={Project} pipeline={Pipeline} "
            + "ticket={Ticket} run={RunId} head={IsHead}: {Reason}",
            project.Name, pipelineName, envelope.TicketId, reservedRunId, isHead, reason);
        return new SpawnResult(new[] { ClaimResult.Queued(reason) });
    }
}
