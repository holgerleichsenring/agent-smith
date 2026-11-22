using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using AgentSmith.Server.Services.Tickets.Conversation;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// 2026-11-22-a720b: where a person's edit of a ticket's text is put — the ticket's open conversation as a Notice turn,
/// kept and sent like every framework line there (the series' writer of one, absent where no conversation surface is
/// composed); a ticket without one gets agent-smith's comment through a 'comment' run, which its conversation shows as
/// a thread entry and a reply from the ticket page answers.
/// </summary>
public sealed class TicketTextEditPost(
    TicketTranscriptReader transcripts, IActRun acts, ILogger<TicketTextEditPost> logger, ISeriesConversation? conversations = null)
{
    /// <summary>The head line that marks the comment as agent-smith's.</summary>
    public const string Heading = "Agent Smith — the ticket text was changed";

    public async Task PostAsync(ResolvedProject project, string ticketId, string text, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (conversations is not null && await transcripts.SessionAsync(project, ticketId, ct) is { IsOpen: true } session)
        {
            await conversations.SayAsync(session.SessionId, text, ct);
            return;
        }
        var slot = new ActSlot<IReadOnlyList<TicketCommentPosted>>();
        var result = await acts.RunAsync(project.Name, PipelinePresets.CommentName, new Dictionary<string, object>
        {
            [ContextKeys.PostTicketCommentsRequest] = new PostTicketCommentsRequest(
                [new(ticketId, TicketCommentPost.Render(text, Heading))], NudgeActivity: true),
            [ContextKeys.ActOutcome] = slot,
        });
        if (slot.Value is not [{ Error: null }])
            logger.LogWarning("The edit of ticket {Ticket} in {Project} was not posted: {Why}", ticketId, project.Name,
                slot.Value is [{ } posted] ? posted.Error : result.Message);
    }
}
