namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59h: a stored between-attempts check as the ticket reads use it.</summary>
/// <param name="RunId">The last attempt the check was taken for.</param>
public sealed record TicketBetweenAttemptsState(
    string RunId, DateTimeOffset CheckedAt, bool FingerprintUnchanged, IReadOnlyList<TicketTrackerAct> Acts);
