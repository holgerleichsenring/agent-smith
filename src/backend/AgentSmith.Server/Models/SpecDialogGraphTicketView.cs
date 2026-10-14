using AgentSmith.Contracts.Models;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcek: one ticket text of a proposed list as the proposal pane reads it.</summary>
public sealed record SpecDialogGraphTicketView(
    string Key, string Title, string Requirement, IReadOnlyList<string> Done, IReadOnlyList<string> Requires,
    IReadOnlyList<string> Repos)
{
    public static SpecDialogGraphTicketView Of(GraphTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return new(ticket.Key, ticket.Title, ticket.Requirement, ticket.Done, ticket.Requires, ticket.Repos);
    }
}
