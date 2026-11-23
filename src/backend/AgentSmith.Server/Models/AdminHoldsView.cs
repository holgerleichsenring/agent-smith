using AgentSmith.Application.Services.Holds;

namespace AgentSmith.Server.Models;

/// <summary>2026-11-23-31dci: every admin hold across the projects the admin sees, the longest held first — what System →
/// Needs attention lists.</summary>
public sealed record AdminHoldsView(IReadOnlyList<AdminHoldView> Holds);

/// <summary>2026-11-23-31dci: one ticket an admin's hold lies on — the ticket, since when, and the hold whose sentence,
/// and for an escalated wait whose value and configuration key, the page shows.</summary>
public sealed record AdminHoldView(
    string Project, string TicketId, string? TicketKey, string? Title, DateTimeOffset? Since, TicketHold Hold);
