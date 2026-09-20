namespace AgentSmith.Contracts.Models;

/// <summary>
/// One ticket whose last run could not move it out of its trigger status, recorded against
/// the project and the ticket. It is the reason a claim is refused: re-claiming a ticket the
/// tracker will refuse again burns a run per poll cycle, forever.
/// </summary>
public sealed record UnmovedTicketFact(
    string Project,
    string TicketId,
    string Tracker,
    string ConfiguredStatus,
    TicketFinalizeOutcome Outcome);
