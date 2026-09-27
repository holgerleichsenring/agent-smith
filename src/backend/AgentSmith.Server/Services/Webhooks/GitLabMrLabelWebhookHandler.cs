using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Merge Request Hook events. Triggers the security-scan pipeline when the merge
/// request carries the review-request label — the word the owning project's gitlab_trigger
/// configures, or the historical "security-review" (2026-09-25-d83b: PrTriggerLabelResolver owns
/// the word and the project match, both of which used to live in this file).
/// </summary>
public sealed class GitLabMrLabelWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    PrTriggerLabelResolver triggerLabels,
    ILogger<GitLabMrLabelWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "merge_request";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var attrs = root.GetProperty("object_attributes");
            var action = attrs.GetProperty("action").GetString();
            if (action != "update")
                return Task.FromResult(new WebhookResult(false, null, null));

            var labels = root.GetProperty("labels").EnumerateArray()
                .Select(l => l.GetProperty("title").GetString());
            var repoUrl = root.GetProperty("project").GetProperty("web_url").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            if (triggerLabels.Match(config, "gitlab", repoUrl, labels) is not { } match)
                return Task.FromResult(new WebhookResult(false, null, null));

            var mrIid = attrs.GetProperty("iid").GetInt32();
            logger.LogInformation(
                "GitLab MR !{MrIid} labeled for security review, project '{Project}'",
                mrIid, match.ProjectName);
            return Task.FromResult(new WebhookResult(
                true, null, "security-scan",
                ProjectName: match.ProjectName,
                TicketId: mrIid.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab merge_request webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
