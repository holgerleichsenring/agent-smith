using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// 2026-10-08-10b0: polling mode's pull-request events, after the rework sources of each sweep cycle.
/// Every repository the polling entries' projects declare is read once a cycle, starting where the
/// last cycle's deadline cut it off; a repository's own budget cut resumes at its stored page. Routes
/// are limited to the polling projects that declare the repository.
/// </summary>
public sealed class PrSweep(
    IServiceScopeFactory scopes, ISourceProviderFactory sources, PrSweepActions actions, PrTriggerLabelResolver labels,
    TimeProvider time, ILogger<PrSweep> logger)
{
    private readonly Dictionary<string, int> _commentTurns = new(StringComparer.Ordinal);
    private int _rotation;

    public async Task RunAsync(AgentSmithConfig config, IReadOnlyList<TrackerConnection> polling, TimeSpan interval, TimeSpan deadline, CancellationToken ct)
    {
        var targets = Targets(config, polling);
        if (targets.Count == 0) return;
        var until = time.GetUtcNow() + deadline;
        var start = _rotation++ % targets.Count;
        for (var i = 0; i < targets.Count && time.GetUtcNow() < until && !ct.IsCancellationRequested; i++)
            await OneAsync(targets[(start + i) % targets.Count], interval, ct);
    }

    private async Task OneAsync(SweepTarget target, TimeSpan interval, CancellationToken ct)
    {
        if (sources.Create(target.Repo) is not IOpenPullRequestSource source) return;
        using var scope = scopes.CreateScope();
        var cursors = scope.ServiceProvider.GetRequiredService<ISweepCursors>();
        var key = $"prsweep:{RepoKey.Of(target.Repo.Url!)}";
        try
        {
            var from = await cursors.GetAsync(key, ct);
            var now = time.GetUtcNow();
            var turn = _commentTurns[key] = _commentTurns.GetValueOrDefault(key) + 1;
            var resume = await new PrSweepPass(scope.ServiceProvider.GetRequiredService<PrSweepStore>(), actions, labels)
                .RunAsync(target, source.OpenPullRequests(), from?.Resume, interval, now, turn, ct);
            await cursors.AdvanceAsync(key, new SweepPosition(now, resume), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "PR sweep of {Repo} failed", target.Repo.Url);
        }
    }

    private static List<SweepTarget> Targets(AgentSmithConfig config, IReadOnlyList<TrackerConnection> polling)
    {
        var entries = polling.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var projects = config.Projects.Values.Where(p => entries.Contains(p.Tracker.Name)).ToList();
        return [.. projects.SelectMany(p => p.Repos).Where(r => r.Url is not null).GroupBy(r => RepoKey.Of(r.Url!))
            .Select(g => new SweepTarget(config, g.First(),
                projects.Where(p => p.Repos.Any(r => r.Url is not null && RepoKey.Of(r.Url) == g.Key)).Select(p => p.Name).ToHashSet(StringComparer.Ordinal)))];
    }
}
