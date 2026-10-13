using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// A GitLab merge request closed, merged, reopened, approved or unapproved. It starts nothing: it
/// asks only for the merge request's record to be read again. Every other merge_request action is
/// not this handler's.
/// </summary>
public sealed class GitLabMrStateWebhookHandler(
    PrRecordNudge records, ILogger<GitLabMrStateWebhookHandler> logger, TriggerModeGate? modeGate = null) : IWebhookHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
    {
        "close", "merge", "reopen", "approved", "unapproved", "approval", "unapproval",
    };

    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "merge_request";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var mr = root.GetProperty("object_attributes");
            if (PayloadActTime.Text(mr, "action") is not { } action || !Actions.Contains(action))
                return WebhookResult.NotHandled("not a state change or approval");
            var repoUrl = PayloadActTime.Text(root, "project", "web_url") ?? string.Empty;
            if (modeGate?.RepoRefusal(repoUrl) is { } polled) return polled;
            var delivery = new PrRecordDelivery(repoUrl, PayloadActTime.Text(mr, "source_branch"),
                Number(mr, "source_project_id") == Number(mr, "target_project_id"), PayloadActTime.Text(mr, "url"));
            return await records.NudgeAsync(delivery, cancellationToken)
                ? WebhookResult.HandledNoRoute()
                : WebhookResult.NotHandled("not a merge request of a ticket");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the GitLab merge-request state delivery");
            return WebhookResult.NotHandled();
        }
    }

    private static string? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64().ToString() : null;
}
