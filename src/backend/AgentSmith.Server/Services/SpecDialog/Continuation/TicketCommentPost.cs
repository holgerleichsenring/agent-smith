using System.Net;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

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
public sealed class TicketCommentPost(
    ITicketProviderFactory trackers, ITicketActivityNudge activity, ILogger<TicketCommentPost> logger)
{
    /// <summary>The head line that marks the comment as the framework's.</summary>
    public const string Heading = "Agent Smith — from the conversation on this ticket";

    public async Task<TicketContinuationOutcome> PostAsync(
        ResolvedProject project, string ticketId, TicketCommentOutcome comment, CancellationToken ct)
    {
        try
        {
            await trackers.Create(project.Tracker).UpdateStatusAsync(new TicketId(ticketId), Render(comment.Comment), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The approved comment on ticket {Ticket} was not posted", ticketId);
            return new($"The comment was not posted on ticket {ticketId}: {ex.Message.Split('\n', 2)[0].Trim()}. Approve again to retry.", false);
        }
        await activity.ChangedAsync(project.Name, TicketKey.For(project.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value, ct);
        return new($"The comment is posted on ticket {ticketId}. Nothing was started.", true);
    }

    /// <summary>The comment in the HTML subset every tracker's comment post takes: lines escaped and joined by breaks.</summary>
    internal static string Render(string comment) => $"{Heading}<br/><br/>"
        + string.Join("<br/>", comment.Replace("\r\n", "\n").Split('\n').Select(WebUtility.HtmlEncode));
}
