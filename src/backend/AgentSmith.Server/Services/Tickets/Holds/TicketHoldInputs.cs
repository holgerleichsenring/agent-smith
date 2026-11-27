using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>
/// 2026-11-23-31dcg: what a ticket's hold reads beyond its own runs, read once for every ticket of a read — the newest series
/// run each ticket is in and its place in that series' merge order, the tickets whose edited text the conversation has not
/// answered, and the earliest interruption the restart takes up (null when it is off). 2026-11-23-31dch: and which of those
/// series runs are still active, so a plan question goes to a conversation a series still reads.
/// <para>2026-11-23-31dci: and since when each run waits for its model place and each ticket for sandbox capacity, the
/// minutes a wait on nobody may last before it is the admin's, and the time the read is made at.</para>
/// <para>2026-11-23-d60d: and every ticket's open pull requests (<see cref="TicketOpenPullRequestsReader"/>), added by the
/// read that holds the pull-request records.</para>
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

    /// <summary>2026-11-23-31dci: the open model waits' starts, by run id.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> ModelWaits { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>2026-11-23-31dci: the capacity queue's arrivals, by (project, ticket id).</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), DateTimeOffset> Enqueued { get; init; } =
        new Dictionary<(string, string), DateTimeOffset>();

    /// <summary>2026-11-23-31dci: <c>holds.escalate_after_minutes</c>; 0 (and no read) reports no wait.</summary>
    public int EscalateAfterMinutes { get; init; }

    /// <summary>2026-11-23-31dci: when the read was made; null where no read was.</summary>
    public DateTimeOffset? Now { get; init; }

    /// <summary>2026-11-23-d60d: the open pull requests by (project, ticket id); a ticket with none open is absent.</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), AgentSmith.Application.Services.Holds.TicketOpenPullRequests> OpenPullRequests
    { get; init; } = new Dictionary<(string, string), AgentSmith.Application.Services.Holds.TicketOpenPullRequests>();

    /// <summary>2026-11-23-849e: each series' merge order and the keys of its tickets that start only on merges, by series id.</summary>
    public IReadOnlyDictionary<string, TicketHoldSeriesPlan> Plans { get; init; } = new Dictionary<string, TicketHoldSeriesPlan>();

    /// <summary>2026-11-26-f511a: per ticket of an active series, the keys of its requirements that have not landed and when
    /// its master last took it up (<see cref="AgentSmith.Application.Services.Holds.TicketSeriesWait"/>).</summary>
    public IReadOnlyDictionary<(string Project, string TicketId), TicketHoldSeriesWait> SeriesWaits { get; init; } =
        new Dictionary<(string, string), TicketHoldSeriesWait>();

    /// <summary>2026-11-23-849e: the facts with the ticket's series — its newest series run, whether it is active, its step,
    /// that series' merge order and what waits on its merges. 2026-11-26-f511a: and what of it it waits on.</summary>
    public AgentSmith.Application.Services.Holds.TicketHoldFacts WithSeries(AgentSmith.Application.Services.Holds.TicketHoldFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var series = SeriesOf(facts.Project, facts.TicketId);
        var plan = PlanOf(series);
        var wait = SeriesWaits.GetValueOrDefault((facts.Project, facts.TicketId)) ?? TicketHoldSeriesWait.None;
        return facts with
        {
            SeriesId = series, SeriesActive = InActiveSeries(facts.Project, facts.TicketId), MergeStep = StepOf(facts.Project, facts.TicketId),
            SeriesMergeOrder = plan.MergeOrder, SeriesStartsOnMerge = plan.StartsOnMerge, SeriesMasterMerges = plan.MasterMerges, SeriesMasterLeads = plan.MasterLeads, // e451
            SeriesUnlanded = wait.Unlanded, SeriesTakenUpAt = wait.TakenUpAt, SeriesStartRefusedAt = wait.StartRefusedAt, // ecf2e
        };
    }

    public TicketHoldSeriesPlan PlanOf(string? seriesId) =>
        seriesId is not null && Plans.TryGetValue(seriesId, out var plan) ? plan : TicketHoldSeriesPlan.None;

    public AgentSmith.Application.Services.Holds.TicketOpenPullRequests? OpenOf(string project, string ticketId) =>
        OpenPullRequests.GetValueOrDefault((project, ticketId));

    public DateTimeOffset? ModelWaitOf(string runId) => ModelWaits.TryGetValue(runId, out var since) ? since : null;

    public DateTimeOffset? EnqueuedOf(string project, string ticketId) =>
        Enqueued.TryGetValue((project, ticketId), out var at) ? at : null;

    public string? SeriesOf(string project, string ticketId) => Series.GetValueOrDefault((project, ticketId));

    public SeriesRunMergeStep? StepOf(string project, string ticketId) => Steps.GetValueOrDefault((project, ticketId));

    /// <summary>2026-11-23-31dch: the ticket's newest series run is still active — running, verifying or paused.</summary>
    public bool InActiveSeries(string project, string ticketId) => ActiveSeries?.Contains((project, ticketId)) == true;

    public bool EditedOpen(string project, string ticketId) => Edited.Contains((project, ticketId));

    /// <summary>Whether the restart takes up an interruption that ended at <paramref name="endedAt"/>.</summary>
    public bool Restarts(DateTimeOffset? endedAt) => Preconditions.InterruptedRestartWindow.Covers(endedAt, RestartSince);
}
