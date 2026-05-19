using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps Pull Request comment events. p0146e: pipeline + ticket
/// resolution is delegated to <see cref="CommentIntentParser"/> + IIntentParser.
/// </summary>
public sealed class AzureDevOpsPrCommentWebhookHandler(
    CommentIntentParser commentIntentParser,
    ServerContext serverContext,
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

    public async Task<WebhookResult> HandleAsync(
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

            var parsed = await commentIntentParser.ParseAsync(
                commentBody, serverContext.ConfigPath, cancellationToken);

            switch (parsed.Type)
            {
                case CommentIntentType.NewJob:
                    var pipeline = parsed.Request!.PipelineName;
                    if (!AllowedPipelines.Contains(pipeline))
                    {
                        logger.LogInformation(
                            "Ignoring PR comment from {Author} on {Repo}#{Pr}: pipeline={Pipeline} is not allowed",
                            authorLogin, repoFullName, prId, pipeline);
                        return WebhookResult.NotHandled();
                    }

                    var triggerInput = BuildTriggerInput(parsed.Request, repoFullName, prId);
                    logger.LogInformation(
                        "PR comment command from {Author} on {Repo}#{Pr}: pipeline={Pipeline}",
                        authorLogin, repoFullName, prId, pipeline);
                    return new WebhookResult(true, triggerInput, pipeline);

                case CommentIntentType.Help:
                    logger.LogInformation(
                        "PR comment help request from {Author} on {Repo}#{Pr}",
                        authorLogin, repoFullName, prId);
                    return WebhookResult.NotHandled();

                case CommentIntentType.DialogueApprove:
                case CommentIntentType.DialogueReject:
                    var answer = parsed.Type == CommentIntentType.DialogueApprove ? "yes" : "no";
                    var dialogueData = new DialogueAnswerData(
                        Platform: "azuredevops",
                        RepoFullName: repoFullName,
                        PrIdentifier: prId.ToString(),
                        Answer: answer,
                        Comment: parsed.DialogueComment,
                        AuthorLogin: authorLogin);
                    logger.LogInformation(
                        "PR comment dialogue {Intent} from {Author} on {Repo}#{Pr}",
                        parsed.Type, authorLogin, repoFullName, prId);
                    return new WebhookResult(true, null, null, dialogueData);

                default:
                    return WebhookResult.NotHandled();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps PR comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static string BuildTriggerInput(PipelineRequest request, string repoFullName, int prId)
    {
        var ticketSegment = request.TicketId is not null ? $" #{request.TicketId.Value}" : "";
        return $"{request.PipelineName}{ticketSegment} pr:{repoFullName}#{prId}";
    }
}
