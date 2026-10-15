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
/// five minutes it nudges a Refresh naming the record for open records of webhook-mode projects
/// (polling mode's PR sweep re-reads those) and unread records of every configured project, unchecked
/// for fifteen minutes, and for merged tickets with no landing — a lost delivery or a failed landing
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
    // Contract §5 default 9, pending operator ratification.
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Staleness = TimeSpan.FromMinutes(15);
    public const int OpenCap = 50;
    public const int MergedCap = 50;
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
            await Task.Delay(Interval, clock, ct);
        }
    }

    /// <summary>One cycle over the live configuration; answers how many Refresh nudges it enqueued.</summary>
    public async Task<int> ReReadOnceAsync(CancellationToken ct)
    {
        var projects = configLoader.LoadConfig(serverContext.ConfigPath).Projects.Values.ToList();
        var bound = (clock.GetUtcNow() - Staleness).UtcTicks;
        var selected = new List<PrReviewRecordView>();
        using (var scope = services.CreateScope())
        {
            var records = scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>();
            var webhook = projects.Where(p => !p.Tracker.Polling.Enabled).Select(p => p.Name).ToList();
            selected.AddRange(await records.OpenUncheckedAsync(webhook, [.. projects.Select(p => p.Name)], bound, OpenCap, ct));
            var merged = 0;
            foreach (var tracker in projects.GroupBy(p => p.Tracker.Name, StringComparer.Ordinal).TakeWhile(_ => merged < MergedCap))
            {
                var found = await records.UnlandedMergedAsync(tracker.Key, [.. tracker.Select(p => p.Name)], bound, MergedCap - merged, ct);
                merged += found.Count;
                selected.AddRange(found);
            }
        }
        var nudges = services.GetRequiredService<IReworkNudges>();
        var perTicket = selected.DistinctBy(r => (r.Project, r.TicketId)).ToList();
        foreach (var record in perTicket)
            await nudges.EnqueueAsync(new ReworkNudgeRequest(record.Project, record.TicketId, ReworkNudgeOrigin.Refresh, record.Url), ct);
        await services.GetRequiredService<Rework.DependentsWake>().RecoverAsync([.. projects.Select(p => p.Name)], ct); // 2026-10-14-dbceh
        return perTicket.Count;
    }
}
