using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Checks;

/// <summary>
/// 2026-10-13-9a59h: runs the checks the list read kicked, one at a time, each in its own scope. A check
/// that throws costs that ticket's check, never the worker.
/// </summary>
public sealed class TicketCheckKickWorker(
    TicketCheckKicks kicks, IServiceScopeFactory scopes, ILogger<TicketCheckKickWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var kick in kicks.Reader.ReadAllAsync(stoppingToken))
                await RunAsync(kick, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    internal async Task RunAsync(TicketCheckKick kick, CancellationToken ct)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<TicketBetweenAttemptsChecker>()
                .CheckAsync(kick.Project, kick.TicketId, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The between-attempts check of ticket {Ticket} in {Project} failed",
                kick.TicketId, kick.Project);
        }
    }
}
