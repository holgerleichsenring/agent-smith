using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: a submitted GitHub review whose state is changes_requested is a rework act on
/// the pull request; every other review, and every comment, starts nothing.
/// </summary>
public sealed class GitHubPrReviewWebhookHandler(
    PrReworkAdmission admission, ILogger<GitHubPrReviewWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "pull_request_review";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var review = root.GetProperty("review");
            if (PayloadActTime.Text(root, "action") != "submitted"
                || !string.Equals(PayloadActTime.Text(review, "state"), "changes_requested", StringComparison.OrdinalIgnoreCase))
                return WebhookResult.NotHandled("not a submitted request for changes");
            return await admission.AdmitAsync(Read(root, review), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub pull_request_review webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static PrReviewRequest Read(JsonElement root, JsonElement review)
    {
        var pr = root.GetProperty("pull_request");
        var repository = root.GetProperty("repository");
        var fullName = PayloadActTime.Text(repository, "full_name") ?? string.Empty;
        var login = PayloadActTime.Text(review, "user", "login") ?? string.Empty;
        var reviewer = new PrCommentAuthor(PayloadActTime.Text(repository, "html_url") ?? string.Empty, fullName, login, login)
        {
            Association = PayloadActTime.Text(review, "author_association"),
        };
        var act = PayloadActTime.Act(login, PayloadActTime.Text(review, "submitted_at"))
            ?? throw new InvalidOperationException("the review carries no submitted_at");
        return new PrReviewRequest(RepoType.GitHub, PayloadActTime.Text(repository, "clone_url") ?? string.Empty,
            PayloadActTime.Text(pr, "head", "ref"),
            string.Equals(PayloadActTime.Text(pr, "head", "repo", "full_name"), fullName, StringComparison.OrdinalIgnoreCase),
            reviewer, PayloadActTime.Text(pr, "user", "login"), pr.GetProperty("number").GetInt32().ToString(), act);
    }
}
