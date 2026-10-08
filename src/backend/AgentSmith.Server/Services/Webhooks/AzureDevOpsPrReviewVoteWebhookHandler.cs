using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: an Azure DevOps "Pull request updated" delivery from the subscription filtered to
/// reviewer votes and marked with <see cref="Header"/>. Deliveries without the header are not this
/// handler's. 2026-10-08-0781: a delivery that shows a reviewer other than the creator at "Wait for
/// author" (-5) nudges the ticket; the worker reads the standing vote threads from the host.
/// </summary>
public sealed class AzureDevOpsPrReviewVoteWebhookHandler(
    PrReworkAdmission admission, ILogger<AzureDevOpsPrReviewVoteWebhookHandler> logger) : IWebhookHandler
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
            var creator = PayloadActTime.Text(pr, "createdBy", "id");
            var voter = AzureDevOpsPrUrl.VotersAt(pr, -5).FirstOrDefault(v => !string.Equals(v, creator, StringComparison.OrdinalIgnoreCase));
            if (voter is null) return WebhookResult.NotHandled("no Wait-for-author vote in this delivery");
            return await admission.AdmitAsync(new PrReviewRequest(RepoType.AzureDevOps, repoUrl,
                PayloadActTime.Text(pr, "sourceRefName"), !pr.TryGetProperty("forkSource", out _),
                new PrCommentAuthor(repoUrl, PayloadActTime.Text(pr, "repository", "id") ?? string.Empty, voter, voter),
                creator, AzureDevOpsPrUrl.Of(repoUrl, pr)), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the Azure DevOps review-vote delivery");
            return WebhookResult.NotHandled();
        }
    }
}
