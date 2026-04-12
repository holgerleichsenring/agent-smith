using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.updated events. Triggers security-scan pipeline
/// when "security-review" tag is added to a work item.
/// </summary>
public sealed class AzureDevOpsWorkItemWebhookHandler(
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
            var projectName = root.GetProperty("resourceContainers")
                .GetProperty("project").GetProperty("id").GetString() ?? "unknown";

            var input = $"security-scan in {projectName}";
            logger.LogInformation("Azure DevOps work item #{WorkItemId} tagged for security review", workItemId);
            return Task.FromResult(new WebhookResult(true, input, "security-scan"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.updated webhook");
            return Task.FromResult(new WebhookResult(false, null, null));
        }
    }
}
