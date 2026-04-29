using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically reconciles Enqueued tickets. For any ticket in Enqueued status with
/// no heartbeat, re-pushes a PipelineRequest onto IRedisJobQueue. Covers Redis loss,
/// crashed pre-consume pods, and enqueue failures from TicketClaimService. Runs on
/// every replica unconditionally — leader-election is deferred to p96.
/// </summary>
public sealed class EnqueuedReconciler(
    IJobHeartbeatService heartbeat,
    IRedisJobQueue jobQueue,
    ITicketProviderFactory ticketFactory,
    IConfigurationLoader configLoader,
    IPipelineConfigResolver pipelineConfigResolver,
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
            await ReconcileProjectAsync(name, project, ct);
    }

    private async Task ReconcileProjectAsync(
        string projectName, ProjectConfig project, CancellationToken ct)
    {
        var provider = ticketFactory.Create(project.Tickets);
        var enqueued = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Enqueued, ct);

        foreach (var ticket in enqueued)
        {
            if (await heartbeat.IsAliveAsync(ticket.Id, ct)) continue;

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

    private string? TryResolveDefaultPipeline(ProjectConfig project)
    {
        try { return pipelineConfigResolver.ResolveDefaultPipelineName(project); }
        catch (InvalidOperationException) { return null; }
    }
}
