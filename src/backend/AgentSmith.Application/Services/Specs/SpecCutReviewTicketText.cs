using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-15-a5a5: the ticket a cut review is shown — its title, its description and its
/// acceptance criteria together. A ticket with a title and no description is still a ticket,
/// and a review shown only the description would be told there is none. Empty only when all
/// three are blank.
/// <para>
/// 2026-09-24-b3c1: an Azure DevOps body arrives as HTML, so each part is read as TEXT
/// through the converter the derivation path has used since p0399.
/// </para>
/// <para>
/// 2026-11-02-2b26c: each part is labelled. Unlabelled, a bug's title ("the discount is not applied
/// at 100.00") read to the reviewer as a statement of the rule, and the fix as its contradiction.
/// </para>
/// </summary>
public static class SpecCutReviewTicketText
{
    public static string Of(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var parts = new (string Label, string? Body)[]
        {
            ("Title: ", ticket.Title),
            ("Description:\n", ticket.Description),
            ("Acceptance criteria:\n", ticket.AcceptanceCriteria),
        };
        return string.Join("\n\n", parts
            .Select(part => (part.Label, Text: TicketHtmlConverter.ToText(part.Body)))
            .Where(part => !string.IsNullOrWhiteSpace(part.Text))
            .Select(part => part.Label + part.Text.Trim()));
    }
}
