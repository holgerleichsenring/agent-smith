using AgentSmith.Application.Services.Specs;
using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
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
    ApprovedRecordProbe approvals,
    ILogger<AzureDevOpsWorkItemWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
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
            var (workItemId, fields) = AzureDevOpsWorkItemPayload.Read(resource);
            var state = AzureDevOpsWorkItemPayload.State(fields);
            var ticketUrl = resource.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            var envelope = WebhookEnvelopeBuilders.BuildForAzureDevOpsWorkItem(
                fields, workItemId.ToString(), ticketUrl);

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            // 2026-09-25-3c7aa: a webhook route is per PLATFORM, so the record is looked for on
            // every connection of that type; a ticket whose stamp is gone still binds.
            envelope = envelope with
            {
                HasApprovedRecord = await approvals.ExistsForPlatformAsync(
                    config, envelope.Platform, envelope.TicketId, cancellationToken),
            };
            IReadOnlyList<ProjectMatch> matches = envelopeResolver.Resolve(config, envelope);
            // 2026-10-08-101b: a polling entry's projects get nothing from webhooks, and the list says why.
            if (modeGate?.Refusal(config, matches, modeGate.Webhook(config, matches)) is { } polled) return polled;
            matches = modeGate?.Webhook(config, matches) ?? matches;

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
