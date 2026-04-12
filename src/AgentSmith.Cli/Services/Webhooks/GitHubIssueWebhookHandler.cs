using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles GitHub Issues labeled events. Triggers the default pipeline
/// when the "agent-smith" label is added to an issue.
/// </summary>
public sealed class GitHubIssueWebhookHandler(
    ILogger<GitHubIssueWebhookHandler> logger) : IWebhookHandler
{
    private const string TriggerLabel = "agent-smith";

    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "issues";

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

            var issue = root.GetProperty("issue").GetProperty("number").GetInt32();
            var repo = root.GetProperty("repository").GetProperty("name").GetString();

            var input = $"fix #{issue} in {repo}";
            logger.LogInformation("GitHub issue labeled: {Input}", input);
            return Task.FromResult(new WebhookResult(true, input, null));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub issues webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
