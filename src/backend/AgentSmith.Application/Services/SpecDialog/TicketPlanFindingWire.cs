namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>2026-10-14-dbcek: one item of the plan reviewer's JSON answer, as it was written.</summary>
internal sealed record TicketPlanFindingWire(string? Ticket, string? Problem, string? Why);
