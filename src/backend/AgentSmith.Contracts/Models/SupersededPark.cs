namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-10-16-86e6b: a parked code run of a ticket that a newer attempt of the same ticket has started past.
/// <paramref name="NewerRunId"/> is the newest such attempt.
/// </summary>
public sealed record SupersededPark(string RunId, string Project, string TicketId, string NewerRunId);
