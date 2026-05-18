using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.updated events. p0140b: envelope (Labels = tags split,
/// AreaPath = System.AreaPath) + IEnvelopeProjectResolver + WebhookSpawnDispatcher.
/// </summary>
public sealed class AzureDevOpsWorkItemWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    WebhookSpawnDispatcher dispatcher,
    ILogger<AzureDevOpsWorkItemWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && eventType == "workitem.updated";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var resource = root.GetProperty("resource");
            var fields = resource.GetProperty("fields");
            var workItemId = resource.GetProperty("id").GetInt32();
            var state = fields.TryGetProperty("System.State", out var stateEl)
                ? stateEl.GetString() ?? "" : "";
            var ticketUrl = resource.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            var envelope = WebhookEnvelopeBuilders.BuildForAzureDevOpsWorkItem(
                fields, workItemId.ToString(), ticketUrl);

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var matches = envelopeResolver.Resolve(config, envelope);

            logger.LogInformation(
                "ADO work item #{Id} → resolved matches={Count}", workItemId, matches.Count);

            await dispatcher.DispatchAsync(config, matches, envelope, state, null, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.updated webhook");
            return WebhookResult.NotHandled();
        }
    }
}
