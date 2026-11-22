using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-9a59a: one attempt as the list row marks it, oldest first.</summary>
/// <param name="EndedOnInfrastructure">Cancelled by a reaper, not by a person or the work.</param>
/// <param name="HandedBack">Parked with no pending question, or a spec of it was handed back.</param>
/// <param name="Cause">2026-11-22-50d7a: why the attempt began, from its record; null where records are not read.</param>
/// <param name="EndKind">2026-11-22-50d7a: why the run ended (<see cref="RunEndKinds"/>); null when no producer classified it.</param>
public sealed record TicketAttemptMark(
    string RunId, string Status, bool EndedOnInfrastructure, bool HandedBack,
    AttemptCause? Cause = null, string? EndKind = null);
