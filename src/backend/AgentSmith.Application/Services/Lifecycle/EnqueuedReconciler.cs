using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically re-enqueues the tickets this framework took up and has not finished. For any
/// taken-ticket record with no FRESH active-run lease, it re-pushes a PipelineRequest onto
/// IRedisJobQueue. Covers Redis loss, crashed pre-consume pods, and enqueue failures from
/// TicketClaimService. Runs on every replica unconditionally — leader-election is deferred to p96.
///
/// p0252: liveness is the DB lease (set at claim, renewed while the run executes), not the
/// volatile Redis heartbeat.
///
/// 2026-09-25-b4d9: the CANDIDATES come from the record, not from asking the tracker for the
/// `agent-smith:enqueued` label. The reaper deletes an orphan's lease three minutes after the
/// crash, so a label on somebody else's board was the last surviving evidence of it — and the
/// pipeline to rebuild the request with was read back out of that same board.
/// </summary>
public sealed class EnqueuedReconciler(
    IActiveRunLease activeRunLease,
    IRedisJobQueue jobQueue,
    ITakenTicketStore takenTickets,
    IConfigurationLoader configLoader,
    Specs.ApprovedSpecSetCarrier approvedSets, // 2026-09-17-0e79a: an orphan is re-enqueued with its set
    TimeProvider timeProvider,
    string configPath,
    ILogger<EnqueuedReconciler> logger)
{
    private static readonly TimeSpan ReconcileInterval = TimeSpan.FromMinutes(10);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("EnqueuedReconciler started (interval: {Interval})", ReconcileInterval);

        try { await ReconcileOnceAsync(cancellationToken); }
        catch (Exception ex) { logger.LogWarning(ex, "Initial reconcile failed"); }

        while (!cancellationToken.IsCancellationRequested)
        {
            try { await Task.Delay(ReconcileInterval, cancellationToken); }
            catch (OperationCanceledException) { break; }

            try { await ReconcileOnceAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "Reconcile failed"); }
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken ct)
    {
        var config = configLoader.LoadConfig(configPath);
        // A record whose state says the reaper holds the ticket is not in this list: the two
        // loops never act on one ticket at once.
        foreach (var taken in await takenTickets.ListReconcilableAsync(ct))
            await ReconcileSafeAsync(config, taken, ct);
    }

    private async Task ReconcileSafeAsync(AgentSmithConfig config, TakenTicketFact taken, CancellationToken ct)
    {
        try { await ReconcileAsync(config, taken, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "EnqueuedReconciler skipped taken ticket {Project}/{Ticket}: {Message}",
                taken.Project, taken.TicketId, ex.Message);
        }
    }

    private async Task ReconcileAsync(AgentSmithConfig config, TakenTicketFact taken, CancellationToken ct)
    {
        // A project the configuration no longer knows cannot be launched into. The record
        // stays: it is cleared by completion, and an operator who restores the project gets
        // the ticket back rather than a row silently dropped by a reconcile pass.
        if (!config.Projects.TryGetValue(taken.Project, out var project)) return;

        var ticketId = new TicketId(taken.TicketId);
        // A fresh lease means a claim/run is already in flight (the lease is set
        // at claim time and renewed while the run executes) — don't re-enqueue.
        var lease = await activeRunLease.GetByTicketAsync(taken.Project, ticketId, ct);
        if (lease is not null && timeProvider.GetUtcNow() - lease.HeartbeatAt < ActiveRunReaper.LeaseFreshFor)
            return;

        var request = new PipelineRequest(
            taken.Project, taken.Pipeline,
            TicketId: ticketId,
            Headless: true,
            Context: await approvedSets.ContextForAsync(project.Tracker, taken.TicketId, ct));
        await jobQueue.EnqueueAsync(request, ct);
        logger.LogInformation(
            "Reconciler re-enqueued orphan taken ticket {Project}/{Ticket} (pipeline {Pipeline})",
            taken.Project, taken.TicketId, taken.Pipeline);
    }
}
