using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
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
    IEnvelopeProjectResolver envelopeResolver,
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
            await ReconcileProjectSafeAsync(config, name, project, ct);
    }

    private async Task ReconcileProjectSafeAsync(
        AgentSmithConfig config, string projectName, ResolvedProject project, CancellationToken ct)
    {
        try { await ReconcileProjectAsync(config, projectName, project, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "EnqueuedReconciler skipped project {Project} (Tickets.Type={Type}): {Message}",
                projectName, project.Tracker.Type, ex.Message);
        }
    }

    private async Task ReconcileProjectAsync(
        AgentSmithConfig config, string projectName, ResolvedProject project, CancellationToken ct)
    {
        var provider = ticketFactory.Create(project.Tracker);
        var enqueued = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Enqueued, ct);

        foreach (var ticket in enqueued)
        {
            // On a shared tracker ListByLifecycleStatusAsync returns EVERY project's Enqueued
            // tickets. Re-enqueue only the ones whose labels route to THIS project, through the
            // same IEnvelopeProjectResolver the poller claims through — otherwise a ticket owned
            // by project B is re-enqueued once per project sharing the tracker (duplicate runs).
            var match = ResolveMatch(config, projectName, project, ticket);
            if (match is null) continue;

            // A fresh lease means a claim/run is already in flight (the lease is set
            // at claim time and renewed while the run executes) — don't re-enqueue.
            var lease = await activeRunLease.GetByTicketAsync(projectName, ticket.Id, ct);
            if (lease is not null && timeProvider.GetUtcNow() - lease.HeartbeatAt < ActiveRunReaper.LeaseFreshFor)
                continue;

            var request = new PipelineRequest(
                projectName, match.Value.PipelineName,
                TicketId: ticket.Id,
                Headless: true);
            await jobQueue.EnqueueAsync(request, ct);
            logger.LogInformation(
                "Reconciler re-enqueued orphan Enqueued ticket {Project}/{Ticket} (pipeline {Pipeline})",
                projectName, ticket.Id.Value, match.Value.PipelineName);
        }
    }

    private ProjectMatch? ResolveMatch(
        AgentSmithConfig config, string projectName, ResolvedProject project, Ticket ticket)
    {
        var envelope = new IncomingTicketEnvelope
        {
            Labels = ticket.Labels ?? [],
            TicketId = ticket.Id.Value,
            Platform = project.Tracker.Type.ToString().ToLowerInvariant(),
        };
        foreach (var m in envelopeResolver.Resolve(config, envelope))
            if (string.Equals(m.ProjectName, projectName, StringComparison.Ordinal))
                return m;
        return null;
    }
}
