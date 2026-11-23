namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dcf: one requires edge between two tickets of a series, as the series graph draws it.
/// 2026-11-23-f562: without a kind — the Overview colours tickets by their holds, not edges by the branch stack.</summary>
/// <param name="TicketId">The dependent.</param>
/// <param name="RequiredTicketId">The prerequisite it requires.</param>
public sealed record SeriesRunEdgeView(string TicketId, string RequiredTicketId);
