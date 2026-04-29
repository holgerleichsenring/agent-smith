using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Note Hook events on Merge Requests.
/// Parses agent commands and triggers the appropriate pipeline.
/// </summary>
public sealed class GitLabMrCommentWebhookHandler(
    ILogger<GitLabMrCommentWebhookHandler> logger) : IWebhookHandler
{
    private static readonly HashSet<string> AllowedPipelines = new(StringComparer.OrdinalIgnoreCase)
    {
        "fix-bug",
        "security-scan",
        "pr-review",
    };

    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "note hook";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var attrs = root.GetProperty("object_attributes");

            // Only handle notes on merge requests
            var noteableType = attrs.GetProperty("noteable_type").GetString() ?? "";
            if (!noteableType.Equals("MergeRequest", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookResult(false, null, null));

            var commentBody = attrs.GetProperty("note").GetString() ?? "";
            var commentId = attrs.GetProperty("id").GetInt64().ToString();
            var authorLogin = root.GetProperty("user").GetProperty("username").GetString() ?? "";
            var repoFullName = root.GetProperty("project")
                .GetProperty("path_with_namespace").GetString() ?? "";

            var mrIid = root.GetProperty("merge_request")
                .GetProperty("iid").GetInt32();

            var intentType = CommentIntentParser.Parse(commentBody, out var pipeline, out var arguments, out _);

            switch (intentType)
            {
                case CommentIntentType.NewJob:
                    if (pipeline is not null && !AllowedPipelines.Contains(pipeline))
                    {
                        logger.LogInformation(
                            "Ignoring MR comment from {Author} on {Repo}!{Mr}: pipeline={Pipeline} is not allowed",
                            authorLogin, repoFullName, mrIid, pipeline);
                        return Task.FromResult(new WebhookResult(false, null, null));
                    }

                    var triggerInput = BuildTriggerInput(pipeline!, arguments, repoFullName, mrIid);
                    logger.LogInformation(
                        "MR comment command from {Author} on {Repo}!{Mr}: pipeline={Pipeline}",
                        authorLogin, repoFullName, mrIid, pipeline);
                    return Task.FromResult(new WebhookResult(true, triggerInput, pipeline));

                case CommentIntentType.Help:
                    logger.LogInformation(
                        "MR comment help request from {Author} on {Repo}!{Mr}",
                        authorLogin, repoFullName, mrIid);
                    return Task.FromResult(new WebhookResult(false, null, null));

                case CommentIntentType.DialogueApprove:
                case CommentIntentType.DialogueReject:
                    var answer = intentType == CommentIntentType.DialogueApprove ? "yes" : "no";
                    CommentIntentParser.Parse(commentBody, out _, out _, out var parsedComment);
                    var dialogueData = new DialogueAnswerData(
                        Platform: "gitlab",
                        RepoFullName: repoFullName,
                        PrIdentifier: mrIid.ToString(),
                        Answer: answer,
                        Comment: parsedComment,
                        AuthorLogin: authorLogin);
                    logger.LogInformation(
                        "MR comment dialogue {Intent} from {Author} on {Repo}!{Mr}",
                        intentType, authorLogin, repoFullName, mrIid);
                    return Task.FromResult(new WebhookResult(true, null, null, dialogueData));

                default:
                    return Task.FromResult(new WebhookResult(false, null, null));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab MR comment webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static string BuildTriggerInput(string pipeline, string? arguments, string repoFullName, int mrIid)
    {
        if (!string.IsNullOrEmpty(arguments))
            return $"{pipeline} {arguments}";

        return $"{pipeline} mr:{repoFullName}!{mrIid}";
    }
}
