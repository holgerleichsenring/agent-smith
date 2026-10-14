namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: one attempt as the list row marks it, oldest first.</summary>
/// <param name="EndedOnInfrastructure">Cancelled by a reaper, not by a person or the work.</param>
/// <param name="HandedBack">Parked with no pending question, or a spec of it was handed back.</param>
public sealed record TicketAttemptMark(
    string RunId, string Status, bool EndedOnInfrastructure, bool HandedBack);
