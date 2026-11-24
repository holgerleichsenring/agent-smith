using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-24-f9c5d: after a refresh, a ticket whose newest attempt finished green and whose open pull requests into a series
/// branch include one that reported no CI on its head for merge.ci_settle_seconds gets one merge-green run that starts that
/// CI first (<see cref="AgentSmith.Application.Services.Handlers.PullRequestCiStart"/>) — under every merge.series_when, since a
/// person reviewing each ticket wants its CI too. Each such head is acted on once: only a run that ran marks the records, so a
/// held lease is tried again on the next refresh. Runs before the CI merge, which never counts a series head without CI green.
/// </summary>
public sealed class SeriesCiStart(
    IServiceScopeFactory scopes, IPreviousAttemptReader attempts, IActiveRunLease leases, IMergeGreenRun run, TimeProvider clock)
{
    public async Task<bool> AfterRefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var attempt = await attempts.LatestAsync(project.Name, ticketId, null, ct);
        if (attempt is not { Finished: true, Status: RunStatuses.Success }) return false;
        using var scope = scopes.CreateScope();
        var urls = attempt.PullRequestUrls.Values.Concat(attempt.AwaitingCiUrls.Values).Select(PrKey.Of).ToHashSet(StringComparer.Ordinal);
        var series = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .Where(r => r.State == PrReviewStates.Open && urls.Contains(r.PrKey) && WorkBranches.IsSeries(r.TargetBranch)).ToList();
        var silent = series.Where(r => Silent(r, project)).ToList();
        if (silent.Count == 0 || await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. series.Select(r => new CiGreenPullRequest(r.Repo, r.Url) { NoCi = silent.Contains(r) })])
        {
            SeriesBranch = series.Select(r => r.TargetBranch).Distinct(StringComparer.Ordinal).Count() == 1 ? series[0].TargetBranch : null,
        };
        if (!await run.RunAsync(project, request, ct)) return false;
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        foreach (var record in silent) await writer.MarkCiActedAsync(record.PrKey, record.Ci!.ActedMark, ct);
        return true;
    }

    // No CI reported on this head for the settle time, and not acted on yet.
    private bool Silent(PrReviewRecordView record, ResolvedProject project) =>
        record.Ci is { State: CiStates.None, At: { } since } ci && ci.IsOn(record.HeadSha) && record.CiActed != ci.ActedMark
        && clock.GetUtcNow() - since >= TimeSpan.FromSeconds(project.Merge.CiSettleSeconds);
}
