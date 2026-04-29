using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.updated events. Triggers a pipeline based on
/// config-driven tag matching and work item state gating.
/// Falls back to "security-review" tag for backward compatibility.
/// </summary>
public sealed class AzureDevOpsWorkItemWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<AzureDevOpsWorkItemWebhookHandler> logger) : IWebhookHandler
{
    private const string DefaultTriggerTag = "security-review";

    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && eventType == "workitem.updated";

    public Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var resource = root.GetProperty("resource");
            var fields = resource.GetProperty("fields");

            if (!fields.TryGetProperty("System.Tags", out var tagsElement))
                return Task.FromResult(new WebhookResult(false, null, null));

            var tags = tagsElement.GetString() ?? "";
            var workItemId = resource.GetProperty("id").GetInt32();

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var (projectName, triggerConfig) = FindProject(config);

            string? pipeline;
            if (triggerConfig is not null)
            {
                var state = fields.TryGetProperty("System.State", out var stateEl)
                    ? stateEl.GetString() ?? ""
                    : "";

                if (!IsStatusAllowed(triggerConfig, state))
                {
                    logger.LogDebug("Work item #{Id} state '{State}' not in trigger_statuses", workItemId, state);
                    return Task.FromResult(new WebhookResult(false, null, null));
                }

                pipeline = ResolvePipelineFromTags(triggerConfig, tags);
                if (pipeline is null)
                {
                    logger.LogDebug("No matching tag found in azuredevops_trigger config");
                    return Task.FromResult(new WebhookResult(false, null, null));
                }
            }
            else
            {
                // Backward compat: trigger on "security-review" tag
                if (!tags.Contains(DefaultTriggerTag, StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new WebhookResult(false, null, null));
                pipeline = "security-scan";
            }

            logger.LogInformation("Azure DevOps work item #{Id} triggered pipeline '{Pipeline}'", workItemId, pipeline);

            var initialContext = triggerConfig is not null
                ? new Dictionary<string, object> { [ContextKeys.DoneStatus] = triggerConfig.DoneStatus }
                : null;

            return Task.FromResult(new WebhookResult(
                true, null, pipeline,
                InitialContext: initialContext,
                ProjectName: projectName,
                TicketId: workItemId.ToString(),
                Platform: "AzureDevOps"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.updated webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    internal static (string? ProjectName, WebhookTriggerConfig? Config) FindProject(AgentSmithConfig config)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (project.AzuredevopsTrigger is not null)
                return (name, project.AzuredevopsTrigger);
            // Backward compat: match by ticket type
            if ("AzureDevOps".Equals(project.Tickets.Type, StringComparison.OrdinalIgnoreCase))
                return (name, null);
        }
        return (null, null);
    }

    internal static bool IsStatusAllowed(WebhookTriggerConfig trigger, string state) =>
        trigger.TriggerStatuses.Count == 0
        || trigger.TriggerStatuses.Contains(state, StringComparer.OrdinalIgnoreCase);

    internal static string? ResolvePipelineFromTags(WebhookTriggerConfig trigger, string tags)
    {
        foreach (var (configTag, pipeline) in trigger.PipelineFromLabel)
        {
            if (tags.Contains(configTag, StringComparison.OrdinalIgnoreCase))
                return pipeline;
        }
        return trigger.PipelineFromLabel.Count == 0 ? trigger.DefaultPipeline : null;
    }
}
