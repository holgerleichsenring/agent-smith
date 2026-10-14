using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Tickets;

/// <summary>The specs a run-record read serves, and the run acceptance it could not tie to one of them.</summary>
public sealed record TicketRunRecordComposition(
    IReadOnlyList<TicketSeriesSpecView> Specs,
    IReadOnlyList<TicketSeriesUnattributedView> Unattributed);
