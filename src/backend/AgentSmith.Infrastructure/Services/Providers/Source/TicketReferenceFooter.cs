using AgentSmith.Contracts.Providers;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-10-25-9dbba: the reference every code host can carry — a 'Ticket: …' footer below the
/// operator-facing body. Never a closing keyword: a merge does not close a ticket, a person does.
/// </summary>
public static class TicketReferenceFooter
{
    /// <summary>The description with the given reference appended as a footer.</summary>
    public static string Append(string description, string reference) =>
        $"{description}\n\nTicket: {reference}";

    /// <summary>
    /// The description with the ticket's web url as a footer, or unchanged when there is no
    /// ticket or the ticket has no web url.
    /// </summary>
    public static string AppendWebLink(string description, PullRequestTicket? ticket) =>
        string.IsNullOrWhiteSpace(ticket?.WebUrl) ? description : Append(description, ticket.WebUrl);
}
