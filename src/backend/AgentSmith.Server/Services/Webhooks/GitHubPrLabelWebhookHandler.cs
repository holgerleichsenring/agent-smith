using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub pull_request labeled events. Triggers the security-scan pipeline when the
/// pull request carries the review-request label — the word the owning project's github_trigger
/// configures, or the historical "security-review" (2026-09-25-d83b: PrTriggerLabelResolver
/// owns the word, which used to be a literal in this file).
/// </summary>
public sealed class GitHubPrLabelWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrTriggerLabelResolver triggerLabels,
    ILogger<GitHubPrLabelWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "pull_request";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.GetProperty("action").GetString() != "labeled")
                return Task.FromResult(new WebhookResult(false, null, null));

            var label = root.GetProperty("label").GetProperty("name").GetString();
            var repoUrl = root.GetProperty("repository").GetProperty("clone_url").GetString() ?? "";
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            if (triggerLabels.Match(config, "github", repoUrl, [label]) is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            var prNumber = root.GetProperty("pull_request").GetProperty("number").GetInt32();
            var repo = root.GetProperty("repository").GetProperty("name").GetString();

            var input = $"security-scan in {repo}";
            logger.LogInformation(
                "GitHub PR #{PrNumber} labeled '{Label}' for security review", prNumber, label);
            return Task.FromResult(new WebhookResult(true, input, "security-scan"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub pull_request webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
