namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-09-25-8e51e: what a rewriter says about a ticket body that carries no framework region,
/// written once because three trackers say it.
/// </summary>
internal static class TicketRegionRefusal
{
    /// <summary>
    /// THE COMMON CASE, AND IT IS REFUSED. Every ticket filed before this phase carries a body
    /// the framework rendered whole, with no marker saying where its text ends and a person's
    /// begins. Replacing the whole body would delete prose nobody can attribute; appending a
    /// second rendering would leave the ticket saying two things at once. So nothing is written
    /// and the operator is told why — a ticket filed from now on carries the markers and amends.
    /// </summary>
    internal const string NoRegion =
        "This ticket's body carries no agent-smith region marker, so it was filed before the "
        + "framework marked its own text. Nothing in the body says where its rendering ends and "
        + "your prose begins, so nothing was rewritten — edit the ticket yourself, or file the "
        + "amended specification as a new ticket.";
}
