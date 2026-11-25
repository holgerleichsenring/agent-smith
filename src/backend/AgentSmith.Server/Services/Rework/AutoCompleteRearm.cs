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
/// 2026-11-24-629ab: after a refresh, a ticket whose newest attempt finished and whose open, non-draft pull requests include
/// one whose auto-complete the host dropped at a conflict (<see cref="PrReviewRecordView.AutoCompleteLostAt"/>) and which the
/// host now says merges cleanly gets one merge-green run that arms it again — under every merge mode: whoever set it decided
/// the merge, and the host's own completion never goes past a branch policy. Each head is armed again once: only a run that ran marks it, so a
/// held lease is tried again on the next refresh, and a refused arm (said on the merge-green run) waits for a new head or a
/// person's arm — one try per head, as every CI-loop act, rather than a run every poll against a host that refuses.
/// </summary>
public sealed class AutoCompleteRearm(IServiceScopeFactory scopes, IPreviousAttemptReader attempts, IActiveRunLease leases, IMergeGreenRun run)
{
    public async Task<bool> AfterRefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var due = (await scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>().ByTicketAsync(project.Name, ticketId, ct))
            .Where(Due).ToList();
        if (due.Count == 0 || await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { Finished: true } attempt) return false;
        if (await leases.GetByTicketAsync(project.Name, new TicketId(ticketId), ct) is not null) return false;
        var request = new CiGreenMergeRequest(ticketId, attempt.RunId, TicketBranchNamer.Compose(new TicketId(ticketId)).Value,
            [.. due.Select(r => new CiGreenPullRequest(r.Repo, r.Url) { Rearm = true })]);
        if (!await run.RunAsync(project, request, ct)) return false;
        var writer = scope.ServiceProvider.GetRequiredService<PrReviewRecordWriter>();
        foreach (var record in due) await writer.MarkRearmedAsync(record.PrKey, record.HeadSha!, ct);
        return true;
    }

    // Open, out of draft, lost at a conflict the host now says has ended, and not armed again on this head.
    private static bool Due(PrReviewRecordView record) =>
        record.State == PrReviewStates.Open && !record.IsDraft && record.MergesCleanly && record.AutoCompleteLostAt is not null
        && record.HeadSha is { } head && !string.Equals(record.AutoCompleteRearmedHead, head, StringComparison.Ordinal);
}
