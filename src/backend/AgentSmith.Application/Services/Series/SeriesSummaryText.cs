using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207d: what a series check found, said once per check — how many criteria hold, what fails,
/// what was filed for it, the series' cost and duration so far, and whether anything waits on the operator.</summary>
public static class SeriesSummaryText
{
    public static string Of(SeriesBeat beat, SeriesVerdict verdict, string? followUpKey)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(verdict);
        var failed = verdict.Failed;
        var text = $"All {beat.Facts.Count} tickets landed. The series check on main ran the done criteria of its specs: "
            + $"{verdict.Results.Count - failed.Count} of {verdict.Results.Count} hold.";
        if (failed.Count > 0) text += $" Failing: {string.Join("; ", failed.Select(f => $"{f.Criterion} ({f.Evidence})"))}.";
        if (verdict.Unread.Count > 0) text += $" Not checked, the specs could not be read: {string.Join("; ", verdict.Unread)}.";
        if (followUpKey is not null)
            text += $" I filed {followUpKey} for it in this series" + (verdict.FollowUpStart is { } why ? $"; it did not start: {why}." : "; it is running.");
        var spent = beat.Facts.Sum(f => f.CostUsd) + beat.Record.MasterCostUsd;
        text += $" Total {SeriesTable.Money(spent)}, {SeriesTable.Duration(beat.Now - beat.Record.StartedAt)}.";
        return text + (WaitsOnOperator(beat, verdict, followUpKey) is { } waits ? $" Waiting on you: {waits}." : " Nothing waits on you.");
    }

    private static string? WaitsOnOperator(SeriesBeat beat, SeriesVerdict verdict, string? followUpKey)
    {
        if (beat.Record.Question is { Answer: null } question) return $"\"{question.Text}\"";
        if (verdict.Failed.Count > 0 && followUpKey is null) return "the failures above were followed up before and still fail";
        return verdict.FollowUpStart is not null ? "starting the follow-up" : null;
    }
}
