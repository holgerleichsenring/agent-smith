using System.Net;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.Acts;

namespace AgentSmith.Server.Services.SpecDialog.Continuation;

/// <summary>
/// 2026-10-28-7a69c: an approved comment in a ticket's conversation, posted on that ticket. Nothing
/// is recorded under the ticket and nothing starts.
/// <para>
/// The comment heads itself as ours ("Agent Smith —"), so a comment webhook never takes it for a
/// person's and a parked run never reads it as an answer. The conversation it came from stays on
/// the ticket for whoever works it next.
/// </para>
/// </summary>
public sealed class TicketCommentPost(IActRun acts)
{
    /// <summary>The head line that marks the comment as the framework's.</summary>
    public const string Heading = "Agent Smith — from the conversation on this ticket";

    /// <summary>2026-11-17-e061c: posted by a 'comment' run with a run id, which also tells the ticket's pages.</summary>
    public async Task<TicketContinuationOutcome> PostAsync(
        ResolvedProject project, string ticketId, TicketCommentOutcome comment, CancellationToken ct)
    {
        var slot = new ActSlot<IReadOnlyList<TicketCommentPosted>>();
        var result = await acts.RunAsync(project.Name, PipelinePresets.CommentName, new Dictionary<string, object>
        {
            [ContextKeys.PostTicketCommentsRequest] = new PostTicketCommentsRequest([new(ticketId, Render(comment.Comment))], NudgeActivity: true),
            [ContextKeys.ActOutcome] = slot,
        });
        var error = slot.Value is [{ } posted] ? posted.Error : result.Message ?? "the comment run ended first";
        return error is null
            ? new($"The comment is posted on ticket {ticketId}. Nothing was started.", true)
            : new($"The comment was not posted on ticket {ticketId}: {error}. Approve again to retry.", false);
    }

    /// <summary>The comment in the HTML subset every tracker's comment post takes: lines escaped and joined by breaks.</summary>
    internal static string Render(string comment) => $"{Heading}<br/><br/>"
        + string.Join("<br/>", comment.Replace("\r\n", "\n").Split('\n').Select(WebUtility.HtmlEncode));
}
