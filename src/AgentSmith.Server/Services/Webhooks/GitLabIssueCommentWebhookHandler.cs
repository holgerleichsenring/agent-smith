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
    WebhookSpawnDispatcher dispatcher,
    PlanAnswerParser planAnswerParser,
    ILogger<GitLabIssueCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "Note Hook";

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
            var matches = envelopeResolver.Resolve(config, envelope);
            var filtered = FilterByKeywordOrAnswers(config, matches, noteBody, planAnswers.Count > 0);

            if (filtered.Count == 0 && matches.Count > 0)
            {
                logger.LogDebug("GitLab issue note !{Issue}: matched but no keyword/answers — ignoring",
                    issueId);
                return WebhookResult.NotHandled();
            }

            var planAnswersDict = planAnswers.Count > 0
                ? new Dictionary<string, string>(planAnswers) : null;
            await dispatcher.DispatchAsync(
                config, filtered, envelope, issueState, planAnswersDict, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab Note Hook webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static IReadOnlyList<ProjectMatch> FilterByKeywordOrAnswers(
        AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches,
        string noteBody, bool hasAnswers)
    {
        if (matches.Count == 0) return matches;
        var kept = new List<ProjectMatch>(matches.Count);
        foreach (var match in matches)
        {
            var trigger = config.Projects[match.ProjectName].GitlabTrigger;
            var hasKeyword = trigger?.CommentKeyword is { } kw
                && noteBody.Contains(kw, StringComparison.OrdinalIgnoreCase);
            if (hasAnswers || hasKeyword) kept.Add(match);
        }
        return kept;
    }
}
