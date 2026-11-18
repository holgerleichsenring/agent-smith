using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Services.Series;

/// <summary>2026-11-17-0207b: the series' first message — what was filed, what started, what waits and on what rule,
/// how it merges and how often it reports.</summary>
public static class SeriesOpening
{
    public static string Text(SeriesBeat beat, ResolvedProject project, DialogueGlobalConfig dialogue)
    {
        ArgumentNullException.ThrowIfNull(beat);
        var rows = beat.Rows;
        var started = rows.Where(r => r.Kind is SeriesTicketState.Running or SeriesTicketState.Green).Select(r => r.Ticket).ToList();
        var held = rows.Count(r => r.Kind == SeriesTicketState.Held);
        var text = $"Filed {rows.Count} tickets. "
            + (started.Count > 0 ? $"{string.Join(", ", started)} started; " : "None has started yet; ")
            + (held > 0 ? $"{held} wait on their prerequisites and {WaitRule(project)}. " : string.Empty);
        var merge = project.Merge.When == MergeWhen.OnGateGreen
            ? "I merge each one when its gate is green" : "Their pull requests stay open for you to merge";
        return text + $"{merge} and report here on every change and every {dialogue.SeriesStatusMinutes} minutes.";
    }

    private static string WaitRule(ResolvedProject project) => project.RequiresStart == RequiresStart.OnMerge
        ? "start once the ticket they need is merged" : "start as soon as the ticket they need has a green push";
}
