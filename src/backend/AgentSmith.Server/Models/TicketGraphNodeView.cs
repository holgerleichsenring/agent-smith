namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-14-dbcen: one ticket of the graph, computed per read. <paramref name="Id"/> is the tracker connection
/// and the ticket key, the identity the edges name; <paramref name="Key"/> is what a person calls the ticket.
/// </summary>
/// <param name="State">merged | done-by-hand | running | blocked | ready, else the list bucket; idle without a page.</param>
/// <param name="HasPage">The ticket has a run or a filing record here, so its page reads.</param>
public sealed record TicketGraphNodeView(
    string Id, string Project, string TicketId, string Key, string Title, string State, string Bucket, bool HasPage);
