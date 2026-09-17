using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-a5a5: the ticket a cut review is shown — its title, its description and its
/// acceptance criteria together. A ticket with a title and no description is still a ticket,
/// and a review shown only the description would be told there is none. Empty only when all
/// three are blank.
/// </summary>
public static class SpecCutReviewTicketText
{
    public static string Of(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return string.Join("\n\n",
            new[] { ticket.Title, ticket.Description, ticket.AcceptanceCriteria }
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .Select(part => part!.Trim()));
    }
}
