using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
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
    ILogger<GitHubIssueWebhookHandler> logger) : IWebhookHandler
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

            if (root.GetProperty("action").GetString() != "labeled")
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
            var matches = envelopeResolver.Resolve(config, envelope);

            logger.LogInformation(
                "GitHub issue #{Issue} → resolved matches={Count}", issueNumber, matches.Count);

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
