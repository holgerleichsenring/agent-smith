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
    // p0269a: capacity-deferred — not claimed, waiting for room. Error carries the
    // human wait reason (which resource is full) for the visible signal.
    public static ClaimResult Queued(string reason) => new(ClaimOutcome.Queued, Error: reason);

    public bool IsSuccess => Outcome == ClaimOutcome.Claimed;
}
