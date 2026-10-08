using System.Text.Json;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-2123: a Jira issue_updated whose changelog moves the status — a person's move back of a
/// finished ticket, through <see cref="StatusBackGate"/>, or nothing. Registered after the assignee
/// handler: one delivery that changes both is taken by the first that handles it, never twice.
/// </summary>
public sealed class JiraStatusWebhookHandler(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IEnvelopeProjectResolver envelopeResolver,
    ApprovedRecordProbe approvals,
    StatusBackGate gate,
    ILogger<JiraStatusWebhookHandler> logger) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "jira" && eventType == "issue_updated";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!MovesStatus(root)) return WebhookResult.NotHandled();
            var issue = root.GetProperty("issue");
            var key = issue.GetProperty("key").GetString()!;
            var envelope = WebhookEnvelopeBuilders.BuildForJiraIssue(root, key, PayloadActTime.Text(issue, "self"));
            var config = configLoader.LoadConfig(serverContext.ConfigPath);
            envelope = envelope with
            {
                HasApprovedRecord = await approvals.ExistsForPlatformAsync(config, envelope.Platform, envelope.TicketId, cancellationToken),
            };
            return await gate.DispatchAsync(config, envelopeResolver.Resolve(config, envelope), envelope,
                JiraAssigneeWebhookHandler.ExtractIssueStatus(root), At(root), Actor(root), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the Jira status webhook");
            return WebhookResult.NotHandled();
        }
    }

    private static bool MovesStatus(JsonElement root) =>
        root.TryGetProperty("changelog", out var log) && log.TryGetProperty("items", out var items)
        && items.ValueKind == JsonValueKind.Array
        && items.EnumerateArray().Any(i => PayloadActTime.Text(i, "field") is { } f && f.Equals("status", StringComparison.OrdinalIgnoreCase));

    // Jira's webhook timestamp is epoch milliseconds.
    private static DateTimeOffset? At(JsonElement root) =>
        root.TryGetProperty("timestamp", out var t) && t.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeMilliseconds(t.GetInt64()) : null;

    private static TrackerActor? Actor(JsonElement root) =>
        root.TryGetProperty("user", out var user)
        && (PayloadActTime.Text(user, "accountId") ?? PayloadActTime.Text(user, "key") ?? PayloadActTime.Text(user, "name")) is { } id
            ? new TrackerActor(id, PayloadActTime.Text(user, "name") ?? PayloadActTime.Text(user, "displayName"))
            : null;
}
