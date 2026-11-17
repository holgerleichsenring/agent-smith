using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// 2026-11-17-dd05d: whether the ticket as its tracker holds it still states the goals of its approved specs — the
/// conversation's divergence check (<see cref="ApprovedSetDivergence"/>), asked on every tracker read of the ticket
/// page so a person's edit in the tracker shows there at once. The text is read as plain text: Azure DevOps answers
/// HTML, which would never contain a goal with an ampersand or a quote verbatim. The title is left out: a filed spec's
/// title carries its goal, so a body that lost every goal would still read as stating it.
/// </summary>
public sealed class TicketTextDivergence(ApprovedSetDivergence divergence)
{
    public Task<SetDivergence?> OfAsync(ResolvedProject project, string ticketId, Ticket ticket, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var text = string.Join("\n", new[] { ticket.Description, ticket.AcceptanceCriteria }
            .Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => TicketHtmlConverter.ToText(f)));
        return divergence.ForAsync(project, ticketId, text, ct, approvedOnly: true);
    }
}
