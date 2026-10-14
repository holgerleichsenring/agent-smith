using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: the link between a ticket a person closed and the follow-up filed for it. No
/// tracker provider relates two tickets, so the link is one comment on each naming the other — headed
/// "Agent Smith —", which every thread reader takes as ours rather than as somebody answering.
/// </summary>
public sealed class FollowUpLink(ITicketProviderFactory trackers, ILogger<FollowUpLink> logger)
{
    public const string Heading = "Agent Smith — follow-up";

    /// <summary>What did not land, for the thread; null when both comments were posted. 2026-10-13-9a59t:
    /// <paramref name="askedBy"/> names the person whose act on the closed ticket filed it; null is an approval.</summary>
    public async Task<string?> LinkAsync(
        ResolvedProject project, string closedTicketId, FiledTicket followUp, CancellationToken ct, string? askedBy = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(followUp);
        var provider = trackers.Create(project.Tracker);
        var closedKey = FiledTicketKey.Of(project.Tracker.Type, closedTicketId);
        var followUpKey = followUp.Key ?? followUp.Reference;
        var missed = new List<string>();
        if (!await PostAsync(provider, closedTicketId,
                $"{Heading}: this ticket was closed, so "
                + (askedBy is null ? "the work approved in its conversation" : $"what {askedBy} asked for")
                + $" continues on {followUpKey} ({followUp.Reference}).", ct))
            missed.Add(closedKey);
        if (followUp.TicketId is { Length: > 0 } id && !await PostAsync(provider, id,
                $"{Heading}: this ticket follows up {closedKey}, which a person closed before "
                + (askedBy is null ? "its conversation approved this work." : $"{askedBy} asked for this work."), ct))
            missed.Add(followUpKey);
        return missed.Count == 0 ? null
            : $"The link comment could not be posted on {string.Join(" and ", missed)}; name the other ticket there by hand.";
    }

    private async Task<bool> PostAsync(ITicketProvider provider, string ticketId, string comment, CancellationToken ct)
    {
        try
        {
            await provider.UpdateStatusAsync(new TicketId(ticketId), comment, ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The follow-up link comment on ticket {Ticket} was not posted", ticketId);
            return false;
        }
    }
}
