using System.Text.Json;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services;

/// <summary>
/// p0327: launches a resume entry from the capacity-queue head. Unlike a fresh
/// claim it skips the ticket-lifecycle transition and the trigger-status check
/// (the ticket legitimately sits in its WORKING status mid-run) — the launch is
/// lease + direct job enqueue, reusing the checkpointed run's own id so the
/// waiting_for_input row becomes the running row (p0320c promotion path).
/// </summary>
public sealed class ResumeRunLauncher(
    IServiceProvider services,
    IActiveRunLease lease,
    IRedisJobQueue jobQueue,
    ICapacityQueue capacityQueue,
    ILogger<ResumeRunLauncher> logger)
{
    public async Task LaunchAsync(CapacityQueueEntry head, CancellationToken ct)
    {
        var gate = await GateOnRunRowAsync(head, ct);
        if (gate == ResumeGate.Drop)
        {
            await capacityQueue.RemoveAsync(head.Project, head.TicketId, ct);
            logger.LogWarning(
                "Resume entry {Project}/#{Ticket} dropped — run {RunId} ended while its resume waited",
                head.Project, head.TicketId, head.ReservedRunId);
            return;
        }
        if (gate == ResumeGate.NotYet) return; // executor still unwinding — retry next tick

        var outcome = await lease.TryClaimAsync(head.Project, new TicketId(head.TicketId), ct);
        if (outcome != LeaseClaimOutcome.Claimed)
        {
            logger.LogWarning(
                "Resume entry {Project}/#{Ticket} lease claim → {Outcome}; retrying next tick",
                head.Project, head.TicketId, outcome);
            return;
        }

        await jobQueue.EnqueueAsync(ToRequest(head), ct);
        await capacityQueue.RemoveAsync(head.Project, head.TicketId, ct);
        logger.LogInformation(
            "Resume launched for {Project}/#{Ticket} (run {RunId})",
            head.Project, head.TicketId, head.ReservedRunId);
    }

    // The run row is the authority: terminal/cancelled → drop; still 'running'
    // (the checkpointing executor is unwinding, its RunFinished(waiting) hasn't
    // landed yet) → wait a tick; waiting_for_input → launch.
    private async Task<ResumeGate> GateOnRunRowAsync(CapacityQueueEntry head, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(head.ReservedRunId)) return ResumeGate.Drop;
        using var scope = services.CreateScope();
        var run = await scope.ServiceProvider.GetRequiredService<RunRepository>()
            .GetRunDetailAsync(head.ReservedRunId!, ct);
        if (run is null) return ResumeGate.Drop;
        if (run.FinishedAt is not null || run.CancelRequested) return ResumeGate.Drop;
        return run.Status == "waiting_for_input" ? ResumeGate.Launch : ResumeGate.NotYet;
    }

    private enum ResumeGate { Launch, NotYet, Drop }

    private static PipelineRequest ToRequest(CapacityQueueEntry head) => new(
        head.Project, head.Pipeline,
        TicketId: new TicketId(head.TicketId), Headless: true,
        Context: head.InitialContextJson is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(head.InitialContextJson),
        RunId: head.ReservedRunId);
}
