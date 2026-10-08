using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9c: an Azure DevOps "Pull request updated" delivery from the subscription filtered to
/// reviewer votes and marked with <see cref="Header"/>. The payload names no change kind and shows every
/// standing vote, so the act is the newest -5 vote thread of a voter this delivery still shows at -5,
/// timed by the thread itself. Deliveries without the header are not this handler's.
/// </summary>
public sealed class AzureDevOpsPrReviewVoteWebhookHandler(
    PrReworkAdmission admission,
    IConfiguredRepoFinder repoFinder,
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ISourceProviderFactory sources,
    ILogger<AzureDevOpsPrReviewVoteWebhookHandler> logger) : IWebhookHandler
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
            if (await StandingVoteAsync(repoUrl, pr, cancellationToken) is not { } vote)
                return WebhookResult.NotHandled("no standing Wait-for-author vote in this delivery");
            return await admission.AdmitAsync(new PrReviewRequest(RepoType.AzureDevOps, repoUrl,
                PayloadActTime.Text(pr, "sourceRefName"), !pr.TryGetProperty("forkSource", out _), vote.Author!,
                PayloadActTime.Text(pr, "createdBy", "id"), pr.GetProperty("pullRequestId").GetInt32().ToString(),
                new ReworkAct(vote.Author!.AuthorLogin, vote.At, ReworkChannel.PullRequest)), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the Azure DevOps review-vote delivery");
            return WebhookResult.NotHandled();
        }
    }

    // The newest standing -5 vote by the host's own threads, kept only while THIS delivery still shows
    // that voter at -5 — a reset vote or another reviewer's approval never serves a stale one.
    private async Task<PrReviewNote?> StandingVoteAsync(string repoUrl, JsonElement pr, CancellationToken ct)
    {
        var repo = repoFinder.FindAll(configLoader.LoadConfig(serverContext.ConfigPath), repoUrl).FirstOrDefault();
        if (repo is null || sources.Create(repo.Repo) is not IPrReviewActReader reader) return null;
        var atMinusFive = AzureDevOpsPrUrl.VotersAt(pr, -5);
        var votes = await reader.ChangesRequestedAsync(AzureDevOpsPrUrl.Of(repoUrl, pr), ct);
        return votes.Where(v => v.Author is not null && atMinusFive.Contains(v.Author.AuthorId)).MaxBy(v => v.At);
    }
}
