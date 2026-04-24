using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// Periodically scans InProgress tickets and reverts any whose heartbeat is
/// missing back to Pending. Handles crashed pods and stuck pipelines — any replica
/// can run this safely because status transitions are atomic per platform.
/// </summary>
public sealed class StaleJobDetector(
    IJobHeartbeatService heartbeat,
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitionerFactory transitionerFactory,
    IConfigurationLoader configLoader,
    string configPath,
    ILogger<StaleJobDetector> logger)
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

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
            await ScanProjectAsync(name, project, ct);
    }

    private async Task ScanProjectAsync(
        string projectName, ProjectConfig project, CancellationToken ct)
    {
        var provider = ticketFactory.Create(project.Tickets);
        var inProgress = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.InProgress, ct);
        if (inProgress.Count == 0) return;

        var transitioner = transitionerFactory.Create(project.Tickets);
        foreach (var ticket in inProgress)
            await RevertIfStaleAsync(transitioner, projectName, ticket, ct);
    }

    private async Task RevertIfStaleAsync(
        ITicketStatusTransitioner transitioner,
        string projectName, Domain.Entities.Ticket ticket, CancellationToken ct)
    {
        if (await heartbeat.IsAliveAsync(ticket.Id, ct)) return;

        var result = await transitioner.TransitionAsync(
            ticket.Id, TicketLifecycleStatus.InProgress, TicketLifecycleStatus.Pending, ct);
        logger.LogWarning(
            "Reverted stale InProgress → Pending for {Project}/{Ticket}: {Outcome}",
            projectName, ticket.Id.Value, result.Outcome);
    }
}
