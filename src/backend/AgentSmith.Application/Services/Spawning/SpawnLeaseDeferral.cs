using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// 2026-10-08-e8b9e: the funnel's DEFER branch, beside <see cref="CapacityDeferral"/>. Before a
/// ticket is queued it is asked what the claim would say: a standing refusal (2026-09-21-77d6), or a
/// FRESH lease — a run working on the ticket now. ClaimRefusal never reads the lease, and the pump leaves
/// an AlreadyClaimed head in place, so a queued ticket with a live run blocked the FIFO behind it. The
/// admitted branch still has only the claim's INSERT as its guard; this check only avoids the queue.
/// </summary>
internal sealed class SpawnLeaseDeferral(
    CapacityDeferral deferral, IUnmovedTicketStore unmovedTickets, IActiveRunLease leases, TimeProvider clock, ILogger logger)
{
    public async Task<SpawnResult> DeferAsync(
        AgentSmithConfig config, ResolvedProject project, string pipelineName, IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger, Dictionary<string, string>? planAnswers, RunFootprintBreakdown footprint,
        CapacityQueueEntry? head, string runId, CapacityDecision? admission, CancellationToken ct)
    {
        var probe = SpawnRequestBuilder.BuildRequest(project, pipelineName, envelope, matchedTrigger, planAnswers, existingRunId: null);
        if (await new Claim.ClaimRefusal(unmovedTickets).ForAsync(probe, config, ct) is { } refusal)
        {
            logger.LogInformation("Spawn refused rather than queued for project={Project} ticket={Ticket}: {Rejection}",
                project.Name, envelope.TicketId, refusal.Rejection);
            return new SpawnResult([refusal]);
        }
        var lease = await leases.GetByTicketAsync(project.Name, new TicketId(envelope.TicketId!), ct);
        if (lease is not null && clock.GetUtcNow() - lease.HeartbeatAt < Lifecycle.ActiveRunReaper.LeaseFreshFor)
        {
            logger.LogInformation("Spawn not queued for project={Project} ticket={Ticket}: run {RunId} holds it",
                project.Name, envelope.TicketId, lease.RunId ?? "(starting)");
            return new SpawnResult([ClaimResult.AlreadyClaimed()]);
        }
        return await deferral.DeferAsync(
            project, pipelineName, envelope, matchedTrigger, planAnswers, footprint, head, runId, admission, ct);
    }
}
