namespace AgentSmith.Contracts.Tickets;

/// <summary>
/// 2026-10-14-dbcef: the dependent ticket must wait until the required ticket is merged. Both tickets
/// live on one tracker connection, named by <paramref name="Tracker"/>.
/// </summary>
/// <param name="Tracker">The tracker CONNECTION's catalog name.</param>
/// <param name="TicketKey">The dependent's key, <c>TicketKey.For(platform, id)</c>.</param>
/// <param name="TicketId">The dependent's id in the tracker's own spelling, copied at filing.</param>
/// <param name="Project">The project the dependent was filed into, copied at filing.</param>
/// <param name="RequiredTicketKey">The key of the ticket that must be merged first.</param>
/// <param name="RequiredTicketId">The required ticket's id in the tracker's own spelling.</param>
/// <param name="StartsWhenMet">Whether the approver held runs.control at filing — one value per dependent.</param>
/// <param name="FiledAt">When the edge was stored; read from the row's insert time and ignored on write.</param>
/// <param name="FiledInConversation">The conversation the filing happened in.</param>
public sealed record TicketRequirementEdge(
    string Tracker,
    string TicketKey,
    string TicketId,
    string Project,
    string RequiredTicketKey,
    string RequiredTicketId,
    bool StartsWhenMet,
    DateTimeOffset FiledAt,
    string FiledInConversation);
