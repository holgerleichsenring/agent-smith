using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-27-481ba: a ticket's comments, read in a try OF THEIR OWN.
/// <para>
/// They used to share the ticket's, so a tracker that served the ticket and refused its comments
/// left the conversation with no ticket at all — a whole grounding lost to the part of it that
/// matters least. What this does NOT do is tell a reader which of the two happened: the stored row
/// has no field for it, and adding one would be a migration for a distinction the prompt cannot
/// act on.
/// </para>
/// </summary>
public sealed class TicketDiscussion(ILogger<TicketDiscussion> logger)
{
    public async Task<IReadOnlyList<TicketComment>> OfAsync(
        ITicketProvider provider, Ticket ticket, CancellationToken ct)
    {
        if (!provider.SupportsComments) return [];
        try
        {
            return await provider.GetCommentsAsync(ticket.Id, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex,
                "The discussion on ticket {Ticket} could not be read; the conversation is grounded "
                + "in the ticket itself", ticket.Id.Value);
            return [];
        }
    }
}
