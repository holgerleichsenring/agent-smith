using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: one ticket's list row, composed from its code runs. The list and the detail
/// compose it here, so the row on the list and the header of the detail cannot disagree. Title and
/// platform come from the newest run that carries them.
/// </summary>
public sealed class TicketSummaryComposer(
    TicketAttempts attempts, TicketBucketRule buckets, TicketAttemptMarker marker, TicketKeyResolver keys,
    Checks.TicketStuckRule stuck)
{
    /// <summary>The spec status of a spec that was built through.</summary>
    private const string DoneSpec = "done";

    /// <param name="codeRows">Every code-pipeline run of the ticket that was read.</param>
    /// <param name="specStatuses">RunSpec statuses by run id; empty where specs are not wanted.</param>
    /// <param name="pullRequests">The ticket's pull requests from their records.</param>
    /// <param name="records">2026-10-13-9a59d: attempt records by run id; null where they are not wanted.</param>
    /// <param name="standing">2026-10-13-9a59h: the stored checks and hand-backs; null where they are not read.</param>
    /// <param name="launches">2026-10-13-9a59n: the attempts' last launches; null where they are not read.</param>
    public TicketSummaryView Compose(IReadOnlyList<TicketRunRow> codeRows, TicketLiveState live,
        ILookup<string, string> specStatuses, IReadOnlyList<TicketPullRequestView> pullRequests,
        IReadOnlyDictionary<string, TicketAttemptRecord>? records = null,
        TicketStanding? standing = null, IReadOnlyDictionary<string, RunLastLaunch>? launches = null)
    {
        var ordered = attempts.Of(codeRows);
        var head = attempts.Head(codeRows).Run;
        var check = standing?.CheckOf(head.Id);
        var unchanged = stuck.Unchanged(head, check, standing?.PrRecordsOf(head) ?? []);
        var state = buckets.Of(head, live, pullRequests, unchanged == true);
        var newestFirst = codeRows.Select(r => r.Run).OrderByDescending(r => r.Id, StringComparer.Ordinal).ToList();
        var platform = newestFirst.Select(r => r.Platform).FirstOrDefault(p => !string.IsNullOrEmpty(p));
        var headSpecs = specStatuses[head.Id].ToList();
        var record = ordered.Select(a => records?.GetValueOrDefault(a.Run.Id)).LastOrDefault(r => r is not null);
        var summary = new TicketSummaryView(
            head.Project, head.TicketId, keys.For(head.Project, platform, head.TicketId), platform,
            newestFirst.Select(r => r.TicketTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t)),
            state.Bucket, state.Kind, state.Since, ordered.Count, codeRows.Sum(r => r.Run.CostTotalUsd),
            head.Id, head.Status, state.LastOutcome, state.QueuePlace, Question(head, state),
            headSpecs.Count(s => s == DoneSpec), headSpecs.Count,
            [.. ordered.Select(a => marker.Mark(a.Run, live, specStatuses, launches))], pullRequests,
            record?.Cause, record?.TrackerState, record?.WebUrl,
            UnchangedSinceLastAttempt: unchanged, CheckedAt: check?.CheckedAt);
        return standing?.Apply(summary) ?? summary;
    }

    private static TicketPendingQuestion? Question(Run head, TicketBucketState state) =>
        state.Question is { } question ? new TicketPendingQuestion(head.Id, question) : null;
}
