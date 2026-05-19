using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Note Hook events on Merge Requests. p0146e: pipeline + ticket
/// resolution is delegated to <see cref="CommentIntentParser"/> + IIntentParser.
/// </summary>
public sealed class GitLabMrCommentWebhookHandler(
    CommentIntentParser commentIntentParser,
    ServerContext serverContext,
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

    public async Task<WebhookResult> HandleAsync(
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
                return WebhookResult.NotHandled();

            var commentBody = attrs.GetProperty("note").GetString() ?? "";
            var commentId = attrs.GetProperty("id").GetInt64().ToString();
            var authorLogin = root.GetProperty("user").GetProperty("username").GetString() ?? "";
            var repoFullName = root.GetProperty("project")
                .GetProperty("path_with_namespace").GetString() ?? "";

            var mrIid = root.GetProperty("merge_request")
                .GetProperty("iid").GetInt32();

            var parsed = await commentIntentParser.ParseAsync(
                commentBody, serverContext.ConfigPath, cancellationToken);

            switch (parsed.Type)
            {
                case CommentIntentType.NewJob:
                    var pipeline = parsed.Request!.PipelineName;
                    if (!AllowedPipelines.Contains(pipeline))
                    {
                        logger.LogInformation(
                            "Ignoring MR comment from {Author} on {Repo}!{Mr}: pipeline={Pipeline} is not allowed",
                            authorLogin, repoFullName, mrIid, pipeline);
                        return WebhookResult.NotHandled();
                    }

                    var triggerInput = BuildTriggerInput(parsed.Request, repoFullName, mrIid);
                    logger.LogInformation(
                        "MR comment command from {Author} on {Repo}!{Mr}: pipeline={Pipeline}",
                        authorLogin, repoFullName, mrIid, pipeline);
                    return new WebhookResult(true, triggerInput, pipeline);

                case CommentIntentType.Help:
                    logger.LogInformation(
                        "MR comment help request from {Author} on {Repo}!{Mr}",
                        authorLogin, repoFullName, mrIid);
                    return WebhookResult.NotHandled();

                case CommentIntentType.DialogueApprove:
                case CommentIntentType.DialogueReject:
                    var answer = parsed.Type == CommentIntentType.DialogueApprove ? "yes" : "no";
                    var dialogueData = new DialogueAnswerData(
                        Platform: "gitlab",
                        RepoFullName: repoFullName,
                        PrIdentifier: mrIid.ToString(),
                        Answer: answer,
                        Comment: parsed.DialogueComment,
                        AuthorLogin: authorLogin);
                    logger.LogInformation(
                        "MR comment dialogue {Intent} from {Author} on {Repo}!{Mr}",
                        parsed.Type, authorLogin, repoFullName, mrIid);
                    return new WebhookResult(true, null, null, dialogueData);

                default:
                    return WebhookResult.NotHandled();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab MR comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static string BuildTriggerInput(PipelineRequest request, string repoFullName, int mrIid)
    {
        var ticketSegment = request.TicketId is not null ? $" #{request.TicketId.Value}" : "";
        return $"{request.PipelineName}{ticketSegment} mr:{repoFullName}!{mrIid}";
    }
}
