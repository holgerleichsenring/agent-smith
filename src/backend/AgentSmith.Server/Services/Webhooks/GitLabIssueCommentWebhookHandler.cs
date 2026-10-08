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
/// Handles GitLab Note Hook events on issues. Filters by comment_keyword / PlanAnswers
/// presence and the issue status gate before dispatching to WebhookSpawnDispatcher.
/// </summary>
public sealed class GitLabIssueCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    KeywordCommentRouter router,
    ApprovedRecordProbe approvals,
    PlanAnswerParser planAnswerParser,
    ILogger<GitLabIssueCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "note hook";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var noteAttrs = root.GetProperty("object_attributes");
            if (noteAttrs.GetProperty("noteable_type").GetString() != "Issue")
                return WebhookResult.NotHandled();

            var noteBody = noteAttrs.GetProperty("note").GetString() ?? "";
            if (OwnTicketComment.IsOurs(noteBody))
                return WebhookResult.NotHandled("the comment is agent-smith's own");
            var repoUrl = root.TryGetProperty("project", out var proj)
                ? proj.GetProperty("web_url").GetString() ?? "" : "";

            if (!root.TryGetProperty("issue", out var issueEl))
                return WebhookResult.NotHandled();

            var issueState = issueEl.GetProperty("state").GetString() ?? "";
            var issueId = issueEl.GetProperty("iid").GetInt32();
            var ticketUrl = issueEl.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            var planAnswers = planAnswerParser.Parse(noteBody);
            var envelope = WebhookEnvelopeBuilders.BuildForGitLabIssue(
                root, issueId.ToString(), repoUrl, ticketUrl);

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
                PayloadActTime.Text(root, "user", "username"), PayloadActTime.Text(noteAttrs, "created_at"));
            return await router.RouteAsync(config, matches, new KeywordComment(envelope, issueState, noteBody,
                planAnswers.Count > 0 ? new Dictionary<string, string>(planAnswers) : null, act), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab Note Hook webhook");
            return WebhookResult.NotHandled();
        }
    }
}
