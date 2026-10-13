namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: one page of the ticket list, the cursor to the next, and the counts.</summary>
/// <param name="NextCursor">The newest-attempt run id the next page starts below; null on the last page.</param>
public sealed record TicketListView(
    IReadOnlyList<TicketSummaryView> Tickets, string? NextCursor, TicketCountsView Counts);
