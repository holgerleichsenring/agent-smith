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
/// each complete pass.
/// </summary>
public sealed class PrSweepRecordNudge(PrReviewRecordRepository records, IReworkNudges nudges)
{
    public async Task RunAsync(SweepTarget target, string repoKey, OpenPullRequestsPage page, string? resume, CancellationToken ct)
    {
        var listed = page.Items.Select(p => p.Number).ToHashSet(StringComparer.Ordinal);
        var complete = !page.Cut && resume is null;
        foreach (var record in await records.ByRepoKeyAsync(repoKey, ct))
            if (target.Allowed.Contains(record.Project) && Mismatched(record, listed, complete))
                await nudges.EnqueueAsync(new ReworkNudgeRequest(record.Project, record.TicketId, ReworkNudgeOrigin.Refresh, record.Url), ct);
    }

    private static bool Mismatched(PrReviewRecordView record, IReadOnlySet<string> listed, bool complete) =>
        record.State == PrReviewStates.Open
            ? complete && !listed.Contains(record.Number)
            : PrReviewStates.IsEnded(record.State) && listed.Contains(record.Number);
}
