namespace AgentSmith.Contracts.Models;

/// <summary>
/// Outcome of a TicketClaimService.ClaimAsync call.
/// </summary>
public enum ClaimOutcome
{
    Claimed,
    AlreadyClaimed,
    Rejected,
    Failed
}
