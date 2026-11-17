using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-14-dbceg: the periodic re-read of pull-request records, under its own leader lease. Every
/// tickets.pr_reread_interval_seconds (2026-11-17-f9b0d) it nudges a Refresh naming the record for open records of webhook-mode projects
/// (polling mode's PR sweep re-reads those) and unread records of every configured project, unchecked
/// for tickets.pr_reread_stale_seconds, and for merged tickets with no landing — a lost delivery or a failed landing
/// write. A named record is re-read, so its check time advances and it goes to the back of the list.
/// 2026-10-14-dbceh: then it recovers a lost wake of the tickets the day's landings freed.
/// Collaborators resolve at run time, as <see cref="ChangeSweepHostedService"/>'s do.
/// </summary>
public sealed class PrRecordReReadHostedService(
    IServiceProvider services,
    ServerContext serverContext,
    IConfigurationLoader configLoader,
    TimeProvider clock,
    ILogger<PrRecordReReadHostedService> logger) : BackgroundService
{
    private const string LeaseKey = "agentsmith:leader:pr-record-reread";
    private readonly SubsystemHealth _health = new("pr-record-reread");

    public ISubsystemHealth Health => _health;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retry = configLoader.LoadConfig(serverContext.ConfigPath).Queue.RedisRetryIntervalSeconds;
        return LeaderSubsystemRunner.RunAsync(services, _health, LeaseKey, LoopAsync, retry, stoppingToken);
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await ReReadOnceAsync(ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "Pull-request re-read failed; retried next cycle"); }
            var interval = configLoader.LoadConfig(serverContext.ConfigPath).Tickets.PrRereadIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(interval), clock, ct);
        }
    }

    /// <summary>One cycle over the live configuration; answers how many Refresh nudges it enqueued.</summary>
    public async Task<int> ReReadOnceAsync(CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        var projects = config.Projects.Values.ToList();
        var tickets = config.Tickets; // 2026-11-17-f9b0d: cadence, staleness and caps from the tickets: block
        var bound = (clock.GetUtcNow() - TimeSpan.FromSeconds(tickets.PrRereadStaleSeconds)).UtcTicks;
        var selected = new List<PrReviewRecordView>();
        using (var scope = services.CreateScope())
        {
            var records = scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>();
            var webhook = projects.Where(p => !p.Tracker.Polling.Enabled).Select(p => p.Name).ToList();
            selected.AddRange(await records.OpenUncheckedAsync(webhook, [.. projects.Select(p => p.Name)], bound, tickets.PrRereadOpenCap, ct));
            var merged = 0;
            foreach (var tracker in projects.GroupBy(p => p.Tracker.Name, StringComparer.Ordinal).TakeWhile(_ => merged < tickets.PrRereadMergedCap))
            {
                var found = await records.UnlandedMergedAsync(tracker.Key, [.. tracker.Select(p => p.Name)], bound, tickets.PrRereadMergedCap - merged, ct);
                merged += found.Count;
                selected.AddRange(found);
            }
        }
        AtCaps(selected.Count, tickets);
        var nudges = services.GetRequiredService<IReworkNudges>();
        var perTicket = selected.DistinctBy(r => (r.Project, r.TicketId)).ToList();
        foreach (var record in perTicket)
            await nudges.EnqueueAsync(new ReworkNudgeRequest(record.Project, record.TicketId, ReworkNudgeOrigin.Refresh, record.Url), ct);
        await services.GetRequiredService<Rework.DependentsWake>().RecoverAsync([.. projects.Select(p => p.Name)], tickets, ct); // 2026-10-14-dbceh
        return perTicket.Count;
    }

    private void AtCaps(int named, TicketsConfig tickets)
    {
        if (named < tickets.PrRereadOpenCap) return;
        logger.LogInformation("The pull-request re-read named {Count} records, at tickets.pr_reread_open_cap = {Open} and "
            + "tickets.pr_reread_merged_cap = {Merged}, its bounds per pass; the rest are read next pass",
            named, tickets.PrRereadOpenCap, tickets.PrRereadMergedCap);
    }
}
