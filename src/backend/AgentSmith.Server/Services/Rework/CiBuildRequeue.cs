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
/// 2026-11-24-629aa: after a refresh, a ticket whose newest attempt finished and whose open, non-draft pull requests include
/// one whose required build has not started (or expired) on its head for merge.ci_settle_seconds gets one merge-green run that
/// queues those builds again — under every merge mode, since a build that never starts blocks a person's merge too. A
/// conflicting one is the conflict refresh's: Azure builds no pull request that cannot merge. Each spell of not_started on a
/// head is acted on once (<see cref="Mark"/>): one that does not take is not queued again, and one that expires after it ran
/// is a new spell. Only a run that ran marks the records, so a held lease is tried again on the next refresh. A host with no
/// such builds never reads not_started, so nothing is started for it.
/// </summary>
public sealed class CiBuildRequeue(
    IServiceScopeFactory scopes, IPreviousAttemptReader attempts, IActiveRunLease leases, IMergeGreenRun run, TimeProvider clock)
{
    public async Task<bool> AfterRefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var attempt = await attempts.LatestAsync(project.Name, ticketId, null, ct);
        if (attempt is not { Finished: true }) return false;
        using var scope = scopes.CreateScope();
        var urls = attempt.PullRequestUrls.Values.Concat(attempt.AwaitingCiUrls.Values).Select(PrKey.Of).ToHashSet(StringComparer.Ordinal);
        var due = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .Where(r => urls.Contains(r.PrKey) && Due(r, project)).ToList();
        if (due.Count == 0 || await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. due.Select(r => new CiGreenPullRequest(r.Repo, r.Url) { NotStarted = true })]);
        if (!await run.RunAsync(project, request, ct)) return false;
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        foreach (var record in due) await writer.MarkCiActedAsync(record.PrKey, Mark(record.Ci!), ct);
        return true;
    }

    /// <summary>'&lt;head&gt;:not_started@&lt;ticks&gt;' — the state's first read on the head names the spell; the CI watch's
    /// query compares marks only for decided states, so the suffix changes nothing there.</summary>
    internal static string Mark(PrRecordCiView ci) => $"{ci.ActedMark}@{ci.At?.UtcTicks}";

    // Open, out of draft, merges cleanly, not started on this head for the settle time, and not acted on yet.
    private bool Due(PrReviewRecordView record, ResolvedProject project) =>
        record.State == PrReviewStates.Open && !record.IsDraft && !record.HasConflicts
        && record.Ci is { State: CiStates.NotStarted, At: { } since } ci && ci.IsOn(record.HeadSha) && record.CiActed != Mark(ci)
        && clock.GetUtcNow() - since >= TimeSpan.FromSeconds(project.Merge.CiSettleSeconds);
}
