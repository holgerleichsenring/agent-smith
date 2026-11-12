using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4cd: a ticket's one-line fields as one line — its title and each done item, whitespace and line
/// breaks folded to single spaces. The filed body lists each done item under the acceptance criteria, where only a
/// list item reads back as a criterion; a break is normalised here, never refused.
/// </summary>
public static class GraphTicketLines
{
    public static GraphTicket Normalized(GraphTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return ticket with
        {
            Title = CriterionLine.Collapse(ticket.Title),
            Done = [.. ticket.Done.Select(CriterionLine.Collapse)],
        };
    }
}
