using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.updated events. Triggers security-scan pipeline
/// when "security-review" tag is added to a work item.
/// </summary>
public sealed class AzureDevOpsWorkItemWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ILogger<AzureDevOpsWorkItemWebhookHandler> logger) : IWebhookHandler
{
    private const string TriggerTag = "security-review";

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
            if (!tags.Contains(TriggerTag, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(new WebhookResult(false, null, null));

            var workItemId = resource.GetProperty("id").GetInt32();

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var projectName = FindProjectByTicketType(config, "AzureDevOps");

            logger.LogInformation("Azure DevOps work item #{WorkItemId} tagged for security review", workItemId);
            return Task.FromResult(new WebhookResult(
                true, null, "security-scan",
                ProjectName: projectName,
                TicketId: workItemId.ToString()));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.updated webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }

    private static string? FindProjectByTicketType(AgentSmithConfig config, string ticketType)
    {
        foreach (var (name, project) in config.Projects)
        {
            if (ticketType.Equals(project.Tickets.Type, StringComparison.OrdinalIgnoreCase))
                return name;
        }

        return null;
    }
}
