using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles Jira issue_updated webhooks. Triggers a pipeline when an issue
/// is assigned to the configured Agent Smith user, the issue status is in the
/// configured whitelist, and a matching label determines the pipeline.
/// </summary>
public sealed class JiraAssigneeWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<JiraAssigneeWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "jira" && eventType == "issue_updated";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var changelogItem = FindAssigneeChangelogItem(root);
            if (changelogItem is null)
                return Task.FromResult(new WebhookResult(false, null, null));

            var newAssignee = changelogItem.Value
                .GetProperty("toString").GetString() ?? string.Empty;

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = FindMatchingProject(config, newAssignee);

            if (triggerConfig is null)
            {
                logger.LogDebug(
                    "No jira_trigger configured for assignee '{Assignee}'", newAssignee);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var issueStatus = ExtractIssueStatus(root);
            if (!IsStatusAllowed(triggerConfig, issueStatus))
            {
                logger.LogDebug(
                    "Issue status '{Status}' not in trigger_statuses, ignoring", issueStatus);
                return Task.FromResult(new WebhookResult(false, null, null));
            }

            var issueKey = root.GetProperty("issue").GetProperty("key").GetString()!;
            var labels = ExtractLabels(root);
            var pipeline = ResolvePipeline(triggerConfig, labels);

            logger.LogInformation(
                "Jira trigger: issue {IssueKey} assigned to '{Assignee}' -> pipeline '{Pipeline}'",
                issueKey, newAssignee, pipeline);

            var initialContext = new Dictionary<string, object>
            {
                [ContextKeys.DoneStatus] = triggerConfig.DoneStatus
            };

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: issueKey));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Jira assignee webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static JsonElement? FindAssigneeChangelogItem(JsonElement root)
    {
        if (!root.TryGetProperty("changelog", out var changelog)) return null;
        if (!changelog.TryGetProperty("items", out var items)) return null;

        foreach (var item in items.EnumerateArray())
        {
            if (item.TryGetProperty("field", out var field) &&
                field.GetString() == "assignee")
                return item;
        }

        return null;
    }

    private static (string ProjectName, JiraTriggerConfig? Config) FindMatchingProject(
        AgentSmithConfig config, string newAssignee)
    {
        foreach (var (name, project) in config.Projects)
        {
            var trigger = project.JiraTrigger;
            if (trigger is not null &&
                string.Equals(trigger.AssigneeName, newAssignee,
                    StringComparison.OrdinalIgnoreCase))
                return (name, trigger);
        }

        return (string.Empty, null);
    }

    internal static string ExtractIssueStatus(JsonElement root)
    {
        if (root.TryGetProperty("issue", out var issue)
            && issue.TryGetProperty("fields", out var fields)
            && fields.TryGetProperty("status", out var status)
            && status.TryGetProperty("name", out var name))
        {
            return name.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    internal static bool IsStatusAllowed(JiraTriggerConfig trigger, string issueStatus) =>
        trigger.TriggerStatuses.Contains(issueStatus, StringComparer.OrdinalIgnoreCase);

    internal static List<string> ExtractLabels(JsonElement root)
    {
        var labels = new List<string>();

        if (!root.TryGetProperty("issue", out var issue)) return labels;
        if (!issue.TryGetProperty("fields", out var fields)) return labels;
        if (!fields.TryGetProperty("labels", out var labelsEl)) return labels;

        foreach (var label in labelsEl.EnumerateArray())
        {
            var value = label.GetString();
            if (value is not null) labels.Add(value);
        }

        return labels;
    }

    internal static string ResolvePipeline(JiraTriggerConfig trigger, List<string> labels)
    {
        foreach (var (configLabel, pipeline) in trigger.PipelineFromLabel)
        {
            if (labels.Contains(configLabel, StringComparer.OrdinalIgnoreCase))
                return pipeline;
        }

        return trigger.DefaultPipeline;
    }
}
