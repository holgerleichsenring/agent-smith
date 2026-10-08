using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9b: what a ticket comment starts, the same way on every tracker. A match whose
/// trigger's comment_keyword the body carries — or a comment holding plan answers — is kept; a
/// keyword comment asks the rework entry first, and only a comment that is not a rework takes the
/// status-gated dispatch it always took. A refused rework is said on the ticket, in our own voice
/// and without the keyword, so the comment cannot come back as a trigger.
/// </summary>
public sealed class KeywordCommentRouter(
    WebhookSpawnDispatcher dispatcher,
    IReworkEntry rework,
    ITicketProviderFactory tickets,
    ILogger<KeywordCommentRouter> logger)
{
    public async Task<WebhookResult> RouteAsync(
        AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches, KeywordComment comment, CancellationToken ct)
    {
        if (matches.Count == 0)
        {
            await dispatcher.DispatchAsync(config, matches, comment.Envelope, comment.PayloadStatus, comment.PlanAnswers, ct);
            return WebhookResult.HandledNoRoute();
        }
        var kept = matches.Where(m => comment.PlanAnswers is { Count: > 0 } || CarriesKeyword(config, m, comment.Body)).ToList();
        if (kept.Count == 0)
        {
            logger.LogDebug("Comment on {Ticket}: matched but no keyword/answers — ignoring", comment.Envelope.TicketId);
            return WebhookResult.NotHandled();
        }
        foreach (var match in kept)
            if (!await ServedAsReworkAsync(config.Projects[match.ProjectName], match, comment, ct))
                await dispatcher.DispatchAsync(config, [match], comment.Envelope, comment.PayloadStatus, comment.PlanAnswers, ct);
        return WebhookResult.HandledNoRoute();
    }

    private async Task<bool> ServedAsReworkAsync(
        ResolvedProject project, ProjectMatch match, KeywordComment comment, CancellationToken ct)
    {
        if (comment.Act is null || comment.PlanAnswers is { Count: > 0 }) return false;
        var outcome = await rework.EnterAsync(project, comment.Envelope.TicketId!, comment.Act, match.PipelineName, ct);
        if (outcome.Kind == ReworkOutcomeKind.Refused) await SayRefusedAsync(project, comment.Envelope.TicketId!, outcome, ct);
        return outcome.Kind != ReworkOutcomeKind.NotARework;
    }

    private async Task SayRefusedAsync(ResolvedProject project, string ticketId, ReworkOutcome outcome, CancellationToken ct)
    {
        try
        {
            await tickets.Create(project.Tracker).UpdateStatusAsync(
                new TicketId(ticketId), ReworkTexts.TicketRefusal(outcome.Reason!), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not say on {Ticket} why the rework was refused", ticketId);
        }
    }

    private static bool CarriesKeyword(AgentSmithConfig config, ProjectMatch match, string body) =>
        TriggerSelectionHelper.ByKind(config.Projects[match.ProjectName], match.Kind)?.CommentKeyword is { Length: > 0 } keyword
        && body.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
