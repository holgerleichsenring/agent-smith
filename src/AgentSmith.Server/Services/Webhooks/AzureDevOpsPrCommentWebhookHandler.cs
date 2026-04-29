using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps Pull Request comment events.
/// Parses agent commands and triggers the appropriate pipeline.
/// </summary>
public sealed class AzureDevOpsPrCommentWebhookHandler(
    ILogger<AzureDevOpsPrCommentWebhookHandler> logger) : IWebhookHandler
{
    private static readonly HashSet<string> AllowedPipelines = new(StringComparer.OrdinalIgnoreCase)
    {
        "fix-bug",
        "security-scan",
        "pr-review",
    };

    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops"
        && eventType.Equals("ms.vss-code.git-pullrequest-comment-event", StringComparison.OrdinalIgnoreCase);

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var resource = root.GetProperty("resource");
            var comment = resource.GetProperty("comment");

            var commentBody = comment.GetProperty("content").GetString() ?? "";
            var commentId = comment.GetProperty("id").GetInt64().ToString();
            var authorLogin = comment.GetProperty("author")
                .GetProperty("uniqueName").GetString() ?? "";

            var pullRequest = resource.GetProperty("pullRequest");
            var prId = pullRequest.GetProperty("pullRequestId").GetInt32();

            var repo = pullRequest.GetProperty("repository");
            var repoName = repo.GetProperty("name").GetString() ?? "";
            var projectName = repo.GetProperty("project")
                .GetProperty("name").GetString() ?? "";
            var repoFullName = $"{projectName}/{repoName}";

            var intentType = CommentIntentParser.Parse(commentBody, out var pipeline, out var arguments, out _);

            switch (intentType)
            {
                case CommentIntentType.NewJob:
                    if (pipeline is not null && !AllowedPipelines.Contains(pipeline))
                    {
                        logger.LogInformation(
                            "Ignoring PR comment from {Author} on {Repo}#{Pr}: pipeline={Pipeline} is not allowed",
                            authorLogin, repoFullName, prId, pipeline);
                        return Task.FromResult(new WebhookResult(false, null, null));
                    }

                    var triggerInput = BuildTriggerInput(pipeline!, arguments, repoFullName, prId);
                    logger.LogInformation(
                        "PR comment command from {Author} on {Repo}#{Pr}: pipeline={Pipeline}",
                        authorLogin, repoFullName, prId, pipeline);
                    return Task.FromResult(new WebhookResult(true, triggerInput, pipeline));

                case CommentIntentType.Help:
                    logger.LogInformation(
                        "PR comment help request from {Author} on {Repo}#{Pr}",
                        authorLogin, repoFullName, prId);
                    return Task.FromResult(new WebhookResult(false, null, null));

                case CommentIntentType.DialogueApprove:
                case CommentIntentType.DialogueReject:
                    var answer = intentType == CommentIntentType.DialogueApprove ? "yes" : "no";
                    CommentIntentParser.Parse(commentBody, out _, out _, out var parsedComment);
                    var dialogueData = new DialogueAnswerData(
                        Platform: "azuredevops",
                        RepoFullName: repoFullName,
                        PrIdentifier: prId.ToString(),
                        Answer: answer,
                        Comment: parsedComment,
                        AuthorLogin: authorLogin);
                    logger.LogInformation(
                        "PR comment dialogue {Intent} from {Author} on {Repo}#{Pr}",
                        intentType, authorLogin, repoFullName, prId);
                    return Task.FromResult(new WebhookResult(true, null, null, dialogueData));

                default:
                    return Task.FromResult(new WebhookResult(false, null, null));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps PR comment webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static string BuildTriggerInput(string pipeline, string? arguments, string repoFullName, int prId)
    {
        if (!string.IsNullOrEmpty(arguments))
            return $"{pipeline} {arguments}";

        return $"{pipeline} pr:{repoFullName}#{prId}";
    }
}
