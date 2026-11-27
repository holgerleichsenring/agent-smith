using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-25-9992g: what a read of a series ticket's pull request changed that its master judges. 2026-11-25-96aec: only
/// what needs a decision wakes it — a CI verdict on the head (green, red, not started), a CI without one merge.ci_pending_seconds
/// after it was first seen running (once per head), the pull request merged, closed or reopened, a conflict appearing, a
/// person's new approval or change request, 2026-11-26-5201f: the host starting to need a person's approval. The first read of a pull request its run opened, a conflict that cleared and a
/// target that changed are recorded quietly (<see cref="SeriesWake.Quiet"/>). A head that moved, a running CI and a re-read
/// that saw the same record nothing.
/// <para>2026-11-27-e451: a head that reported no CI for merge.ci_settle_seconds has none — the verdict none, once per pull
/// request and head (<see cref="NoCi"/>); before that, 'nothing yet' is not 'nothing configured' and records nothing.</para>
/// </summary>
public static class SeriesPrChange
{
    public static IReadOnlyList<SeriesWake> Of(
        PrReviewRecordView? stored, PrReviewRecord read, IReadOnlyList<PrReviewEntry> entries, int pendingSeconds, int settleSeconds)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(entries);
        if (read.State is PrReviewStates.Unread or PrReviewStates.Unsupported) return [];
        var wakes = new List<SeriesWake>();
        if (Ci(stored, read) is { } ci) wakes.Add(new SeriesWake(SeriesWakeKinds.Ci, ci) { Verdict = read.CiState });
        if (NoCi(stored, read, settleSeconds) is { } none) wakes.Add(none);
        if (SeriesPrCiDeadline.Of(stored, read, pendingSeconds) is { } late) wakes.Add(late);
        var waking = Waking(stored, read).ToList();
        if (waking.Count > 0) wakes.Add(Change(read, waking, quiet: false) with { Verdict = StateChanged(stored, read) ? read.State : null });
        var reviewed = SeriesPrDecisions.New(stored, entries).ToList(); // a person's, never the master's own act
        if (reviewed.Count > 0) wakes.Add(new SeriesWake(SeriesWakeKinds.Review, $"pull request {read.Url} {string.Join("; ", reviewed)}"));
        var quiet = Quiet(stored, read).ToList();
        if (quiet.Count > 0) wakes.Add(Change(read, quiet, quiet: true));
        return wakes;
    }

    /// <summary>2026-11-27-e451: an open pull request whose head reports no CI and has stood for merge.ci_settle_seconds — it has
    /// none, and merges as a green one does; raised once per pull request and head.</summary>
    public static SeriesWake? NoCi(PrReviewRecordView? stored, PrReviewRecord read, int settleSeconds)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (read.State != PrReviewStates.Open || read.CiState != CiStates.None || read.CiHeadSha is not { } head) return null;
        if (!PrRecordCiRead.Settled(read, stored, settleSeconds)) return null;
        return new SeriesWake(SeriesWakeKinds.Ci, $"the CI of pull request {read.Url} on {SeriesPrCiDeadline.Short(head)} is none: nothing "
            + $"reported for merge.ci_settle_seconds = {settleSeconds}, so it has no CI and merges as a green one does")
        { Verdict = CiStates.None, Once = $"ci-none:{read.PrKey}:{head}" };
    }

    // A record that never read the host (its first read failed) knows no state yet.
    private static bool FirstRead(PrReviewRecordView stored) => stored.State is PrReviewStates.Unread or PrReviewStates.Unsupported;

    private static SeriesWake Change(PrReviewRecord read, IReadOnlyList<string> changes, bool quiet) =>
        new(SeriesWakeKinds.PullRequest, $"pull request {read.Url} {string.Join("; ", changes)}") { Quiet = quiet };

    private static string? Ci(PrReviewRecordView? stored, PrReviewRecord read)
    {
        if (read.CiState is not (CiStates.Green or CiStates.Red or CiStates.NotStarted) || read.CiHeadSha is not { } head) return null;
        if (stored?.Ci is { } was && was.State == read.CiState && was.IsOn(head)) return null;
        var state = read.CiState == CiStates.NotStarted ? "has not started" : $"is {read.CiState}";
        return $"the CI of pull request {read.Url} on {SeriesPrCiDeadline.Short(head)} {state}";
    }

    // A state change is a decision unless it is the first read of the pull request its run opened.
    private static IEnumerable<string> Waking(PrReviewRecordView? stored, PrReviewRecord read)
    {
        if (StateChanged(stored, read)) yield return $"is {read.State}";
        if (read.HasConflicts == true && stored?.HasConflicts != true) yield return "conflicts with its base";
        // 2026-11-26-5201f: a person's step the master tells the person about, once per turn to required
        if (read.ApprovalRequired == true && stored?.ApprovalRequired != true) yield return "waits for a person's approval on the host";
        // 2026-11-25-96aea: an auto-complete the host dropped at the conflict is armed again only by the master's merge
        if (Cleared(stored, read) && stored!.AutoCompleteLostAt is not null) yield return "no longer conflicts with its base; its auto-complete is gone";
    }

    private static bool StateChanged(PrReviewRecordView? stored, PrReviewRecord read) =>
        stored is null || FirstRead(stored) ? read.State != PrReviewStates.Open : stored.State != read.State;

    private static bool Cleared(PrReviewRecordView? stored, PrReviewRecord read) => read.HasConflicts == false && stored?.HasConflicts == true;

    private static IEnumerable<string> Quiet(PrReviewRecordView? stored, PrReviewRecord read)
    {
        if ((stored is null || FirstRead(stored)) && read.State == PrReviewStates.Open) yield return "is open";
        if (Cleared(stored, read) && stored!.AutoCompleteLostAt is null) yield return "no longer conflicts with its base";
        if (stored?.TargetBranch is { } before && read.TargetBranch is { } now && before != now) yield return $"now targets {now}";
    }
}
