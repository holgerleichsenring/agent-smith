namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061b: the existing ticket a 'start' run makes claimable, and whether its approver may move it
/// into a trigger status (runs.control). 2026-11-27-3532a: a series ticket is no longer started by a 'start' run, so the
/// trigger label 5f6e put on a ticket its series filed from a text is gone with it.</summary>
public sealed record StartTicketRequest(string TicketId, bool MayStartRuns);
