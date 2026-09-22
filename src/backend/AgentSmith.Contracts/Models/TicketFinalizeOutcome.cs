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
    StatusNotExpressible,

    /// <summary>
    /// 2026-09-22-7c41b: no status was asked for, so none was attempted — the answer of a
    /// caller that posted a comment and nothing else. Not a provider outcome: a finalize
    /// always carries a status. It exists because <see cref="Moved"/> is the zero value, so a
    /// caller that moved nothing cannot answer with a default without claiming it moved the
    /// ticket — and a claimed move CLEARS a standing record nothing had earned.
    /// </summary>
    NoStatusRequested
}
