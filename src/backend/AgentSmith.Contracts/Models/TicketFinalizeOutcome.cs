namespace AgentSmith.Contracts.Models;

/// <summary>
/// What a finalize did to the ticket's status. <see cref="Moved"/> is deliberately the
/// zero value: it is what the old void signature meant, so a stub that does not care
/// still reports the write happened.
/// </summary>
public enum TicketFinalizeOutcome
{
    /// <summary>The ticket now carries the requested status.</summary>
    Moved = 0,

    /// <summary>The tracker refused the write — the value exists in the request, not in the tracker.</summary>
    TrackerRejectedTheStatus,

    /// <summary>The tracker offers no transition that lands on the requested status.</summary>
    NoTransitionToTheStatus,

    /// <summary>The provider cannot express the requested status at all; nothing was sent.</summary>
    StatusNotExpressible
}
