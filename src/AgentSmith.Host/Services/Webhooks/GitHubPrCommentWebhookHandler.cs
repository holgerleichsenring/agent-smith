using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Host.Services.Webhooks;

/// <summary>
/// Handles GitHub PR comment events (issue_comment on PRs, pull_request_review_comment).
/// Parses agent commands and triggers the appropriate pipeline.
/// </summary>
public sealed class GitHubPrCommentWebhookHandler(
    ILogger<GitHubPrCommentWebhookHandler> logger) : IWebhookHandler
{
    private static readonly HashSet<string> SupportedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "issue_comment",
        "pull_request_review_comment",
    };

    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && SupportedEventTypes.Contains(eventType);

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.GetProperty("action").GetString() != "created")
                return Task.FromResult(new WebhookResult(false, null, null));

            var commentBody = root.GetProperty("comment").GetProperty("body").GetString() ?? "";
            var authorLogin = root.GetProperty("comment").GetProperty("user").GetProperty("login").GetString() ?? "";
            var commentId = root.GetProperty("comment").GetProperty("id").GetInt64().ToString();
            var repoFullName = root.GetProperty("repository").GetProperty("full_name").GetString() ?? "";

            var prNumber = ExtractPrNumber(root);
            if (prNumber is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            var intentType = CommentIntentParser.Parse(commentBody, out var pipeline, out var arguments, out _);

            switch (intentType)
            {
                case CommentIntentType.NewJob:
                    var triggerInput = BuildTriggerInput(pipeline!, arguments, repoFullName, prNumber.Value);
                    logger.LogInformation(
                        "PR comment command from {Author} on {Repo}#{Pr}: pipeline={Pipeline}",
                        authorLogin, repoFullName, prNumber.Value, pipeline);
                    return Task.FromResult(new WebhookResult(true, triggerInput, pipeline));

                case CommentIntentType.Help:
                    logger.LogInformation(
                        "PR comment help request from {Author} on {Repo}#{Pr}",
                        authorLogin, repoFullName, prNumber.Value);
                    return Task.FromResult(new WebhookResult(false, null, null));

                case CommentIntentType.DialogueApprove:
                case CommentIntentType.DialogueReject:
                    // Dialogue answers are handled by CommentIntentRouter (Step 6)
                    logger.LogDebug(
                        "PR comment dialogue {Intent} from {Author} on {Repo}#{Pr} — deferred to router",
                        intentType, authorLogin, repoFullName, prNumber.Value);
                    return Task.FromResult(new WebhookResult(false, null, null));

                default:
                    return Task.FromResult(new WebhookResult(false, null, null));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub PR comment webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static int? ExtractPrNumber(JsonElement root)
    {
        // pull_request_review_comment events have a top-level "pull_request" object
        if (root.TryGetProperty("pull_request", out var pr))
            return pr.GetProperty("number").GetInt32();

        // issue_comment events: only handle if the issue has a "pull_request" property
        if (root.TryGetProperty("issue", out var issue))
        {
            if (!issue.TryGetProperty("pull_request", out _))
                return null; // Plain issue comment, not a PR

            return issue.GetProperty("number").GetInt32();
        }

        return null;
    }

    private static string BuildTriggerInput(string pipeline, string? arguments, string repoFullName, int prNumber)
    {
        if (!string.IsNullOrEmpty(arguments))
            return $"{pipeline} {arguments}";

        return $"{pipeline} pr:{repoFullName}#{prNumber}";
    }
}
