using System.Text.Json;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub PR comment events (issue_comment on PRs, pull_request_review_comment).
/// This handler owns the GitHub payload shape; what the comment starts is decided by
/// <see cref="PrCommentCommandAdmission"/> with GitHub's author trust.
/// </summary>
public sealed class GitHubPrCommentWebhookHandler(
    PrCommentCommandAdmission admission,
    [FromKeyedServices("github")] IPrCommentAuthorTrust authorTrust,
    ILogger<GitHubPrCommentWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
{
    private static readonly HashSet<string> SupportedEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "issue_comment",
        "pull_request_review_comment",
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

            var prNumber = ExtractPrNumber(root);
            if (prNumber is null)
                return WebhookResult.NotHandled();

            var command = ReadCommand(root, prNumber.Value);
            // 2026-10-08-101b: before the model — a polling project's repository gets nothing from webhooks.
            if (modeGate?.RepoRefusal(command.Author.RepositoryUrl) is { } polled) return polled;
            return await admission.AdmitAsync(command, authorTrust, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub PR comment webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static PrCommentCommand ReadCommand(JsonElement root, int prNumber)
    {
        var comment = root.GetProperty("comment");
        var repository = root.GetProperty("repository");
        var repoFullName = repository.GetProperty("full_name").GetString() ?? "";
        var login = comment.GetProperty("user").GetProperty("login").GetString() ?? "";
        var author = new PrCommentAuthor(
            RepositoryUrl: repository.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "",
            RepositoryId: repoFullName,
            AuthorId: login,
            AuthorLogin: login)
        {
            Association = comment.TryGetProperty("author_association", out var assoc) ? assoc.GetString() : null,
        };
        return new PrCommentCommand(
            comment.GetProperty("body").GetString() ?? "", author,
            $"{repoFullName}#{prNumber}", $"pr:{repoFullName}#{prNumber}", prNumber.ToString());
    }

    private static int? ExtractPrNumber(JsonElement root)
    {
        // pull_request_review_comment events have a top-level "pull_request" object
        if (root.TryGetProperty("pull_request", out var pr))
            return pr.GetProperty("number").GetInt32();

        // issue_comment events: only handle if the issue has a "pull_request" property
        if (root.TryGetProperty("issue", out var issue) && issue.TryGetProperty("pull_request", out _))
            return issue.GetProperty("number").GetInt32();

        return null;
    }
}
