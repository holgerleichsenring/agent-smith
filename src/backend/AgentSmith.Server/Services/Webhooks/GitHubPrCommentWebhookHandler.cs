using System.Text.Json;
using AgentSmith.Application.Webhooks;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub PR comment events (issue_comment on PRs, pull_request_review_comment).
/// Parses agent commands and triggers the appropriate pipeline. p0146e: pipeline + ticket
/// resolution is delegated to <see cref="CommentIntentParser"/> + IIntentParser; this
/// handler only owns the GitHub-payload-shape + trust gate.
/// </summary>
public sealed class GitHubPrCommentWebhookHandler(
    CommentIntentParser commentIntentParser,
    ServerContext serverContext,
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
                return WebhookResult.NotHandled();
            }

            var prNumber = ExtractPrNumber(root);
            if (prNumber is null)
                return WebhookResult.NotHandled();

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
                            authorLogin, repoFullName, prNumber.Value, pipeline);
                        return WebhookResult.NotHandled();
                    }

                    var triggerInput = BuildTriggerInput(parsed.Request, repoFullName, prNumber.Value);
                    logger.LogInformation(
                        "PR comment command from {Author} on {Repo}#{Pr}: pipeline={Pipeline}",
                        authorLogin, repoFullName, prNumber.Value, pipeline);
                    return new WebhookResult(true, triggerInput, pipeline);

                case CommentIntentType.Help:
                    logger.LogInformation(
                        "PR comment help request from {Author} on {Repo}#{Pr}",
                        authorLogin, repoFullName, prNumber.Value);
                    return WebhookResult.NotHandled();

                case CommentIntentType.DialogueApprove:
                case CommentIntentType.DialogueReject:
                    var answer = parsed.Type == CommentIntentType.DialogueApprove ? "yes" : "no";
                    var dialogueData = new DialogueAnswerData(
                        Platform: "github",
                        RepoFullName: repoFullName,
                        PrIdentifier: prNumber.Value.ToString(),
                        Answer: answer,
                        Comment: parsed.DialogueComment,
                        AuthorLogin: authorLogin);
                    logger.LogInformation(
                        "PR comment dialogue {Intent} from {Author} on {Repo}#{Pr}",
                        parsed.Type, authorLogin, repoFullName, prNumber.Value);
                    return new WebhookResult(true, null, null, dialogueData);

                default:
                    return WebhookResult.NotHandled();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub PR comment webhook");
            return WebhookResult.NotHandled();
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

    // p0146e: triggerInput now carries the LLM-resolved pipeline plus any ticket
    // reference + repo/PR coordinates. The downstream legacy path (ExecutePipelineUseCase
    // string overload) re-parses this with the LLM; that's a known cost we accept until
    // the legacy path is retired in favour of structured PipelineRequest dispatch.
    private static string BuildTriggerInput(PipelineRequest request, string repoFullName, int prNumber)
    {
        var ticketSegment = request.TicketId is not null ? $" #{request.TicketId.Value}" : "";
        return $"{request.PipelineName}{ticketSegment} pr:{repoFullName}#{prNumber}";
    }
}
