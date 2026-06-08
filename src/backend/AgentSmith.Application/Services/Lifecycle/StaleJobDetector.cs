using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically scans InProgress tickets and reverts any whose heartbeat is
/// missing back to Pending. Handles crashed pods and stuck pipelines — any replica
/// can run this safely because status transitions are atomic per platform.
///
/// p0242: a revert authoritatively CANCELS the run it reverts (run-cancellation
/// registry + a cross-process RunCancelRequestedEvent) BEFORE flipping the ticket,
/// so it can never leave an old run alive next to a fresh spawn. The cancelled
/// run's RunFinished releases its lease, which is what actually frees the ticket.
/// </summary>
public sealed class StaleJobDetector(
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitionerFactory transitionerFactory,
    IActiveRunLease activeRunLease,
    IRunCancellationRegistry cancellationRegistry,
    IEventPublisher eventPublisher,
    TimeProvider timeProvider,
    IConfigurationLoader configLoader,
    string configPath,
    ILogger<StaleJobDetector> logger)
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);
    // p0242: matches the ActiveRunReaper's stale threshold — a lease whose DB
    // heartbeat is younger than this is a live run (the heartbeat pump renews it
    // every 45s while the run executes), so the ticket must not be reverted.
    // p0252: internal so EnqueuedReconciler shares the one freshness threshold.
    internal static readonly TimeSpan LeaseFreshFor = TimeSpan.FromMinutes(3);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("StaleJobDetector started (interval: {Interval})", ScanInterval);
        while (!cancellationToken.IsCancellationRequested)
        {
            try { await ScanOnceAsync(cancellationToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { logger.LogError(ex, "StaleJobDetector scan failed"); }

            try { await Task.Delay(ScanInterval, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ScanOnceAsync(CancellationToken ct)
    {
        var config = configLoader.LoadConfig(configPath);
        foreach (var (name, project) in config.Projects)
            await ScanProjectSafeAsync(name, project, ct);
    }

    private async Task ScanProjectSafeAsync(
        string projectName, ResolvedProject project, CancellationToken ct)
    {
        try { await ScanProjectAsync(projectName, project, ct); }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "StaleJobDetector skipped project {Project} (Tickets.Type={Type}): {Message}",
                projectName, project.Tracker.Type, ex.Message);
        }
    }

    private async Task ScanProjectAsync(
        string projectName, ResolvedProject project, CancellationToken ct)
    {
        var provider = ticketFactory.Create(project.Tracker);
        var inProgress = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.InProgress, ct);
        if (inProgress.Count == 0) return;

        var transitioner = transitionerFactory.Create(project.Tracker);
        foreach (var ticket in inProgress)
            await RevertIfStaleAsync(transitioner, projectName, ticket, ct);
    }

    private async Task RevertIfStaleAsync(
        ITicketStatusTransitioner transitioner,
        string projectName, Domain.Entities.Ticket ticket, CancellationToken ct)
    {
        // p0242 (DB-authority): liveness comes from the active-run LEASE in the DB
        // — flush-proof, unlike the Redis heartbeat. A lease whose heartbeat is
        // still fresh is a LIVE run: never revert it, even when Redis was flushed
        // (the old Redis-only check would have reverted every live ticket on a
        // flush — the empty-Redis meltdown). Only a stale/absent lease is a dead
        // or hung run to revert.
        var lease = await activeRunLease.GetByTicketAsync(projectName, ticket.Id, ct);
        if (lease is not null && timeProvider.GetUtcNow() - lease.HeartbeatAt < LeaseFreshFor)
            return;

        // p0252: the lease is the SOLE liveness source — the Redis-heartbeat
        // secondary check is gone. Every in-flight run now holds a lease (the claim
        // INSERTs it, a direct-spawn run UPSERTs it at AttachRun), so a stale/absent
        // lease is an unambiguously dead or hung run to revert.

        // Never revert-without-cancel: if a stale lease still names a run, cancel it
        // (registry for THIS replica + a cross-process event for another) BEFORE
        // flipping the ticket. The cancelled run releases its own lease on
        // RunFinished — releasing here would re-open the duplicate-spawn window.
        if (lease?.RunId is { Length: > 0 } runId)
        {
            cancellationRegistry.TryCancel(runId, "stale-revert");
            await eventPublisher.PublishAsync(
                new RunCancelRequestedEvent(runId, "stale-revert", timeProvider.GetUtcNow()), ct);
            logger.LogWarning(
                "Stale-revert cancelled run {Run} for {Project}/{Ticket}", runId, projectName, ticket.Id.Value);
        }

        var result = await transitioner.TransitionAsync(
            ticket.Id, TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Pending, ct);
        logger.LogWarning(
            "Reverted stale InProgress → Pending for {Project}/{Ticket}: {Outcome}",
            projectName, ticket.Id.Value, result.Outcome);
    }
}
