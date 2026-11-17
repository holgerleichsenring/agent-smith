namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061b: the existing ticket a 'start' run makes claimable, and whether its approver may move it
/// into a trigger status (runs.control).</summary>
public sealed record StartTicketRequest(string TicketId, bool MayStartRuns);
