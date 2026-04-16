using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles GitLab Issue Hook events. Triggers a pipeline based on
/// config-driven label matching and issue status gating.
/// </summary>
public sealed class GitLabIssueWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<GitLabIssueWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "Issue Hook";

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
            if (action is not "update" and not "open")
                return Task.FromResult(new WebhookResult(false, null, null));

            var issueState = attrs.GetProperty("state").GetString() ?? "";
            var issueId = attrs.GetProperty("iid").GetInt32();

            var repoUrl = root.TryGetProperty("project", out var proj)
                ? proj.GetProperty("web_url").GetString() ?? ""
                : "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = FindProject(config, repoUrl);

            if (triggerConfig is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            if (!IsStatusAllowed(triggerConfig, issueState))
            {
                logger.LogDebug("GitLab issue !{Issue} state '{State}' not in trigger_statuses", issueId, issueState);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var labels = ExtractLabels(root);
            var pipeline = ResolvePipeline(triggerConfig, labels);
            if (pipeline is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            logger.LogInformation("GitLab issue !{Issue} triggered pipeline '{Pipeline}'", issueId, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: issueId.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab Issue Hook webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    internal static (string? ProjectName, WebhookTriggerConfig? Config) FindProject(
        AgentSmithConfig config, string repoUrl)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (repoUrl.Equals(project.Source.Url, StringComparison.OrdinalIgnoreCase))
                return (name, project.GitlabTrigger);
        }
        return (null, null);
    }

    internal static bool IsStatusAllowed(WebhookTriggerConfig trigger, string status) =>
        trigger.TriggerStatuses.Count == 0
        || trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    internal static List<string> ExtractLabels(JsonElement root)
    {
        var labels = new List<string>();
        if (root.TryGetProperty("labels", out var labelsEl))
        {
            foreach (var label in labelsEl.EnumerateArray())
            {
                var title = label.GetProperty("title").GetString();
                if (title is not null) labels.Add(title);
            }
        }
        return labels;
    }

    internal static string? ResolvePipeline(WebhookTriggerConfig trigger, List<string> labels)
    {
        foreach (var (configLabel, pipeline) in trigger.PipelineFromLabel)
        {
            if (labels.Contains(configLabel, StringComparer.OrdinalIgnoreCase))
                return pipeline;
        }
        return trigger.PipelineFromLabel.Count == 0 ? trigger.DefaultPipeline : null;
    }
}
