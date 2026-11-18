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
/// <para>2026-11-17-3435b: the CI loop rides on it — it ticks at the smallest merge.ci_poll_seconds of the on_ci_green
/// projects (<see cref="CiWatchSelection"/>), nudges their CI-due records every tick and runs the rest at its interval.
/// 2026-11-18-3863f: every tick also runs the deploy follow, and ticks at its cadence while a deploy is unsettled.</para>
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
        var lastFull = DateTimeOffset.MinValue;
        while (!ct.IsCancellationRequested)
        {
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var interval = TimeSpan.FromSeconds(config.Tickets.PrRereadIntervalSeconds);
            var full = clock.GetUtcNow() - lastFull >= interval;
            TimeSpan? deploy = null; // 2026-11-18-3863f: the deploy follow rides on this loop and its lease
            try { await (full ? ReReadOnceAsync(ct) : CiOnceAsync(ct)); deploy = await services.GetRequiredService<Deployment.DeployFollowPass>().RunAsync(config, ct); }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogWarning(ex, "Pull-request re-read failed; retried next cycle"); }
            if (full) lastFull = clock.GetUtcNow();
            var ci = CiWatchSelection.Cadence(CiWatchSelection.Watched(config.Projects.Values)); // 2026-11-17-3435b
            await Task.Delay(new[] { ci, deploy, interval }.Min(t => t ?? interval), clock, ct);
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
        selected.AddRange(await CiDueAsync(projects, ct)); // 2026-11-17-3435b
        var count = await NudgeAsync(selected, ct);
        await services.GetRequiredService<Rework.DependentsWake>().RecoverAsync([.. projects.Select(p => p.Name)], tickets, ct); // 2026-10-14-dbceh
        return count;
    }

    /// <summary>2026-11-17-3435b: a CI tick between full re-reads — only the on_ci_green projects' CI-due records.</summary>
    public async Task<int> CiOnceAsync(CancellationToken ct) =>
        await NudgeAsync(await CiDueAsync([.. configLoader.LoadConfig(serverContext.ConfigPath).Projects.Values], ct), ct);

    private async Task<IReadOnlyList<PrReviewRecordView>> CiDueAsync(IReadOnlyList<ResolvedProject> projects, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        return await CiWatchSelection.DueAsync(CiWatchSelection.Watched(projects),
            scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>(), clock.GetUtcNow(), ct);
    }

    private async Task<int> NudgeAsync(IReadOnlyList<PrReviewRecordView> selected, CancellationToken ct)
    {
        var nudges = services.GetRequiredService<IReworkNudges>();
        var perTicket = selected.DistinctBy(r => (r.Project, r.TicketId)).ToList();
        foreach (var record in perTicket)
            await nudges.EnqueueAsync(new ReworkNudgeRequest(record.Project, record.TicketId, ReworkNudgeOrigin.Refresh, record.Url), ct);
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
