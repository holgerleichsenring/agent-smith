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
    WebhookSpawnDispatcher dispatcher,
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
            var planAnswers = planAnswerParser.Parse(commentBody);
            var hasAnswers = planAnswers.Count > 0;

            var issueKey = root.GetProperty("issue").GetProperty("key").GetString()!;
            var issueStatus = JiraAssigneeWebhookHandler.ExtractIssueStatus(root);
            var ticketUrl = root.GetProperty("issue").TryGetProperty("self", out var selfEl)
                ? selfEl.GetString() : null;

            var envelope = WebhookEnvelopeBuilders.BuildForJiraIssue(root, issueKey, ticketUrl);
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var matches = envelopeResolver.Resolve(config, envelope);
            var filtered = FilterByKeywordOrAnswers(config, matches, commentBody, hasAnswers);

            if (filtered.Count == 0 && matches.Count > 0)
            {
                logger.LogDebug("Jira comment on {Key}: matched but no keyword/answers — ignoring",
                    issueKey);
                return WebhookResult.NotHandled();
            }

            var planAnswersDict = hasAnswers ? new Dictionary<string, string>(planAnswers) : null;
            await dispatcher.DispatchAsync(
                config, filtered, envelope, issueStatus, planAnswersDict, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Jira comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static IReadOnlyList<ProjectMatch> FilterByKeywordOrAnswers(
        AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches,
        string commentBody, bool hasAnswers)
    {
        if (matches.Count == 0) return matches;
        var kept = new List<ProjectMatch>(matches.Count);
        foreach (var match in matches)
        {
            var trigger = config.Projects[match.ProjectName].JiraTrigger;
            var hasKeyword = trigger?.CommentKeyword is { } kw
                && commentBody.Contains(kw, StringComparison.OrdinalIgnoreCase);
            if (hasAnswers || hasKeyword) kept.Add(match);
        }
        return kept;
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
