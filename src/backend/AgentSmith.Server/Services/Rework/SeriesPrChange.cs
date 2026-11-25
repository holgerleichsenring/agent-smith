using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-25-9992g: what a read of a series ticket's pull request changed that its master judges — the CI on the head
/// reaching a verdict (green, red, not started), and the pull request's state, conflict and target. A read that only
/// advanced its check time, its threads or a running CI changes nothing here.
/// </summary>
public static class SeriesPrChange
{
    public static IReadOnlyList<SeriesWake> Of(PrReviewRecordView? stored, PrReviewRecord read)
    {
        ArgumentNullException.ThrowIfNull(read);
        if (read.State is PrReviewStates.Unread or PrReviewStates.Unsupported) return [];
        var wakes = new List<SeriesWake>();
        if (Ci(stored, read) is { } ci) wakes.Add(new SeriesWake(SeriesWakeKinds.Ci, ci));
        var changes = Changes(stored, read).ToList();
        if (changes.Count > 0) wakes.Add(new SeriesWake(SeriesWakeKinds.PullRequest, $"pull request {read.Url} {string.Join("; ", changes)}"));
        return wakes;
    }

    private static string? Ci(PrReviewRecordView? stored, PrReviewRecord read)
    {
        if (read.CiState is not (CiStates.Green or CiStates.Red or CiStates.NotStarted) || read.CiHeadSha is not { } head) return null;
        if (stored?.Ci is { } was && was.State == read.CiState && was.IsOn(head)) return null;
        var state = read.CiState == CiStates.NotStarted ? "has not started" : $"is {read.CiState}";
        return $"the CI of pull request {read.Url} on {head[..Math.Min(7, head.Length)]} {state}";
    }

    private static IEnumerable<string> Changes(PrReviewRecordView? stored, PrReviewRecord read)
    {
        if (stored is null || stored.State != read.State) yield return $"is {read.State}";
        if (read.HasConflicts == true && stored?.HasConflicts != true) yield return "conflicts with its base";
        if (read.HasConflicts == false && stored?.HasConflicts == true) yield return "no longer conflicts with its base";
        if (stored?.TargetBranch is { } before && read.TargetBranch is { } now && before != now) yield return $"now targets {now}";
    }
}
