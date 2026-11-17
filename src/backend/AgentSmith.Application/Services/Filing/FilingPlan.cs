namespace AgentSmith.Application.Services.Filing;

/// <summary>2026-11-17-5701b: the tickets a filing creates, in the order they are created — each one's requires name
/// only tickets before it or tickets the tracker already has.</summary>
public sealed record FilingPlan(IReadOnlyList<PlannedTicket> Tickets);
