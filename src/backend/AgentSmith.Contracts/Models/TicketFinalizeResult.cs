namespace AgentSmith.Contracts.Models;

/// <summary>
/// The answer <c>ITicketProvider.FinalizeAsync</c> owes its caller: did the ticket's status
/// actually move, and when it did not, which of the three ways it failed to. A STRUCT, so a
/// loose test double hands back <see cref="TicketFinalizeOutcome.Moved"/> rather than null.
/// </summary>
public readonly record struct TicketFinalizeResult(
    TicketFinalizeOutcome Outcome,
    string? RequestedStatus = null,
    string? Detail = null)
{
    public static TicketFinalizeResult Moved() => new(TicketFinalizeOutcome.Moved);

    public static TicketFinalizeResult Rejected(string requestedStatus, string detail) =>
        new(TicketFinalizeOutcome.TrackerRejectedTheStatus, requestedStatus, detail);

    public static TicketFinalizeResult NoTransition(string requestedStatus) =>
        new(TicketFinalizeOutcome.NoTransitionToTheStatus, requestedStatus);

    public static TicketFinalizeResult NotExpressible(string requestedStatus) =>
        new(TicketFinalizeOutcome.StatusNotExpressible, requestedStatus);

    /// <summary>True when the ticket carries the requested status now.</summary>
    public bool StatusMoved => Outcome == TicketFinalizeOutcome.Moved;
}
