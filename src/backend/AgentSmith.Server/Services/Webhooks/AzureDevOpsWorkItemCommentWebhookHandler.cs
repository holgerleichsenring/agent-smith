using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using System.Text.Json;
using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles Azure DevOps workitem.commented events for re-triggering pipelines. Filters
/// on comment_keyword / PlanAnswers presence and the state gate before dispatching.
/// </summary>
public sealed class AzureDevOpsWorkItemCommentWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    KeywordCommentRouter router,
    ApprovedRecordProbe approvals,
    PlanAnswerParser planAnswerParser,
    ILogger<AzureDevOpsWorkItemCommentWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "azuredevops" && eventType == "workitem.commented";

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
            var commentText = AzureDevOpsWorkItemPayload.CommentText(fields);
            if (OwnTicketComment.IsOurs(commentText))
                return WebhookResult.NotHandled("the comment is agent-smith's own");
            var state = AzureDevOpsWorkItemPayload.State(fields);
            var ticketUrl = resource.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            var planAnswers = planAnswerParser.Parse(commentText);
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
            var act = PayloadActTime.Act(
                PayloadActTime.Text(fields, "System.ChangedBy"), PayloadActTime.Text(fields, "System.ChangedDate"));
            return await router.RouteAsync(config, matches, new KeywordComment(envelope, state, commentText,
                planAnswers.Count > 0 ? new Dictionary<string, string>(planAnswers) : null, act), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse Azure DevOps workitem.commented webhook");
            return WebhookResult.NotHandled();
        }
    }
}
