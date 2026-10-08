using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitLab Issue Hook events. p0140b: envelope + IEnvelopeProjectResolver +
/// WebhookSpawnDispatcher path.
/// </summary>
public sealed class GitLabIssueWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    WebhookSpawnDispatcher dispatcher,
    ApprovedRecordProbe approvals,
    ILogger<GitLabIssueWebhookHandler> logger,
    StatusBackGate? statusBack = null) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "issue hook";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var attrs = root.GetProperty("object_attributes");
            var action = attrs.GetProperty("action").GetString();
            // 2026-10-08-2123: a reopen is a person's move back of a finished ticket, or nothing.
            if (action is not "update" and not "open" && (action != "reopen" || statusBack is null))
                return WebhookResult.NotHandled();

            var issueState = attrs.GetProperty("state").GetString() ?? "";
            var issueId = attrs.GetProperty("iid").GetInt32();
            var ticketUrl = attrs.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            var repoUrl = root.TryGetProperty("project", out var proj)
                ? proj.GetProperty("web_url").GetString() ?? "" : "";

            var envelope = WebhookEnvelopeBuilders.BuildForGitLabIssue(
                root, issueId.ToString(), repoUrl, ticketUrl);

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            // 2026-09-25-3c7aa: a webhook route is per PLATFORM, so the record is looked for on
            // every connection of that type; a ticket whose stamp is gone still binds.
            envelope = envelope with
            {
                HasApprovedRecord = await approvals.ExistsForPlatformAsync(
                    config, envelope.Platform, envelope.TicketId, cancellationToken),
            };
            var matches = envelopeResolver.Resolve(config, envelope);

            logger.LogInformation(
                "GitLab issue !{Issue} → resolved matches={Count}", issueId, matches.Count);

            if (action == "reopen")
                return await statusBack!.DispatchAsync(config, matches, envelope, issueState,
                    PayloadActTime.Act(null, PayloadActTime.Text(attrs, "updated_at"))?.At,
                    PayloadActTime.Text(root, "user", "username") is { } user ? new TrackerActor(user, user) : null, cancellationToken);
            await dispatcher.DispatchAsync(config, matches, envelope, issueState, null, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitLab Issue Hook webhook");
            return WebhookResult.NotHandled();
        }
    }
}
