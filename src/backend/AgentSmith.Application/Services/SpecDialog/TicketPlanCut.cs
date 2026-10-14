using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>2026-10-14-dbcek: one change the plan review asks of a ticket text — the quote, verbatim in that
/// ticket, and what it becomes; an empty replacement removes it.</summary>
public sealed record TicketPlanCut(string Ticket, string Quote, string Replacement, string Why);

/// <summary>2026-10-14-dbcek: what one plan review admitted — every finding it keeps, and the cuts among them.</summary>
public sealed record TicketPlanVerdict(IReadOnlyList<ProposalFinding> Findings, IReadOnlyList<TicketPlanCut> Cuts)
{
    public static TicketPlanVerdict Clean { get; } = new([], []);
}

/// <summary>One item of the plan reviewer's JSON answer, as it was written.</summary>
internal sealed record TicketPlanFindingWire(
    string? Ticket, string? Problem, string? Why, string? Quote = null, string? Replacement = null);
