using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9b: what a ticket comment starts, the same way on every tracker. A match whose
/// trigger's comment_keyword the body carries — or a comment holding plan answers — is kept; only a
/// comment that is not a rework takes the status-gated dispatch it always took.
/// <para>2026-10-08-0781: a keyword comment on a ticket with a code attempt nudges the ticket and
/// returns — the worker reads the comment from the tracker, decides, and answers. Plan answers and
/// a ticket no run has worked on still dispatch here, in the request.</para>
/// </summary>
public sealed class KeywordCommentRouter(
    WebhookSpawnDispatcher dispatcher,
    IPreviousAttemptReader attempts,
    IReworkNudges nudges,
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
            if (!await NudgedAsync(match, comment, ct))
                await dispatcher.DispatchAsync(config, [match], comment.Envelope, comment.PayloadStatus, comment.PlanAnswers, ct);
        return WebhookResult.HandledNoRoute();
    }

    private async Task<bool> NudgedAsync(ProjectMatch match, KeywordComment comment, CancellationToken ct)
    {
        if (comment.Act is null || comment.PlanAnswers is { Count: > 0 }) return false;
        var ticketId = comment.Envelope.TicketId!;
        if (await attempts.LatestAsync(match.ProjectName, ticketId, null, ct) is null) return false;
        await nudges.EnqueueAsync(new ReworkNudgeRequest(match.ProjectName, ticketId, ReworkNudgeOrigin.Ticket,
            Channel: ReworkChannel.Ticket), ct);
        return true;
    }

    private static bool CarriesKeyword(AgentSmithConfig config, ProjectMatch match, string body) =>
        TriggerSelectionHelper.ByKind(config.Projects[match.ProjectName], match.Kind)?.CommentKeyword is { Length: > 0 } keyword
        && body.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
