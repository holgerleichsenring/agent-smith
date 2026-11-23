using AgentSmith.Application.Services.Series;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: a series' tickets in depth order, its progress and its merge order.</summary>
public sealed record SeriesRunTicketsShape(
    IReadOnlyList<SeriesRunTicketView> Tickets, SeriesRunCounts Counts, IReadOnlyList<SeriesRunMergeStep> MergeOrder)
{
    /// <summary>2026-11-23-849e: the key of the ticket a person merges now — the ready step, and once holds were read only
    /// while its hold is that merge (CI running or red is not ready); null when none is.</summary>
    public string? NextMerge { get; init; }
}
