using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dcg: what a ticket's hold reads beyond its own runs, read once for every ticket of a read — the newest series
/// run each ticket is in and its place in that series' merge order, the tickets whose edited text the conversation has not
/// answered, and the earliest interruption the restart takes up (null when it is off).
/// <para>2026-11-23-31dci: and since when each run waits for its model place and each ticket for sandbox capacity, the
/// minutes a wait on nobody may last before it is the admin's, and the time the read is made at.</para>
/// </summary>
public sealed record TicketHoldInputs(
    IReadOnlyDictionary<(string Project, string TicketId), string> Series,
    IReadOnlyDictionary<(string Project, string TicketId), SeriesRunMergeStep> Steps,
    IReadOnlySet<(string Project, string TicketId)> Edited,
    DateTimeOffset? RestartSince)
{
    public static TicketHoldInputs None { get; } = new(
        new Dictionary<(string, string), string>(), new Dictionary<(string, string), SeriesRunMergeStep>(),
        new HashSet<(string, string)>(), null);

    /// <summary>2026-11-23-31dci: the open model waits' starts, by run id.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> ModelWaits { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>2026-11-23-31dci: the capacity queue's arrivals, by (project, ticket id).</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), DateTimeOffset> Enqueued { get; init; } =
        new Dictionary<(string, string), DateTimeOffset>();

    /// <summary>2026-11-23-31dci: <c>holds.escalate_after_minutes</c>; 0 (and no read) reports no wait.</summary>
    public int EscalateAfterMinutes { get; init; }

    /// <summary>2026-11-23-31dci: when the read was made; null where no read was.</summary>
    public DateTimeOffset? Now { get; init; }

    public DateTimeOffset? ModelWaitOf(string runId) => ModelWaits.TryGetValue(runId, out var since) ? since : null;

    public DateTimeOffset? EnqueuedOf(string project, string ticketId) =>
        Enqueued.TryGetValue((project, ticketId), out var at) ? at : null;

    public string? SeriesOf(string project, string ticketId) => Series.GetValueOrDefault((project, ticketId));

    public SeriesRunMergeStep? StepOf(string project, string ticketId) => Steps.GetValueOrDefault((project, ticketId));

    public bool EditedOpen(string project, string ticketId) => Edited.Contains((project, ticketId));

    /// <summary>Whether the restart takes up an interruption that ended at <paramref name="endedAt"/>.</summary>
    public bool Restarts(DateTimeOffset? endedAt) => Preconditions.InterruptedRestartWindow.Covers(endedAt, RestartSince);
}
