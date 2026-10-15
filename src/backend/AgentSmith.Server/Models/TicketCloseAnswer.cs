using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-15-114fa: the stored mark, one outcome per pull request the ticket's runs named (empty unless asked
/// for) and the tracker's outcome (null unless asked for).
/// </summary>
public sealed record TicketCloseAnswer(
    TicketClosedMark Mark, IReadOnlyList<TicketPrCloseOutcome> PullRequests, TicketTrackerCloseOutcome? Tracker);
