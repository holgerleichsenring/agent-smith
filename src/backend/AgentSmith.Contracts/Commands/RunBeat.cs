namespace AgentSmith.Contracts.Commands;

/// <summary>
/// p0344b: the run-story beat every pipeline command belongs to. The dashboard's
/// storybar renders five operator-level beats (ticket → plan → building → verify
/// → outcome); the SERVER derives each beat's state from the run's typed command
/// progress. The vocabulary is deliberately tiny — a beat is a narrative act,
/// not a step list.
/// </summary>
public enum RunBeat
{
    /// <summary>Getting the work: fetch ticket, checkout, credentials, context loads.</summary>
    Ticket,
    /// <summary>Agreeing the WHAT: expectation negotiation, plan, approval, clarification gates.</summary>
    Plan,
    /// <summary>Doing the work: analysis, master/skill rounds, scans, generation.</summary>
    Building,
    /// <summary>Checking the work: review/verify phases, findings compilation, convergence.</summary>
    Verify,
    /// <summary>Shipping the result: run record, commit + PR, delivery, cross-links.</summary>
    Outcome,
}
