namespace AgentSmith.Contracts.Models;

/// <summary>
/// Result of a TicketClaimService.ClaimAsync call.
/// </summary>
public sealed record ClaimResult(
    ClaimOutcome Outcome,
    ClaimRejectionReason? Rejection = null,
    string? Error = null)
{
    public static ClaimResult Claimed() => new(ClaimOutcome.Claimed);
    public static ClaimResult AlreadyClaimed() => new(ClaimOutcome.AlreadyClaimed);
    public static ClaimResult Rejected(ClaimRejectionReason reason)
        => new(ClaimOutcome.Rejected, Rejection: reason);
    public static ClaimResult Failed(string error) => new(ClaimOutcome.Failed, Error: error);

    public bool IsSuccess => Outcome == ClaimOutcome.Claimed;
}
