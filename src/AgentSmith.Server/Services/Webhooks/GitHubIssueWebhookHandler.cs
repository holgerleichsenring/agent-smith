using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub Issues labeled events. Triggers a pipeline based on
/// config-driven label matching and issue status gating.
/// Falls back to "agent-smith" label for backward compatibility.
/// </summary>
public sealed class GitHubIssueWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<GitHubIssueWebhookHandler> logger) : IWebhookHandler
{
    private const string DefaultTriggerLabel = "agent-smith";

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

            var label = root.GetProperty("label").GetProperty("name").GetString() ?? "";
            var issueEl = root.GetProperty("issue");
            var issueState = issueEl.TryGetProperty("state", out var stateEl)
                ? stateEl.GetString() ?? "open" : "open";
            var issue = issueEl.GetProperty("number").GetInt32();
            var repoUrl = root.GetProperty("repository").GetProperty("html_url").GetString() ?? "";

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = FindProject(config, repoUrl);

            string? pipeline;
            if (triggerConfig is not null)
            {
                if (!IsStatusAllowed(triggerConfig, issueState))
                {
                    logger.LogDebug("GitHub issue #{Issue} state '{State}' not in trigger_statuses", issue, issueState);
                    return Task.FromResult(new WebhookResult(false, null, null));
                }

                pipeline = ResolvePipeline(triggerConfig, label);
                if (pipeline is null)
                {
                    logger.LogDebug("Label '{Label}' not in github_trigger config", label);
                    return Task.FromResult(new WebhookResult(false, null, null));
                }
            }
            else
            {
                // Backward compat: trigger on "agent-smith" label without config
                if (!DefaultTriggerLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new WebhookResult(false, null, null));
                pipeline = null;
            }

            logger.LogInformation("GitHub issue #{Issue} labeled '{Label}', project '{Project}', pipeline '{Pipeline}'",
                issue, label, projectName, pipeline ?? "(default)");

            var initialContext = triggerConfig is not null
                ? new Dictionary<string, object> { [ContextKeys.DoneStatus] = triggerConfig.DoneStatus }
                : null;

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: issue.ToString(),
                Platform: "GitHub"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub issues webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    internal static (string? ProjectName, WebhookTriggerConfig? Config) FindProject(
        AgentSmithConfig config, string repoUrl)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (repoUrl.Equals(project.Source.Url, StringComparison.OrdinalIgnoreCase))
                return (name, project.GithubTrigger);
        }

        return (null, null);
    }

    internal static bool IsStatusAllowed(WebhookTriggerConfig trigger, string status) =>
        trigger.TriggerStatuses.Count == 0
        || trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);

    internal static string? ResolvePipeline(WebhookTriggerConfig trigger, string label)
    {
        foreach (var (configLabel, pipeline) in trigger.PipelineFromLabel)
        {
            if (configLabel.Equals(label, StringComparison.OrdinalIgnoreCase))
                return pipeline;
        }

        // If no pipeline_from_label entries match, check if this is ANY configured label
        // If pipeline_from_label is empty, accept any label and use default pipeline
        return trigger.PipelineFromLabel.Count == 0 ? trigger.DefaultPipeline : null;
    }
}
