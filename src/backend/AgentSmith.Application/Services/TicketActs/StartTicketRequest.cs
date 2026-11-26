namespace AgentSmith.Application.Services.TicketActs;

/// <summary>2026-11-17-e061b: the existing ticket a 'start' run makes claimable, and whether its approver may move it
/// into a trigger status (runs.control). 2026-11-26-5f6e: <paramref name="FiledFromText"/> for a ticket its series filed from
/// a text, which gets its trigger label (<see cref="Filing.FiledWorkTriggerLabel"/>).</summary>
public sealed record StartTicketRequest(string TicketId, bool MayStartRuns, bool FiledFromText = false);
