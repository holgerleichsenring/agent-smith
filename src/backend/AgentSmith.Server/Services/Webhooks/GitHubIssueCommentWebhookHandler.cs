using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub issue_comment events for re-triggering pipelines. Filters on configured
/// comment_keyword (or PlanAnswers presence) and the issue status gate before resolving
/// project matches through IEnvelopeProjectResolver and dispatching the spawn.
/// </summary>
public sealed class GitHubIssueCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    WebhookSpawnDispatcher dispatcher,
    PlanAnswerParser planAnswerParser,
    ILogger<GitHubIssueCommentWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "issue_comment";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.GetProperty("action").GetString() != "created")
                return WebhookResult.NotHandled();

            if (!root.TryGetProperty("issue", out var issueEl)
                || issueEl.TryGetProperty("pull_request", out _))
                return WebhookResult.NotHandled();

            var commentBody = root.GetProperty("comment").GetProperty("body").GetString() ?? "";
            var repoUrl = root.GetProperty("repository").GetProperty("html_url").GetString() ?? "";
            var issueNumber = issueEl.GetProperty("number").GetInt32();
            var issueState = issueEl.GetProperty("state").GetString() ?? "open";
            var ticketUrl = issueEl.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;

            var planAnswers = planAnswerParser.Parse(commentBody);
            var envelope = WebhookEnvelopeBuilders.BuildForGitHubIssue(
                issueEl, issueNumber.ToString(), repoUrl, ticketUrl);

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var matches = envelopeResolver.Resolve(config, envelope);
            var filtered = FilterMatchesByCommentKeywordOrAnswers(
                config, matches, commentBody, planAnswers.Count > 0);

            if (filtered.Count == 0 && matches.Count > 0)
            {
                logger.LogDebug(
                    "GitHub issue comment #{Issue}: matched but no keyword/answers — ignoring",
                    issueNumber);
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
            logger.LogWarning(ex, "Failed to parse GitHub issue_comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static IReadOnlyList<ProjectMatch> FilterMatchesByCommentKeywordOrAnswers(
        AgentSmithConfig config,
        IReadOnlyList<ProjectMatch> matches,
        string commentBody,
        bool hasAnswers)
    {
        if (matches.Count == 0) return matches;
        var kept = new List<ProjectMatch>(matches.Count);
        foreach (var match in matches)
        {
            var trigger = config.Projects[match.ProjectName].GithubTrigger;
            var hasKeyword = trigger?.CommentKeyword is { } kw
                && commentBody.Contains(kw, StringComparison.OrdinalIgnoreCase);
            if (hasAnswers || hasKeyword) kept.Add(match);
        }
        return kept;
    }
}
