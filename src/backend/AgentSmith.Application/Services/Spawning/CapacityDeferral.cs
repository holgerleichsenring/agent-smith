using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// p0320c: the write side of the spawn funnel's deferral — ONE queue entry, ONE visible
/// "queued" Run row and the budget record that makes the waiting run's footprint readable.
/// The entry is keyed by project and ticket, so a ticket that simply waits keeps its arrival
/// order and its reserved run id however many polls it waits.
/// <para>
/// 2026-09-21-77d6: extracted from SpawnPipelineRunsUseCase, which now decides three ways —
/// start, refuse, defer — and no longer holds the writes for the third.
/// </para>
/// </summary>
internal sealed class CapacityDeferral(
    ICapacityQueue capacityQueue,
    ICapacityBudget capacityBudget,
    Specs.ApprovedSpecSetCarrier approvedSets,
    IRunListNudge runListNudge,
    ILogger logger)
{
    public async Task<SpawnResult> DeferAsync(
        ResolvedProject project, string pipelineName, IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger, Dictionary<string, string>? planAnswers,
        RunFootprintBreakdown footprint, CapacityQueueEntry? head, string candidateRunId,
        CapacityDecision? refusal, CancellationToken ct)
    {
        var reason = WaitReason(project, envelope, refusal, head);
        var candidate = SpawnRequestBuilder.BuildCandidate(
            project, pipelineName, envelope, matchedTrigger, planAnswers, candidateRunId, reason,
            approvedSetJson: await approvedSets.JsonForAsync(
                project.Tracker.Name, envelope.Platform, envelope.TicketId, ct));
        var reservedRunId = await capacityQueue.EnqueueAsync(candidate, ct);
        await capacityBudget.RecordAsync(reservedRunId, footprint, ct);
        // 2026-09-20-9f00: a deferred run is outside the active set the broadcaster drains, so a
        // published event would die there. Nudge the surface AFTER the write, and best-effort.
        try { await runListNudge.RunsChangedAsync(reservedRunId, ct); }
        catch (Exception ex) { logger.LogDebug(ex, "Queued-run nudge failed for {RunId}", reservedRunId); }
        logger.LogInformation(
            "Spawn deferred to capacity queue for project={Project} pipeline={Pipeline} "
            + "ticket={Ticket} run={RunId}: {Reason}",
            project.Name, pipelineName, envelope.TicketId, reservedRunId, reason);
        return new SpawnResult(new[] { ClaimResult.Queued(reason) });
    }

    // 2026-09-21-5c17: the refusal that was actually RECEIVED is what a waiting run says. The
    // funnel composed a budget sentence for every deferral, so a host that declares no budget
    // — where the ledger fails open and cannot have refused anything — told its operator to go
    // looking for a limit that does not exist. The queue-position branch is the deferral's own
    // knowledge and stays; it is also what answers when no refusal was received, because behind
    // the queue no reservation is attempted at all.
    private static string WaitReason(
        ResolvedProject project, IncomingTicketEnvelope envelope,
        CapacityDecision? refusal, CapacityQueueEntry? head)
    {
        var isHead = head is not null && head.Project == project.Name && head.TicketId == envelope.TicketId;
        return head is not null && !isHead
            ? $"waiting in line behind {head!.Project}/#{head.TicketId}"
            : Sandbox.CapacityReasons.Carried(refusal?.Reason);
    }
}
