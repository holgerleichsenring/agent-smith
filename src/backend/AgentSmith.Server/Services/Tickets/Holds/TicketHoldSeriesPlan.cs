using AgentSmith.Application.Services.Series;

namespace AgentSmith.Server.Services.Tickets.Holds;

/// <summary>2026-11-23-849e: one series as its holds read it — its merge order and the keys of its never-run tickets that
/// start only once others merge (the rule a start reads, <c>UnmetTicketRequirements.ForStartManyAsync</c>).</summary>
public sealed record TicketHoldSeriesPlan(IReadOnlyList<SeriesRunMergeStep> MergeOrder, IReadOnlyList<string> StartsOnMerge)
{
    public static TicketHoldSeriesPlan None { get; } = new([], []);
}
