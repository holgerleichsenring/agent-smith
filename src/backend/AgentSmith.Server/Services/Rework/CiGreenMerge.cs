using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-17-3435b: after a refresh, a ticket of a review: none project (2026-11-25-9992i) whose newest attempt finished green with
/// its pull requests awaiting CI is merged once CI is green on every one of THOSE pull requests (an older attempt's
/// closed one does not block it) — or reports nothing for merge.ci_settle_seconds. Each head and verdict is acted on
/// once: only a merge-green run that ran marks the records, so a held lease is tried again on the next refresh.
/// <para>2026-11-24-f9c5c: a pull request into a series branch is named on the request (it merges past protection) and merges
/// on green only: one that reports nothing is the series master's to start.</para>
/// </summary>
public sealed class CiGreenMerge(
    IServiceScopeFactory scopes, IPreviousAttemptReader attempts, IActiveRunLease leases, IMergeGreenRun run, TimeProvider clock)
{
    public async Task<bool> AfterRefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        if (project.Review != ReviewMode.None) return false; // 2026-11-25-9992i
        var attempt = await attempts.LatestAsync(project.Name, ticketId, null, ct);
        if (attempt is not { Finished: true, Status: RunStatuses.Success } || attempt.AwaitingCiUrls.Count == 0) return false;
        using var scope = scopes.CreateScope();
        var records = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .ToDictionary(r => r.PrKey, StringComparer.Ordinal);
        var awaited = attempt.AwaitingCiUrls.Select(p => (Repo: p.Key, Url: p.Value, Record: records.GetValueOrDefault(PrKey.Of(p.Value)))).ToList();
        // 2026-11-24-f9c5c: a target not read yet waits for the read — the protection and the base guard both turn on it.
        if (awaited.Any(a => a.Record is { TargetBranch: null })) return false;
        var series = SeriesTarget(awaited.Select(a => a.Record));
        if (awaited.Any(a => a.Record?.Ci is not { } ci || !Green(a.Record, ci, project, series is not null)) || awaited.All(a => Acted(a.Record!)))
            return false;
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        // 2026-11-24-6811d: a refresh-base run at work on one of them merges the base first, and one that refused it found the
        // merged tree conflicting or red — the head is merged once a refresh pushed or a person moved it.
        var refreshes = await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>()
            .RefreshesAsync([.. awaited.Select(a => a.Url)], ct);
        if (awaited.Any(a => refreshes.TryGetValue(a.Url, out var said) && said.Stands(a.Record?.HeadMovedAt))) return false;
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. awaited.Select(a => new CiGreenPullRequest(a.Repo, a.Url))]) { SeriesBranch = series };
        if (!await run.RunAsync(project, request, ct)) return false;
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        foreach (var (_, _, record) in awaited) await writer.MarkCiActedAsync(record!.PrKey, record.Ci!.ActedMark, ct);
        return true;
    }

    /// <summary>2026-11-24-f9c5c: the series branch the pull requests target, read from their records; null when any targets
    /// another branch or its target is not read yet.</summary>
    internal static string? SeriesTarget(IEnumerable<PrReviewRecordView?> records)
    {
        var targets = records.Select(r => r?.TargetBranch).Distinct(StringComparer.Ordinal).ToList();
        return targets is [{ } only] && AgentSmith.Contracts.Providers.WorkBranches.IsSeries(only) ? only : null;
    }

    private bool Green(PrReviewRecordView record, PrRecordCiView ci, ResolvedProject project, bool intoSeries) =>
        record.State == PrReviewStates.Open && !record.HasConflicts && ci.IsOn(record.HeadSha) // 6811a: a conflict cannot merge
        && (ci.State == CiStates.Green
            || (!intoSeries && ci.State == CiStates.None && ci.At is { } since
                && clock.GetUtcNow() - since >= TimeSpan.FromSeconds(project.Merge.CiSettleSeconds)));

    private static bool Acted(PrReviewRecordView record) => record.CiActed == record.Ci!.ActedMark;
}
