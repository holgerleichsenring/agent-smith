using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Holds;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// 2026-10-13-9a59a: one ticket's list row, composed from its code runs. The list and the detail
/// compose it here, so the row on the list and the header of the detail cannot disagree. Title and
/// platform come from the newest run that carries them.
/// <para>2026-11-23-31dcg: and its hold, read here for the list and the detail alike (<see cref="TicketHoldReader"/>), which
/// the bucket is laid under (<see cref="TicketBucketRule.Held"/>).</para>
/// </summary>
public sealed class TicketSummaryComposer(
    TicketAttempts attempts, TicketBucketRule buckets, TicketAttemptMarker marker, TicketKeyResolver keys,
    Checks.TicketStuckRule stuck, TicketHoldReader holds)
{
    /// <summary>The spec status of a spec that was built through.</summary>
    private const string DoneSpec = "done";

    /// <param name="codeRows">Every code-pipeline run of the ticket that was read.</param>
    /// <param name="specStatuses">RunSpec statuses by run id; empty where specs are not wanted.</param>
    /// <param name="pullRequests">The ticket's pull requests from their records.</param>
    /// <param name="records">2026-10-13-9a59d: attempt records by run id; null where they are not wanted.</param>
    /// <param name="standing">2026-10-13-9a59h: the stored checks and hand-backs; null where they are not read.</param>
    /// <param name="launches">2026-10-13-9a59n: the attempts' last launches; null where they are not read.</param>
    /// <param name="derivationCost">2026-10-26-af69: the ticket's spec derivations' summed cost, part of its cost and nothing else.</param>
    /// <param name="inputs">2026-11-23-31dcg: the read's series, merge order, open text edits and restart window; none where not read.</param>
    public TicketSummaryView Compose(IReadOnlyList<TicketRunRow> codeRows, TicketLiveState live,
        ILookup<string, string> specStatuses, IReadOnlyList<TicketPullRequestView> pullRequests,
        IReadOnlyDictionary<string, TicketAttemptRecord>? records = null,
        TicketStanding? standing = null, IReadOnlyDictionary<string, RunLastLaunch>? launches = null,
        decimal derivationCost = 0m, TicketHoldInputs? inputs = null)
    {
        var ordered = attempts.Of(codeRows);
        var head = attempts.Head(codeRows).Run;
        var check = standing?.CheckOf(head.Id);
        var unchanged = stuck.Unchanged(head, check, standing?.PrRecordsOf(head) ?? []);
        var closed = standing?.ClosedOf(head);
        var marks = ordered.Select(a => marker.Mark(a.Run, live, specStatuses, launches, records?.GetValueOrDefault(a.Run.Id))).ToList();
        var standingState = buckets.Of(head, live, pullRequests, unchanged == true, standing?.LandedAt(head), closed,
            standing?.ClosedForWorkOf(head) == true);
        var number = ordered.Select(a => a.Run.Id).ToList().IndexOf(head.Id) + 1;
        var hold = holds.Of(Facts(head, number, standingState, live, standing,
            marks.FirstOrDefault(m => m.RunId == head.Id)?.EndedOnInfrastructure == true, inputs ?? TicketHoldInputs.None));
        var state = buckets.Held(standingState, hold, live.Pending.GetValueOrDefault(head.Id));
        var newestFirst = codeRows.Select(r => r.Run).OrderByDescending(r => r.Id, StringComparer.Ordinal).ToList();
        var platform = newestFirst.Select(r => r.Platform).FirstOrDefault(p => !string.IsNullOrEmpty(p));
        var headSpecs = specStatuses[head.Id].ToList();
        var record = ordered.Select(a => records?.GetValueOrDefault(a.Run.Id)).LastOrDefault(r => r is not null);
        var summary = new TicketSummaryView(
            head.Project, head.TicketId, keys.For(head.Project, platform, head.TicketId), platform,
            newestFirst.Select(r => r.TicketTitle).FirstOrDefault(t => !string.IsNullOrEmpty(t)),
            state.Bucket, state.Kind, state.Since, ordered.Count, codeRows.Sum(r => r.Run.CostTotalUsd) + derivationCost,
            head.Id, head.Status, state.LastOutcome, state.QueuePlace, Question(head, state),
            headSpecs.Count(s => s == DoneSpec), headSpecs.Count,
            marks, pullRequests,
            record?.Cause, record?.TrackerState, record?.WebUrl,
            UnchangedSinceLastAttempt: unchanged, CheckedAt: check?.CheckedAt, Landing: state.Landing,
            Closed: TicketClosedView.Of(closed), Hold: hold);
        return standing?.Apply(summary) ?? summary;
    }

    /// <summary>2026-11-23-31dcg: a never-run row under its hold — a ticket that starts once others merge is a person's move,
    /// one whose requirements are still worked waits on nobody.</summary>
    public TicketSummaryView HeldNeverRun(TicketSummaryView row, TicketHoldInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(inputs);
        var waits = row.WaitsFor ?? [];
        var hold = holds.Of(new TicketHoldFacts(row.Project, row.TicketId)
        {
            Finished = row.Bucket is TicketBucket.Done or TicketBucket.Closed,
            SeriesId = inputs.SeriesOf(row.Project, row.TicketId),
            StartsOnceMerged = [.. waits.Where(w => w.StartsOnMerge).Select(w => w.Key)],
            // A closed requirement never finishes: it is named by no "starts when it finishes".
            WaitsFor = [.. waits.Where(w => !w.StartsOnMerge && !w.Closed).Select(w => w.Key)],
            TextEdited = inputs.EditedOpen(row.Project, row.TicketId),
        });
        var state = buckets.Held(new TicketBucketState(row.Bucket, row.Kind, row.Since, null, null, null, row.Landing), hold);
        return row with { Bucket = state.Bucket, Kind = state.Kind, Since = state.Since, Hold = hold };
    }

    // The stored facts the hold is read from: the newest attempt, what parks it, the restart, the series and the text edit.
    private static TicketHoldFacts Facts(Run head, int number, TicketBucketState state, TicketLiveState live,
        TicketStanding? standing, bool onInfrastructure, TicketHoldInputs inputs)
    {
        var question = live.Pending.GetValueOrDefault(head.Id);
        var interrupted = head.Status == LeftInPlaceEnd.Cancelled && OwedWorkCancel.IsOwed(head.CancelReason);
        // Done by its delivery alone — no merge, no landing by hand — leaves a short delivery to a person's decision.
        var delivered = state.Bucket == TicketBucket.Done && state.Landing != TicketLanding.Merged && standing?.LandedAt(head) is null;
        return new TicketHoldFacts(head.Project, head.TicketId)
        {
            Finished = state.Bucket == TicketBucket.Closed
                || (state.Bucket == TicketBucket.Done && !(delivered && head.Status == RunStatuses.Shortfall)),
            HeadStatus = head.Status, HeadRunId = head.Id, Attempt = number, EndKind = head.EndKind,
            EndedOnInfrastructure = onInfrastructure, EndDetail = EndDetailJson.Read(head.EndDetailJson),
            EndedAt = head.FinishedAt,
            Parked = head.Status == RunStatuses.WaitingForInput && state.QueuePlace is null,
            Asked = question is not null, Answerers = question?.Answerers ?? [],
            ParkedAt = question?.AskedAt ?? standing?.Handbacks.GetValueOrDefault(head.Id)?.At,
            Interrupted = interrupted, Restarting = interrupted && inputs.Restarts(head.FinishedAt),
            SeriesId = inputs.SeriesOf(head.Project, head.TicketId), SeriesActive = inputs.InActiveSeries(head.Project, head.TicketId),
            MergeStep = inputs.StepOf(head.Project, head.TicketId),
            TextEdited = inputs.EditedOpen(head.Project, head.TicketId),
        };
    }

    private static TicketPendingQuestion? Question(Run head, TicketBucketState state) =>
        state.Question is { } question ? new TicketPendingQuestion(head.Id, question) : null;
}
