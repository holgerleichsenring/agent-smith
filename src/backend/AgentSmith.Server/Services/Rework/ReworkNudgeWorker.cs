using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-0781: drains the rework nudge queue on every replica. A due nudge is claimed by
/// compare-and-set, handled under the ticket's own lock — a busy lock means another replica is
/// handling that ticket, so the nudge waits — and then finished or rescheduled at the generation it
/// was claimed at. A handler failure reschedules; nothing is dropped by an exception.
/// </summary>
public sealed class ReworkNudgeWorker(
    IServiceScopeFactory scopes,
    IRedisClaimLock locks,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ReworkNudgeHandler handler,
    ILogger<ReworkNudgeWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);
    private const int Batch = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Rework nudge drain failed; retrying");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    public async Task DrainAsync(CancellationToken ct)
    {
        IReadOnlyList<ClaimedReworkNudge> claimed;
        using (var scope = scopes.CreateScope())
            claimed = await scope.ServiceProvider.GetRequiredService<ReworkNudgeRepository>().ClaimDueAsync(Batch, ct);
        foreach (var nudge in claimed) await OneAsync(nudge, ct);
    }

    private async Task OneAsync(ClaimedReworkNudge nudge, CancellationToken ct)
    {
        var key = $"agentsmith:rework-nudge:{nudge.Project}:{nudge.TicketId}";
        var token = await locks.TryAcquireAsync(key, ReworkNudgeRepository.ClaimLease, ct);
        if (token is null)
        {
            await SettleAsync(nudge, ReworkNudgeDisposition.Reschedule, ct);
            return;
        }
        try
        {
            await SettleAsync(nudge, await HandleAsync(nudge, ct), ct);
        }
        finally
        {
            await locks.ReleaseAsync(key, token, CancellationToken.None);
        }
    }

    private async Task<ReworkNudgeDisposition> HandleAsync(ClaimedReworkNudge nudge, CancellationToken ct)
    {
        try
        {
            if (!configLoader.LoadConfig(serverContext.ConfigPath).Projects.TryGetValue(nudge.Project, out var project))
                return ReworkNudgeDisposition.Finish;
            return await handler.HandleAsync(project, nudge, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Rework nudge for {Project}/#{Ticket} failed; checked again later", nudge.Project, nudge.TicketId);
            return ReworkNudgeDisposition.Reschedule;
        }
    }

    private async Task SettleAsync(ClaimedReworkNudge nudge, ReworkNudgeDisposition disposition, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ReworkNudgeRepository>();
        if (disposition == ReworkNudgeDisposition.Finish) await repo.FinishAsync(nudge, ct);
        else await repo.RescheduleAsync(nudge, Backoff(nudge.Tries), ct);
    }

    /// <summary>15 s doubling per try, at most 15 minutes.</summary>
    public static TimeSpan Backoff(int tries) =>
        TimeSpan.FromSeconds(Math.Min(15 * 60, 15 * Math.Pow(2, Math.Min(tries, 10))));
}
