using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Host.Services.Webhooks;

/// <summary>
/// Handles GitHub pull_request labeled events. Triggers the security-scan
/// pipeline when "security-review" label is added to a PR.
/// </summary>
public sealed class GitHubPrLabelWebhookHandler(
    ILogger<GitHubPrLabelWebhookHandler> logger) : IWebhookHandler
{
    private const string TriggerLabel = "security-review";

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
            if (!TriggerLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookResult(false, null, null));

            var prNumber = root.GetProperty("pull_request").GetProperty("number").GetInt32();
            var repo = root.GetProperty("repository").GetProperty("name").GetString();
            var repoUrl = root.GetProperty("repository").GetProperty("clone_url").GetString() ?? "";

            var input = $"security-scan in {repo}";
            logger.LogInformation("GitHub PR #{PrNumber} labeled for security review", prNumber);
            return Task.FromResult(new WebhookResult(true, input, "security-scan"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub pull_request webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
