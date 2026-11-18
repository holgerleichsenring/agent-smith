using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Deployment;

/// <summary>
/// 2026-11-18-3863f: one pass of the deploy follow, ridden by the pull-request re-read's leader loop — the unsettled
/// records least recently read, at most tickets.pr_reread_open_cap per pass (the re-read's own bound), each read when
/// due. Answers the tick the follow needs next: the smallest merge.ci_poll_seconds while any record is unsettled.
/// </summary>
public sealed class DeployFollowPass(IServiceScopeFactory scopes, DeployFollow follow, TimeProvider clock)
{
    public async Task<TimeSpan?> RunAsync(AgentSmithConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var scope = scopes.CreateScope();
        var records = scope.ServiceProvider.GetRequiredService<DeployRecordRepository>();
        var now = clock.GetUtcNow();
        foreach (var record in await records.UnsettledAsync(config.Tickets.PrRereadOpenCap, ct))
        {
            var project = config.Projects.GetValueOrDefault(record.Project);
            if (project is null || DeployFollowVerdict.IsDue(record, project.Merge, config.Tickets, now))
                await follow.FollowAsync(record, project, now, ct);
        }
        await records.SaveAsync(ct);
        return await records.AnyUnsettledAsync(ct) && config.Projects.Count > 0
            ? TimeSpan.FromSeconds(config.Projects.Values.Min(p => Math.Max(1, p.Merge.CiPollSeconds)))
            : null;
    }
}
