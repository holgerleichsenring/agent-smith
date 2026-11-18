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
/// 2026-11-17-3435b: after a refresh, a ticket of a merge: on_ci_green project whose newest attempt finished green with
/// its pull requests awaiting CI is merged once CI is green on every one of THOSE pull requests (an older attempt's
/// closed one does not block it) — or reports nothing for merge.ci_settle_seconds. Each head and verdict is acted on
/// once: only a merge-green run that ran marks the records, so a held lease is tried again on the next refresh.
/// </summary>
public sealed class CiGreenMerge(
    IServiceScopeFactory scopes, IPreviousAttemptReader attempts, IActiveRunLease leases, IMergeGreenRun run, TimeProvider clock)
{
    public async Task<bool> AfterRefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        if (project.Merge.When != MergeWhen.OnCiGreen) return false;
        var attempt = await attempts.LatestAsync(project.Name, ticketId, null, ct);
        if (attempt is not { Finished: true, Status: RunStatuses.Success } || attempt.AwaitingCiUrls.Count == 0) return false;
        using var scope = scopes.CreateScope();
        var records = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .ToDictionary(r => r.PrKey, StringComparer.Ordinal);
        var awaited = attempt.AwaitingCiUrls.Select(p => (Repo: p.Key, Url: p.Value, Record: records.GetValueOrDefault(PrKey.Of(p.Value)))).ToList();
        if (awaited.Any(a => a.Record?.Ci is not { } ci || !Green(a.Record, ci, project)) || awaited.All(a => Acted(a.Record!)))
            return false;
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. awaited.Select(a => new CiGreenPullRequest(a.Repo, a.Url))]);
        if (!await run.RunAsync(project, request, ct)) return false;
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        foreach (var (_, _, record) in awaited) await writer.MarkCiActedAsync(record!.PrKey, record.Ci!.ActedMark, ct);
        return true;
    }

    private bool Green(PrReviewRecordView record, PrRecordCiView ci, ResolvedProject project) =>
        record.State == PrReviewStates.Open && ci.IsOn(record.HeadSha)
        && (ci.State == CiStates.Green
            || (ci.State == CiStates.None && ci.At is { } since
                && clock.GetUtcNow() - since >= TimeSpan.FromSeconds(project.Merge.CiSettleSeconds)));

    private static bool Acted(PrReviewRecordView record) => record.CiActed == record.Ci!.ActedMark;
}
