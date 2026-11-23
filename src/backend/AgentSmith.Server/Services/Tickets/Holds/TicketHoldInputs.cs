using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dcg: what a ticket's hold reads beyond its own runs, read once for every ticket of a read — the newest series
/// run each ticket is in and its place in that series' merge order, the tickets whose edited text the conversation has not
/// answered, and the earliest interruption the restart takes up (null when it is off). 2026-11-23-31dch: and which of those
/// series runs are still active, so a plan question goes to a conversation a series still reads.
/// </summary>
public sealed record TicketHoldInputs(
    IReadOnlyDictionary<(string Project, string TicketId), string> Series,
    IReadOnlyDictionary<(string Project, string TicketId), SeriesRunMergeStep> Steps,
    IReadOnlySet<(string Project, string TicketId)> Edited,
    DateTimeOffset? RestartSince,
    IReadOnlySet<(string Project, string TicketId)>? ActiveSeries = null)
{
    public static TicketHoldInputs None { get; } = new(
        new Dictionary<(string, string), string>(), new Dictionary<(string, string), SeriesRunMergeStep>(),
        new HashSet<(string, string)>(), null);

    public string? SeriesOf(string project, string ticketId) => Series.GetValueOrDefault((project, ticketId));

    public SeriesRunMergeStep? StepOf(string project, string ticketId) => Steps.GetValueOrDefault((project, ticketId));

    /// <summary>2026-11-23-31dch: the ticket's newest series run is still active — running, verifying or paused.</summary>
    public bool InActiveSeries(string project, string ticketId) => ActiveSeries?.Contains((project, ticketId)) == true;

    public bool EditedOpen(string project, string ticketId) => Edited.Contains((project, ticketId));

    /// <summary>Whether the restart takes up an interruption that ended at <paramref name="endedAt"/>.</summary>
    public bool Restarts(DateTimeOffset? endedAt) => Preconditions.InterruptedRestartWindow.Covers(endedAt, RestartSince);
}
