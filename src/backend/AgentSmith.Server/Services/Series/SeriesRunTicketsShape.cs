using AgentSmith.Application.Services.Series;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Series;

/// <summary>2026-11-23-31dcb: a series' tickets in depth order, its progress and its merge order.</summary>
public sealed record SeriesRunTicketsShape(
    IReadOnlyList<SeriesRunTicketView> Tickets, SeriesRunCounts Counts, IReadOnlyList<SeriesRunMergeStep> MergeOrder);
