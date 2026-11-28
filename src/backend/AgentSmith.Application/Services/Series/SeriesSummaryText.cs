using AgentSmith.Contracts.Series;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207d: what a series check found, said once per check. 2026-11-28-d1ffc: in a few lines — the outcome with
/// its counts, each criterion not checkable here (d1ffa), what was filed, what waits on the operator and the final pull requests —
/// and the rest (the failing criteria with their evidence, the specs not read, cost and duration) in a ````details fence the
/// series chat draws collapsed behind "show all".</summary>
public static class SeriesSummaryText
{
    public const string DetailsOpen = "````details";
    public const string DetailsClose = "````";

    public static string Of(SeriesBeat beat, SeriesVerdict verdict, string? followUpKey)
    {
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(verdict);
        var (failed, notCheckable) = (verdict.Failed, verdict.NotCheckable);
        // Blocks apart by a blank line, so a list never swallows the sentence after it.
        var blocks = new List<string>
        {
            $"All {beat.Facts.Count} tickets landed. Series check on {SeriesBranchWords.Checked(beat.Record)}: "
                + $"{verdict.Results.Count(r => r.Passed)} of {verdict.Results.Count} hold"
                + (failed.Count > 0 ? $", {failed.Count} fail" : string.Empty)
                + (notCheckable.Count > 0 ? $", {notCheckable.Count} not checkable here" : string.Empty)
                + (verdict.Unread.Count > 0 ? $"; {verdict.Unread.Count} ticket(s) not checked, their specs could not be read" : string.Empty) + ".",
        };
        if (notCheckable.Count > 0)
            blocks.Add(string.Join("\n", notCheckable.Select(n => $"- {OneLine(n.Criterion)}: not checkable here: {OneLine(n.NotCheckable!)}")));
        var finals = beat.Record.Deliveries.Where(d => d.Url.Length > 0).ToList();
        if (finals.Count > 0) blocks.Add(string.Join("\n", finals.Select(d => $"- Final pull request of {d.Repo}: [#{d.Number}]({d.Url})")));
        if (followUpKey is not null)
            blocks.Add($"I filed {followUpKey} for it in this series" + (verdict.FollowUpStart is { } why ? $"; it did not start: {why}." : "; it is running."));
        blocks.Add(WaitsOnOperator(beat, verdict, followUpKey) is { } waits ? $"Waiting on you: {waits}." : "Nothing waits on you.");
        blocks.Add(Details(beat, verdict));
        return string.Join("\n\n", blocks);
    }

    /// <summary>The part of a summary before its details fence — what a reader sees without opening it.</summary>
    public static string Shown(string summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var at = summary.IndexOf(DetailsOpen, StringComparison.Ordinal);
        return (at < 0 ? summary : summary[..at]).TrimEnd();
    }

    private static string Details(SeriesBeat beat, SeriesVerdict verdict)
    {
        var lines = new List<string> { DetailsOpen };
        if (verdict.Failed.Count > 0) lines.Add("Failing:");
        lines.AddRange(verdict.Failed.Select(f => $"- {OneLine(f.Criterion)} — {OneLine(f.Evidence)}"));
        if (verdict.Unread.Count > 0) lines.Add($"Not checked, the specs could not be read: {string.Join("; ", verdict.Unread)}.");
        var spent = beat.Facts.Sum(f => f.CostUsd) + beat.Record.MasterCostUsd;
        lines.Add($"Total {SeriesTable.Money(spent)}, {SeriesTable.Duration(beat.Now - beat.Record.StartedAt)}.");
        lines.Add(DetailsClose);
        return string.Join("\n", lines);
    }

    // A recorded text on one line, so it stays one list item and never closes the details fence.
    private static string OneLine(string text) => text.ReplaceLineEndings(" ").Trim();

    // 2026-11-28-d1ffc: a series with a branch that files nothing new delivers next, and its final pull request waits for review.
    private static string? WaitsOnOperator(SeriesBeat beat, SeriesVerdict verdict, string? followUpKey)
    {
        if (beat.Record.Question is { Answer: null } question) return $"\"{question.Text}\"";
        if (verdict.Failed.Count > 0 && followUpKey is null)
            return beat.Record.IntegrationBranch is null ? "the failures were followed up before and still fail"
                : "the failures were followed up before and still fail; the final pull request waits for your review";
        if (verdict.FollowUpStart is not null) return "starting the follow-up";
        return followUpKey is null && beat.Record.IntegrationBranch is not null ? "your review of the final pull request" : null;
    }
}
