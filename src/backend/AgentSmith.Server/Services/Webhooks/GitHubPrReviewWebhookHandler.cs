using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: a submitted GitHub review whose state is changes_requested is a rework act on
/// the pull request; every other review, and every comment, starts nothing. 2026-10-08-0781: the
/// delivery only nudges the ticket; the worker reads the review from GitHub. Every review delivery
/// that is not an admitted request for changes — approved, commented, edited, dismissed — asks only
/// for the pull request's record to be read again.
/// </summary>
public sealed class GitHubPrReviewWebhookHandler(
    PrReworkAdmission admission, PrRecordNudge records, ILogger<GitHubPrReviewWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
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
            var request = Read(root, review);
            // 2026-10-08-101b: a polling project's repository gets nothing from webhooks.
            if (modeGate?.RepoRefusal(request.RepoUrl) is { } polled) return polled;
            var result = IsChangeRequest(root, review)
                ? await admission.AdmitAsync(request, cancellationToken)
                : WebhookResult.NotHandled("not a submitted request for changes");
            return result.Handled || !await records.NudgeAsync(PrRecordDelivery.Of(request), cancellationToken)
                ? result : WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub pull_request_review webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static bool IsChangeRequest(JsonElement root, JsonElement review) =>
        PayloadActTime.Text(root, "action") == "submitted"
        && string.Equals(PayloadActTime.Text(review, "state"), "changes_requested", StringComparison.OrdinalIgnoreCase);

    private static PrReviewRequest Read(JsonElement root, JsonElement review)
    {
        var pr = root.GetProperty("pull_request");
        var repository = root.GetProperty("repository");
        var fullName = PayloadActTime.Text(repository, "full_name") ?? string.Empty;
        var login = PayloadActTime.Text(review, "user", "login") ?? string.Empty;
        var reviewer = new PrCommentAuthor(PayloadActTime.Text(repository, "html_url") ?? string.Empty, fullName, login, login);
        return new PrReviewRequest(RepoType.GitHub, PayloadActTime.Text(repository, "clone_url") ?? string.Empty,
            PayloadActTime.Text(pr, "head", "ref"),
            string.Equals(PayloadActTime.Text(pr, "head", "repo", "full_name"), fullName, StringComparison.OrdinalIgnoreCase),
            reviewer, PayloadActTime.Text(pr, "user", "login"), PayloadActTime.Text(pr, "html_url") ?? string.Empty);
    }
}
