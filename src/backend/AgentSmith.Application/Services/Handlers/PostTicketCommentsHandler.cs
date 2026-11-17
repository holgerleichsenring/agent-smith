using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-17-e061c: the step of a 'comment' run — posts each comment on its ticket and reports, per comment, whether it
/// landed. A comment that did not post is an answer, not a failure: the others are still posted.
/// </summary>
public sealed class PostTicketCommentsHandler(
    ITicketProviderFactory trackers, ITicketActivityNudge activity, ILogger<PostTicketCommentsHandler> logger)
    : ICommandHandler<PostTicketCommentsContext>
{
    public async Task<CommandResult> ExecuteAsync(PostTicketCommentsContext context, CancellationToken cancellationToken)
    {
        var pipeline = context.Pipeline;
        if (!pipeline.TryGet<PostTicketCommentsRequest>(ContextKeys.PostTicketCommentsRequest, out var request) || request is null
            || !pipeline.TryGet<ActSlot<IReadOnlyList<TicketCommentPosted>>>(ContextKeys.ActOutcome, out var slot) || slot is null)
            return CommandResult.Fail("A 'comment' run needs the request and outcome slot the server seeds under "
                + $"ContextKeys.{nameof(ContextKeys.PostTicketCommentsRequest)} and ContextKeys.{nameof(ContextKeys.ActOutcome)}. Nothing was posted.");
        var posted = new List<TicketCommentPosted>(request.Comments.Count);
        foreach (var comment in request.Comments) posted.Add(await PostAsync(context, comment, request.NudgeActivity));
        slot.Set(posted);
        return CommandResult.Ok($"Posted {posted.Count(p => p.Posted)} of {posted.Count} comment(s)");
    }

    private async Task<TicketCommentPosted> PostAsync(PostTicketCommentsContext context, TicketCommentToPost comment, bool nudge)
    {
        var project = context.Project;
        try
        {
            await trackers.Create(project.Tracker).UpdateStatusAsync(new TicketId(comment.TicketId), comment.Text, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The comment on ticket {Ticket} was not posted", comment.TicketId);
            return new TicketCommentPosted(comment.TicketId, ex.Message.Split('\n', 2)[0].Trim());
        }
        if (nudge)
            await activity.ChangedAsync(project.Name, TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), comment.TicketId).Value, CancellationToken.None);
        return new TicketCommentPosted(comment.TicketId, null);
    }
}
