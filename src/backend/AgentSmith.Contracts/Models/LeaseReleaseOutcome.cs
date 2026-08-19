namespace AgentSmith.Contracts.Models;

/// <summary>
/// The result of releasing the single-run lease for a ticket. Released = the
/// caller held it and the row is gone; HeldByAnotherRun = a DIFFERENT run holds
/// the ticket now (the caller was reclaimed while it ran, or is finishing late) and
/// nothing was touched; NotFound = there was no lease to release, which is
/// harmless — every release path may run twice.
/// </summary>
public enum LeaseReleaseOutcome
{
    Released,
    HeldByAnotherRun,
    NotFound,
}
