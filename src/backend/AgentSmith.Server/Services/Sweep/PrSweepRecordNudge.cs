using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Sweep;

/// <summary>
/// Polling mode's merges, closes and reopens: the repository's pull-request records against the open
/// list the PR sweep read. A record still open that a complete list — neither cut nor resumed — does
/// not name was merged or closed (also one merged before any sweep saw it open); a merged or closed
/// record the list names was reopened. Each is a late Refresh nudge with the record's URL, limited to
/// the polling projects; a record the host can no longer read stays mismatched and is nudged again
/// each complete pass. 2026-10-15-114fc: an open record the list names with another head — or with no
/// stored head yet, its backfill — is nudged too, so the refresh can stamp the head move.
/// </summary>
public sealed class PrSweepRecordNudge(PrReviewRecordRepository records, IReworkNudges nudges)
{
    public async Task RunAsync(SweepTarget target, string repoKey, OpenPullRequestsPage page, string? resume, CancellationToken ct)
    {
        var listed = page.Items.GroupBy(p => p.Number, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().HeadSha, StringComparer.Ordinal);
        var complete = !page.Cut && resume is null;
        foreach (var record in await records.ByRepoKeyAsync(repoKey, ct))
            if (target.Allowed.Contains(record.Project) && (Mismatched(record, listed, complete) || HeadMoved(record, listed)))
                await nudges.EnqueueAsync(new ReworkNudgeRequest(record.Project, record.TicketId, ReworkNudgeOrigin.Refresh, record.Url), ct);
    }

    private static bool Mismatched(PrReviewRecordView record, Dictionary<string, string?> listed, bool complete) =>
        record.State == PrReviewStates.Open
            ? complete && !listed.ContainsKey(record.Number)
            : PrReviewStates.IsEnded(record.State) && listed.ContainsKey(record.Number);

    private static bool HeadMoved(PrReviewRecordView record, Dictionary<string, string?> listed) =>
        record.State == PrReviewStates.Open && listed.TryGetValue(record.Number, out var head) && head is not null
            && !string.Equals(head, record.HeadSha, StringComparison.Ordinal);
}
