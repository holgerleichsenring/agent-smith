using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207b: the series' first message — what was filed, what started, what waits and on what rule,
/// how it merges (2026-11-25-9992i: by the series' review). 2026-11-25-e94eb: every series conversation is told it hears only what needs a person; the series
/// page shows every ticket.</summary>
public static class SeriesOpening
{
    public static string Text(SeriesBeat beat, ResolvedProject project)
    {
        ArgumentNullException.ThrowIfNull(beat);
        // 2026-11-25-9992a: from the one state — an attempt began, or the hold waits on prerequisites or their merges
        var started = beat.Facts.Where(f => f.LatestStatus is not null).Select(f => f.Ticket.Key).ToList();
        var held = beat.Facts.Count(f => !f.Landed && f.Hold?.Case is Holds.TicketHoldCases.Prerequisite or Holds.TicketHoldCases.StartsOnMerge);
        var text = $"Filed {beat.Facts.Count} tickets. "
            + (started.Count > 0 ? $"{string.Join(", ", started)} started; " : "None has started yet; ")
            + (held > 0 ? $"{held} wait on their prerequisites and {WaitRule(project, beat.Record.IntegrationBranch)}. " : string.Empty);
        var branch = beat.Record.IntegrationBranch; // 2026-11-24-f9c5c: a series with a branch merges its tickets there
        var merge = beat.Record.EffectiveReview switch // 2026-11-25-9992i
        {
            ReviewMode.None => "I merge each one when its build and tests are green",
            ReviewMode.Each => "I merge each one once you approved its pull request",
            _ => $"I merge each one into {branch} when its build and tests are green; the series reaches the default branch as "
                + "one pull request per repository at the end, for you to review",
        };
        return text + $"{merge}. I write here only when you are needed; the series page on the dashboard shows every ticket, "
            + "and asking me to hold pauses the series.";
    }

    private static string WaitRule(ResolvedProject project, string? seriesBranch) =>
        seriesBranch is not null || project.RequiresStart == RequiresStart.OnMerge
            ? "start once the ticket they need is merged" : "start as soon as the ticket they need has a green push";
}
