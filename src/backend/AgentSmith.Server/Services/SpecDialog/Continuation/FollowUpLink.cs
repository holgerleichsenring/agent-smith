using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.Filing;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-13-9a59l: the link between a ticket a person closed and the follow-up filed for it. No
/// tracker provider relates two tickets, so the link is one comment on each naming the other — headed
/// "Agent Smith —", which every thread reader takes as ours rather than as somebody answering.
/// </summary>
public sealed class FollowUpLink(Acts.IActRun acts)
{
    public const string Heading = "Agent Smith — follow-up";

    /// <summary>What did not land, for the thread; null when both comments were posted. 2026-10-13-9a59t:
    /// <paramref name="askedBy"/> names the person whose act on the closed ticket filed it; null is an approval.
    /// 2026-11-17-e061d: both comments are posted by one 'comment' run with a run id.</summary>
    public async Task<string?> PostLinksAsync(
        ResolvedProject project, string closedTicketId, FiledTicket followUp, CancellationToken ct, string? askedBy = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(followUp);
        var closedKey = FiledTicketKey.Of(project.Tracker.Type, closedTicketId);
        var followUpKey = followUp.Key ?? followUp.Reference;
        List<TicketCommentToPost> comments = [new(closedTicketId, $"{Heading}: this ticket was closed, so "
            + (askedBy is null ? "the work approved in its conversation" : $"what {askedBy} asked for")
            + $" continues on {followUpKey} ({followUp.Reference}).")];
        if (followUp.TicketId is { Length: > 0 } id)
            comments.Add(new(id, $"{Heading}: this ticket follows up {closedKey}, which a person closed before "
                + (askedBy is null ? "its conversation approved this work." : $"{askedBy} asked for this work.")));
        var slot = new ActSlot<IReadOnlyList<TicketCommentPosted>>();
        await acts.RunAsync(project.Name, PipelinePresets.CommentName, new Dictionary<string, object>
        {
            [ContextKeys.PostTicketCommentsRequest] = new PostTicketCommentsRequest(comments, NudgeActivity: false),
            [ContextKeys.ActOutcome] = slot,
        });
        var missed = comments.Where(c => slot.Value?.FirstOrDefault(p => p.TicketId == c.TicketId) is not { Posted: true })
            .Select(c => c.TicketId == closedTicketId ? closedKey : followUpKey).ToList();
        return missed.Count == 0 ? null
            : $"The link comment could not be posted on {string.Join(" and ", missed)}; name the other ticket there by hand.";
    }
}
