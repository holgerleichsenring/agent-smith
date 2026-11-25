using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207b: the series' first message — what was filed, what started, what waits and on what rule,
/// how it merges. 2026-11-25-e94eb: every series conversation is told it hears only what needs a person; the series
/// page shows every ticket.</summary>
public static class SeriesOpening
{
    public static string Text(SeriesBeat beat, ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var rows = beat.Rows;
        var started = rows.Where(r => r.Kind is SeriesTicketState.Running or SeriesTicketState.ModelWait or SeriesTicketState.Green).Select(r => r.Ticket).ToList();
        var held = rows.Count(r => r.Kind == SeriesTicketState.Held);
        var text = $"Filed {rows.Count} tickets. "
            + (started.Count > 0 ? $"{string.Join(", ", started)} started; " : "None has started yet; ")
            + (held > 0 ? $"{held} wait on their prerequisites and {WaitRule(project, beat.Record.IntegrationBranch)}. " : string.Empty);
        var branch = beat.Record.IntegrationBranch; // 2026-11-24-f9c5c: a series with a branch merges its tickets there
        var into = branch is null ? string.Empty : $" into {branch}";
        var merge = project.Merge.WhenInto(branch) switch
        {
            MergeWhen.OnGateGreen => $"I merge each one{into} when its gate is green",
            MergeWhen.OnCiGreen => $"I merge each one{into} when its CI is green; a red CI goes back to its run with the failing check",
            _ => $"Their pull requests{into} stay open for you to merge",
        };
        if (branch is not null) merge += "; the series reaches the default branch as one pull request per repository at the end";
        return text + $"{merge}. I write here only when you are needed; the series page on the dashboard shows every ticket, "
            + "and asking me to hold pauses the series.";
    }

    private static string WaitRule(ResolvedProject project, string? seriesBranch) =>
        seriesBranch is not null || project.RequiresStart == RequiresStart.OnMerge
            ? "start once the ticket they need is merged" : "start as soon as the ticket they need has a green push";
}
