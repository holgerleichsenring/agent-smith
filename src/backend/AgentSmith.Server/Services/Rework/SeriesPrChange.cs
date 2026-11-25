using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-25-9992g: what a read of a series ticket's pull request changed that its master judges. 2026-11-25-96aec: only
/// what needs a decision wakes it — a CI verdict on the head (green, red, not started), a CI without one merge.ci_pending_seconds
/// after it was first seen running (once per head), the pull request merged, closed or reopened, a conflict appearing, a
/// person's new approval or change request. The first read of a pull request its run opened, a conflict that cleared and a
/// target that changed are recorded quietly (<see cref="SeriesWake.Quiet"/>). A head that moved, a running CI and a re-read
/// that saw the same record nothing.
/// </summary>
public static class SeriesPrChange
{
    public static IReadOnlyList<SeriesWake> Of(
        PrReviewRecordView? stored, PrReviewRecord read, IReadOnlyList<PrReviewEntry> entries, int pendingSeconds)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(entries);
        if (read.State is PrReviewStates.Unread or PrReviewStates.Unsupported) return [];
        var wakes = new List<SeriesWake>();
        if (Ci(stored, read) is { } ci) wakes.Add(new SeriesWake(SeriesWakeKinds.Ci, ci));
        if (SeriesPrCiDeadline.Of(stored, read, pendingSeconds) is { } late) wakes.Add(late);
        var waking = Waking(stored, read).ToList();
        if (waking.Count > 0) wakes.Add(Change(read, waking, quiet: false));
        var reviewed = SeriesPrDecisions.New(stored, entries).ToList(); // a person's, never the master's own act
        if (reviewed.Count > 0) wakes.Add(new SeriesWake(SeriesWakeKinds.Review, $"pull request {read.Url} {string.Join("; ", reviewed)}"));
        var quiet = Quiet(stored, read).ToList();
        if (quiet.Count > 0) wakes.Add(Change(read, quiet, quiet: true));
        return wakes;
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
        if (stored is null || FirstRead(stored) ? read.State != PrReviewStates.Open : stored.State != read.State)
            yield return $"is {read.State}";
        if (read.HasConflicts == true && stored?.HasConflicts != true) yield return "conflicts with its base";
    }

    private static IEnumerable<string> Quiet(PrReviewRecordView? stored, PrReviewRecord read)
    {
        if ((stored is null || FirstRead(stored)) && read.State == PrReviewStates.Open) yield return "is open";
        if (read.HasConflicts == false && stored?.HasConflicts == true) yield return "no longer conflicts with its base";
        if (stored?.TargetBranch is { } before && read.TargetBranch is { } now && before != now) yield return $"now targets {now}";
    }
}
