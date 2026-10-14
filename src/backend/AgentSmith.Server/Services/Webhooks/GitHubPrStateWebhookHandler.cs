using System.Text.Json;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// A GitHub pull request closed (merged or not) or reopened. It starts nothing: it asks only for the
/// pull request's record to be read again. Every other pull_request action is not this handler's.
/// </summary>
public sealed class GitHubPrStateWebhookHandler(
    PrRecordNudge records, ILogger<GitHubPrStateWebhookHandler> logger, TriggerModeGate? modeGate = null) : IWebhookHandler
{
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal) { "closed", "reopened" };

    public bool CanHandle(string platform, string eventType) =>
        platform == "github" && eventType == "pull_request";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (PayloadActTime.Text(root, "action") is not { } action || !Actions.Contains(action))
                return WebhookResult.NotHandled("not a close or reopen");
            var delivery = Read(root);
            if (modeGate?.RepoRefusal(delivery.RepoUrl) is { } polled) return polled;
            return await records.NudgeAsync(delivery, cancellationToken)
                ? WebhookResult.HandledNoRoute()
                : WebhookResult.NotHandled("not a pull request of a ticket");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the GitHub pull-request state delivery");
            return WebhookResult.NotHandled();
        }
    }

    private static PrRecordDelivery Read(JsonElement root)
    {
        var pr = root.GetProperty("pull_request");
        var repository = root.GetProperty("repository");
        var fullName = PayloadActTime.Text(repository, "full_name");
        return new PrRecordDelivery(PayloadActTime.Text(repository, "clone_url") ?? string.Empty,
            PayloadActTime.Text(pr, "head", "ref"),
            string.Equals(PayloadActTime.Text(pr, "head", "repo", "full_name"), fullName, StringComparison.OrdinalIgnoreCase),
            PayloadActTime.Text(pr, "html_url"));
    }
}
