using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// Handles GitHub Issues labeled events. p0140b: builds an IncomingTicketEnvelope, asks
/// IEnvelopeProjectResolver for matches, and hands each match to WebhookSpawnDispatcher.
/// The dispatcher applies the per-match status-filter and spawns N pipeline runs (one per
/// repo in the matched project) via SpawnPipelineRunsUseCase.
/// </summary>
public sealed class GitHubIssueWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    WebhookSpawnDispatcher dispatcher,
    ApprovedRecordProbe approvals,
    ILogger<GitHubIssueWebhookHandler> logger,
    StatusBackGate? statusBack = null) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "issues";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // 2026-10-08-2123: a reopen is a person's move back of a finished ticket, or nothing.
            var action = root.GetProperty("action").GetString();
            if (action != "labeled" && (action != "reopened" || statusBack is null))
                return WebhookResult.NotHandled();

            var issueEl = root.GetProperty("issue");
            var issueState = issueEl.TryGetProperty("state", out var stateEl)
                ? stateEl.GetString() ?? "open" : "open";
            var issueNumber = issueEl.GetProperty("number").GetInt32();
            var repoUrl = root.GetProperty("repository").GetProperty("html_url").GetString() ?? "";
            var ticketUrl = issueEl.TryGetProperty("html_url", out var urlEl) ? urlEl.GetString() : null;

            var envelope = WebhookEnvelopeBuilders.BuildForGitHubIssue(
                issueEl, issueNumber.ToString(), repoUrl, ticketUrl);

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
                "GitHub issue #{Issue} → resolved matches={Count}", issueNumber, matches.Count);

            if (action == "reopened")
                return await statusBack!.DispatchAsync(config, matches, envelope, issueState,
                    PayloadActTime.Act(null, PayloadActTime.Text(issueEl, "updated_at"))?.At,
                    PayloadActTime.Text(root, "sender", "login") is { } login ? new TrackerActor(login, login) : null, cancellationToken);
            await dispatcher.DispatchAsync(config, matches, envelope, issueState, null, cancellationToken);
            return WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GitHub issues webhook");
            return WebhookResult.NotHandled();
        }
    }
}
