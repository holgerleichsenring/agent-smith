using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// An Azure DevOps pull request completed or abandoned ("Pull request updated", status change) or a
/// merge attempted ("Pull request merge attempted"). It starts nothing: it asks only for the pull
/// request's record to be read again. A delivery of the review-vote subscription is the vote
/// handler's; an update to an active pull request is not this handler's.
/// </summary>
public sealed class AzureDevOpsPrStateWebhookHandler(
    PrRecordNudge records, ILogger<AzureDevOpsPrStateWebhookHandler> logger, TriggerModeGate? modeGate = null) : IWebhookHandler
{
    private const string Updated = "git.pullrequest.updated";
    private const string Merged = "git.pullrequest.merged";
    private static readonly HashSet<string> EndedStatuses = new(StringComparer.OrdinalIgnoreCase) { "completed", "abandoned" };

    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && (string.Equals(eventType, Updated, StringComparison.OrdinalIgnoreCase)
            || string.Equals(eventType, Merged, StringComparison.OrdinalIgnoreCase));

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (AzureDevOpsPrReviewVoteWebhookHandler.IsVoteDelivery(headers)) return WebhookResult.NotHandled();
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var pr = doc.RootElement.GetProperty("resource");
            if (!IsStateChange(PayloadActTime.Text(doc.RootElement, "eventType"), pr))
                return WebhookResult.NotHandled("not a completion, abandonment or merge");
            var repoUrl = AzureDevOpsPrUrl.RepositoryUrl(pr);
            if (modeGate?.RepoRefusal(repoUrl) is { } polled) return polled;
            var delivery = new PrRecordDelivery(repoUrl, PayloadActTime.Text(pr, "sourceRefName"),
                !pr.TryGetProperty("forkSource", out _), AzureDevOpsPrUrl.Of(repoUrl, pr));
            return await records.NudgeAsync(delivery, cancellationToken)
                ? WebhookResult.HandledNoRoute()
                : WebhookResult.NotHandled("not a pull request of a ticket");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the Azure DevOps pull-request state delivery");
            return WebhookResult.NotHandled();
        }
    }

    private static bool IsStateChange(string? eventType, JsonElement pr) =>
        string.Equals(eventType, Merged, StringComparison.OrdinalIgnoreCase)
        || EndedStatuses.Contains(PayloadActTime.Text(pr, "status") ?? string.Empty);
}
