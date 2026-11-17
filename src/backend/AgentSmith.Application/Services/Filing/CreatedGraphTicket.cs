using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Filing;

/// <summary>One created ticket of a filing: its stored requires edges, the labels it was created with, and whether its
/// filing is COMPLETE — created, its edges stored, and (with a series) its approval record stored and its branch
/// written. 2026-11-17-5701b: the labels travel with it, so a bug starts with none and a spec ticket with its stamp.</summary>
public sealed record CreatedGraphTicket(
    CreatedTicket Ticket, IReadOnlyList<TicketRequirementEdge> Edges, bool Complete, IReadOnlyList<string> Labels);
