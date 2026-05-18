using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
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
    ILogger<GitLabIssueWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "Issue Hook";

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
            if (action is not "update" and not "open")
                return WebhookResult.NotHandled();

            var issueState = attrs.GetProperty("state").GetString() ?? "";
            var issueId = attrs.GetProperty("iid").GetInt32();
            var ticketUrl = attrs.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;
            var repoUrl = root.TryGetProperty("project", out var proj)
                ? proj.GetProperty("web_url").GetString() ?? "" : "";

            var envelope = WebhookEnvelopeBuilders.BuildForGitLabIssue(
                root, issueId.ToString(), repoUrl, ticketUrl);

            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            var matches = envelopeResolver.Resolve(config, envelope);

            logger.LogInformation(
                "GitLab issue !{Issue} → resolved matches={Count}", issueId, matches.Count);

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
