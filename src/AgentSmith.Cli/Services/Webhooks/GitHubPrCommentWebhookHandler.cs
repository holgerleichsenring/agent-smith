using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

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

    private static readonly HashSet<string> TrustedAssociations = new(StringComparer.OrdinalIgnoreCase)
    {
        "OWNER",
        "MEMBER",
        "COLLABORATOR",
        "CONTRIBUTOR",
    };

    private static readonly HashSet<string> AllowedPipelines = new(StringComparer.OrdinalIgnoreCase)
    {
        "fix-bug",
        "security-scan",
        "pr-review",
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

            var comment = root.GetProperty("comment");
            var commentBody = comment.GetProperty("body").GetString() ?? "";
            var authorLogin = comment.GetProperty("user").GetProperty("login").GetString() ?? "";
            var commentId = comment.GetProperty("id").GetInt64().ToString();
            var repoFullName = root.GetProperty("repository").GetProperty("full_name").GetString() ?? "";

            var authorAssociation = comment.TryGetProperty("author_association", out var assocProp)
                ? assocProp.GetString() ?? ""
                : "";

            if (!TrustedAssociations.Contains(authorAssociation))
            {
                logger.LogInformation(
                    "Ignoring PR comment from {Author} on {Repo} — author_association={Association} is not trusted",
                    authorLogin, repoFullName, authorAssociation);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var prNumber = ExtractPrNumber(root);
            if (prNumber is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            var intentType = CommentIntentParser.Parse(commentBody, out var pipeline, out var arguments, out _);

            switch (intentType)
            {
                case CommentIntentType.NewJob:
                    if (pipeline is not null && !AllowedPipelines.Contains(pipeline))
                    {
                        logger.LogInformation(
                            "Ignoring PR comment from {Author} on {Repo}#{Pr}: pipeline={Pipeline} is not allowed",
                            authorLogin, repoFullName, prNumber.Value, pipeline);
                        return Task.FromResult(new WebhookResult(false, null, null));
                    }

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
                    var answer = intentType == CommentIntentType.DialogueApprove ? "yes" : "no";
                    var dialogueComment = CommentIntentParser.Parse(commentBody, out _, out _, out var parsedComment);
                    var dialogueData = new DialogueAnswerData(
                        Platform: "github",
                        RepoFullName: repoFullName,
                        PrIdentifier: prNumber.Value.ToString(),
                        Answer: answer,
                        Comment: parsedComment,
                        AuthorLogin: authorLogin);
                    logger.LogInformation(
                        "PR comment dialogue {Intent} from {Author} on {Repo}#{Pr}",
                        intentType, authorLogin, repoFullName, prNumber.Value);
                    return Task.FromResult(new WebhookResult(true, null, null, dialogueData));

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
