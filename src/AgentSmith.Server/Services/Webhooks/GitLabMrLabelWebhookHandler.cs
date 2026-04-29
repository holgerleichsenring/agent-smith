using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Merge Request Hook events. Triggers security-scan pipeline
/// when "security-review" label is added to a merge request.
/// </summary>
public sealed class GitLabMrLabelWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<GitLabMrLabelWebhookHandler> logger) : IWebhookHandler
{
    private const string TriggerLabel = "security-review";

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

            var labels = root.GetProperty("labels");
            var hasLabel = labels.EnumerateArray()
                .Any(l => TriggerLabel.Equals(l.GetProperty("title").GetString(),
                    StringComparison.OrdinalIgnoreCase));

            if (!hasLabel)
                return Task.FromResult(new WebhookResult(false, null, null));

            var mrIid = attrs.GetProperty("iid").GetInt32();
            var repoUrl = root.GetProperty("project").GetProperty("web_url").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var projectName = FindProjectBySourceUrl(config, repoUrl);

            logger.LogInformation("GitLab MR !{MrIid} labeled for security review, project '{Project}'", mrIid, projectName);
            return Task.FromResult(new WebhookResult(
                true, null, "security-scan",
                ProjectName: projectName,
                TicketId: mrIid.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab merge_request webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static string? FindProjectBySourceUrl(AgentSmithConfig config, string repoUrl)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (project.Source.Url is not null
                && repoUrl.Contains(project.Source.Url, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }
}
