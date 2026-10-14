using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: an Azure DevOps "Pull request updated" delivery from the subscription filtered to
/// reviewer votes and marked with <see cref="Header"/>. Deliveries without the header are not this
/// handler's. 2026-10-08-0781: a delivery that shows a reviewer at "Wait for author" (-5) nudges the
/// ticket; the worker reads the standing vote threads from the host. A -5 counts whoever cast it, the
/// creator included — agent-smith only ever votes +10 — so the request carries no author. Every
/// other vote delivery — an approval, a reset to 0 that withdraws a decision — asks only for the pull
/// request's record to be read again.
/// </summary>
public sealed class AzureDevOpsPrReviewVoteWebhookHandler(
    PrReworkAdmission admission, PrRecordNudge records, ILogger<AzureDevOpsPrReviewVoteWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
{
    public const string Header = "X-AgentSmith-Change";
    public const string VoteValue = "review-vote";

    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && string.Equals(eventType, "git.pullrequest.updated", StringComparison.OrdinalIgnoreCase);

    public static bool IsVoteDelivery(IDictionary<string, string> headers) =>
        headers.TryGetValue(Header, out var value) && string.Equals(value.Trim(), VoteValue, StringComparison.OrdinalIgnoreCase);

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        if (!IsVoteDelivery(headers)) return WebhookResult.NotHandled();
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var pr = doc.RootElement.GetProperty("resource");
            var repoUrl = AzureDevOpsPrUrl.RepositoryUrl(pr);
            if (modeGate?.RepoRefusal(repoUrl) is { } polled) return polled; // 2026-10-08-101b
            var voter = AzureDevOpsPrUrl.VotersAt(pr, -5).FirstOrDefault();
            var request = new PrReviewRequest(RepoType.AzureDevOps, repoUrl,
                PayloadActTime.Text(pr, "sourceRefName"), !pr.TryGetProperty("forkSource", out _),
                new PrCommentAuthor(repoUrl, PayloadActTime.Text(pr, "repository", "id") ?? string.Empty, voter ?? string.Empty, voter ?? string.Empty),
                null, AzureDevOpsPrUrl.Of(repoUrl, pr));
            var result = voter is null
                ? WebhookResult.NotHandled("no Wait-for-author vote in this delivery")
                : await admission.AdmitAsync(request, cancellationToken);
            return result.Handled || !await records.NudgeAsync(PrRecordDelivery.Of(request), cancellationToken)
                ? result : WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the Azure DevOps review-vote delivery");
            return WebhookResult.NotHandled();
        }
    }
}
