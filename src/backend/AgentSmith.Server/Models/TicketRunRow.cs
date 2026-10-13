using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59a: one run of a ticket as the ticket reads see it — the row and whether it
/// ever started a step, which is what tells a run that did nothing from one that did work.
/// </summary>
public sealed record TicketRunRow(Run Run, bool HasStep);
