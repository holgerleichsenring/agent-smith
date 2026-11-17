using AgentSmith.Contracts.Models;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-14-dbcek: one ticket of a proposed list as the proposal pane reads it; 2026-11-17-5701a: with its specs.</summary>
public sealed record SpecDialogGraphTicketView(
    string Key, string Title, string Requirement, IReadOnlyList<string> Done, IReadOnlyList<string> Requires,
    IReadOnlyList<string> Repos)
{
    /// <summary>2026-11-17-5701a: the ticket's specs, in the spec view the pane draws a spec in.</summary>
    public IReadOnlyList<SpecDialogDraftView> Specs { get; init; } = [];

    public static SpecDialogGraphTicketView Of(GraphTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        return new(ticket.Key, ticket.Title, ticket.Requirement, ticket.Done, ticket.Requires, ticket.Repos)
        {
            Specs = [.. ticket.Specs.Select(SpecDialogProposalComposer.View)],
        };
    }
}
