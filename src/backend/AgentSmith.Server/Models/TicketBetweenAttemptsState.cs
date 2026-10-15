namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59h: a stored between-attempts check as the ticket reads use it.</summary>
/// <param name="RunId">The last attempt the check was taken for.</param>
/// <param name="ClosedForWork">2026-10-15-114fc: a person closed the ticket on the tracker (TicketClosure).</param>
public sealed record TicketBetweenAttemptsState(
    string RunId, DateTimeOffset CheckedAt, bool FingerprintUnchanged, IReadOnlyList<TicketTrackerAct> Acts, bool ClosedForWork = false);
