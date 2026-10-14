using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>
/// The acceptance a run-record spec takes its verdicts from: the spec's own, or — when its row carries
/// none — the run-end acceptance of the run that executed it, shared by every spec of that run.
/// </summary>
/// <param name="OfTheRun">True when this is the run-end acceptance rather than the spec's own.</param>
/// <param name="SingleSpecRun">True when the run recorded exactly one spec.</param>
public sealed record TicketRunRecordAcceptance(string RunId, AcceptanceView View, bool OfTheRun, bool SingleSpecRun)
{
    /// <summary>True when everything in the acceptance belongs to the spec that reads it.</summary>
    public bool IsWhollyTheSpecs => !OfTheRun || SingleSpecRun;
}
