using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically reconciles Enqueued tickets. For any ticket in Enqueued status with
/// no FRESH active-run lease, re-pushes a PipelineRequest onto IRedisJobQueue. Covers
/// Redis loss, crashed pre-consume pods, and enqueue failures from TicketClaimService.
/// Runs on every replica unconditionally — leader-election is deferred to p96.
///
/// p0252: liveness is the DB lease (set at claim, renewed while the run executes),
/// not the volatile Redis heartbeat — the same source StaleJobDetector reverts against.
/// </summary>
public sealed class EnqueuedReconciler(
    IActiveRunLease activeRunLease,
    IRedisJobQueue jobQueue,
    ITicketProviderFactory ticketFactory,
    IConfigurationLoader configLoader,
    IPipelineConfigResolver pipelineConfigResolver,
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
        foreach (var (name, project) in config.Projects)
            await ReconcileProjectSafeAsync(name, project, ct);
    }

    private async Task ReconcileProjectSafeAsync(
        string projectName, ResolvedProject project, CancellationToken ct)
    {
        try { await ReconcileProjectAsync(projectName, project, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "EnqueuedReconciler skipped project {Project} (Tickets.Type={Type}): {Message}",
                projectName, project.Tracker.Type, ex.Message);
        }
    }

    private async Task ReconcileProjectAsync(
        string projectName, ResolvedProject project, CancellationToken ct)
    {
        var provider = ticketFactory.Create(project.Tracker);
        var enqueued = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Enqueued, ct);

        foreach (var ticket in enqueued)
        {
            // A fresh lease means a claim/run is already in flight (the lease is set
            // at claim time and renewed while the run executes) — don't re-enqueue.
            var lease = await activeRunLease.GetByTicketAsync(projectName, ticket.Id, ct);
            if (lease is not null && timeProvider.GetUtcNow() - lease.HeartbeatAt < StaleJobDetector.LeaseFreshFor)
                continue;

            var pipeline = TryResolveDefaultPipeline(project) ?? "fix-bug";
            var request = new PipelineRequest(
                projectName, pipeline,
                TicketId: ticket.Id,
                Headless: true);
            await jobQueue.EnqueueAsync(request, ct);
            logger.LogInformation(
                "Reconciler re-enqueued orphan Enqueued ticket {Project}/{Ticket}",
                projectName, ticket.Id.Value);
        }
    }

    private string? TryResolveDefaultPipeline(ResolvedProject project)
    {
        try { return pipelineConfigResolver.ResolveDefaultPipelineName(project); }
        catch (InvalidOperationException) { return null; }
    }
}
