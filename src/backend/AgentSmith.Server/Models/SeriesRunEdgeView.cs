namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcf: one requires edge between two tickets of a series, as the Order and branches graph draws it.</summary>
/// <param name="TicketId">The dependent.</param>
/// <param name="RequiredTicketId">The prerequisite it requires.</param>
/// <param name="Kind">One of <see cref="SeriesRunEdgeKinds"/>.</param>
public sealed record SeriesRunEdgeView(string TicketId, string RequiredTicketId, string Kind);
