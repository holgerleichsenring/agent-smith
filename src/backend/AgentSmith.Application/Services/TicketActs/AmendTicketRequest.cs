using AgentSmith.Application.Services.Filing;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.TicketActs;

/// <summary>
/// 2026-11-17-e061c: what an 'amend' run writes under a never-worked ticket — seeded by the server after it read the
/// ticket's previous record, rendered the amended series and checked no run holds the ticket.
/// </summary>
/// <param name="Approval">The approval the record carries: conversation, principal, scope and the operator's words.</param>
/// <param name="TicketId">The ticket the conversation belongs to.</param>
/// <param name="Set">The approved series, its base kept from the ticket's filing.</param>
/// <param name="Goal">The series' goal.</param>
/// <param name="Region">The description region the tracker shows.</param>
/// <param name="Previous">The record it replaces; null when there was none.</param>
/// <param name="Continuation">The record's continuation; null for a plain never-worked hand-off.</param>
public sealed record AmendTicketRequest(
    ApprovalSource Approval, string TicketId, FiledSeries Set, string Goal, string Region,
    SpecApprovalRecord? Previous, SeriesContinuation? Continuation);
