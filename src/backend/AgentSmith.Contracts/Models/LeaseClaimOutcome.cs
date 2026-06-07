namespace AgentSmith.Contracts.Models;

/// <summary>
/// The result of attempting to take the single-run lease for a ticket. Claimed =
/// the INSERT won; AlreadyClaimed = the UNIQUE(Project,TicketId) index rejected a
/// duplicate (a run already holds the ticket); Error = an unexpected write fault.
/// </summary>
public enum LeaseClaimOutcome
{
    Claimed,
    AlreadyClaimed,
    Error,
}
