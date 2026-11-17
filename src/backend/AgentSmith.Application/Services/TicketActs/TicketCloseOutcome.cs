using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061a: what a 'close' run did — one outcome per pull request, the tracker's outcome (null unless
/// asked for) and the mark it stored (null when the request carried none).</summary>
public sealed record TicketCloseOutcome(
    IReadOnlyList<TicketPrCloseOutcome> PullRequests, TicketTrackerCloseOutcome? Tracker, TicketClosedMark? Mark);
