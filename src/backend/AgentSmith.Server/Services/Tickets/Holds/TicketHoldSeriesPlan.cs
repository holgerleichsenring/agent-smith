using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>2026-11-23-849e: one series as its holds read it — its merge order and the keys of its never-run tickets that
/// start only once others merge (the rule a start reads, <c>UnmetTicketRequirements.ForStartManyAsync</c>).</summary>
public sealed record TicketHoldSeriesPlan(IReadOnlyList<SeriesRunMergeStep> MergeOrder, IReadOnlyList<string> StartsOnMerge)
{
    /// <summary>2026-11-27-e451: the series' master merges its tickets' pull requests itself (<see cref="SeriesLeads.Merges"/>).</summary>
    public bool MasterMerges { get; init; }

    /// <summary>2026-11-27-e451: the series' master leads its tickets' pull requests (<see cref="SeriesLeads.PullRequests"/>).</summary>
    public bool MasterLeads { get; init; }

    public static TicketHoldSeriesPlan None { get; } = new([], []);
}
