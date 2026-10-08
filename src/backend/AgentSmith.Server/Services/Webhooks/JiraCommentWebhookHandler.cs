using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Jira comment_created webhooks. p0140b: filters by comment_keyword / PlanAnswers
/// presence and the issue status gate, then dispatches via WebhookSpawnDispatcher.
/// </summary>
public sealed class JiraCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    KeywordCommentRouter router,
    ApprovedRecordProbe approvals,
    PlanAnswerParser planAnswerParser,
    ILogger<JiraCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "jira" && eventType == "comment_created";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var commentBody = ExtractCommentBody(root);
            if (OwnTicketComment.IsOurs(commentBody))
                return WebhookResult.NotHandled("the comment is agent-smith's own");
            var planAnswers = planAnswerParser.Parse(commentBody);
            var hasAnswers = planAnswers.Count > 0;

            var issueKey = root.GetProperty("issue").GetProperty("key").GetString()!;
            var issueStatus = JiraAssigneeWebhookHandler.ExtractIssueStatus(root);
            var ticketUrl = root.GetProperty("issue").TryGetProperty("self", out var selfEl)
                ? selfEl.GetString() : null;

            var envelope = WebhookEnvelopeBuilders.BuildForJiraIssue(root, issueKey, ticketUrl);
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            // 2026-09-25-3c7aa: a webhook route is per PLATFORM, so the record is looked for on
            // every connection of that type; a ticket whose stamp is gone still binds.
            envelope = envelope with
            {
                HasApprovedRecord = await approvals.ExistsForPlatformAsync(
                    config, envelope.Platform, envelope.TicketId, cancellationToken),
            };
            var matches = envelopeResolver.Resolve(config, envelope);
            var act = PayloadActTime.Act(
                PayloadActTime.Text(root, "comment", "author", "displayName"), PayloadActTime.Text(root, "comment", "created"));
            return await router.RouteAsync(config, matches, new KeywordComment(envelope, issueStatus, commentBody,
                hasAnswers ? new Dictionary<string, string>(planAnswers) : null, act), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Jira comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static string ExtractCommentBody(JsonElement root)
    {
        if (root.TryGetProperty("comment", out var comment)
            && comment.TryGetProperty("body", out var body))
        {
            return body.ValueKind == JsonValueKind.String
                ? body.GetString() ?? string.Empty
                : body.GetRawText();
        }
        return string.Empty;
    }
}
