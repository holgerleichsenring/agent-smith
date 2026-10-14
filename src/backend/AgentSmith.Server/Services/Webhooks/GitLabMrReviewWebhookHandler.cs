using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-f147: a GitLab merge-request update that shows a reviewer in requested_changes. A
/// first request changes the reviewer list; a repeated one changes nothing, so the delivery is taken
/// when it carries no oldrev (a push) and no change besides the reviewers. Registered after the
/// label handler. 2026-10-08-0781: it nudges the ticket; the worker reads GitLab's own
/// "requested changes" note for the act and its time. A reviewers-only update that is no admitted
/// request for changes asks only for the merge request's record to be read again.
/// </summary>
public sealed class GitLabMrReviewWebhookHandler(
    PrReworkAdmission admission, PrRecordNudge records, ILogger<GitLabMrReviewWebhookHandler> logger,
    TriggerModeGate? modeGate = null) : IWebhookHandler
{
    public bool CanHandle(string platform, string eventType) =>
        platform == "gitlab" && eventType == "merge_request";

    public async Task<WebhookResult> HandleAsync(
        string payload, IDictionary<string, string> headers, CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (!GitLabReviewDelivery.IsReviewersOnlyUpdate(root))
                return WebhookResult.NotHandled("not a submitted request for changes");
            GitLabReviewDelivery.IsReviewSubmission(root, out var requesting);
            var mr = root.GetProperty("object_attributes");
            var author = Number(mr, "author_id");
            var reviewer = requesting.FirstOrDefault(r => r != author);
            var repoUrl = PayloadActTime.Text(root, "project", "web_url") ?? string.Empty;
            if (modeGate?.RepoRefusal(repoUrl) is { } polled) return polled; // 2026-10-08-101b
            var request = new PrReviewRequest(RepoType.GitLab, repoUrl, PayloadActTime.Text(mr, "source_branch"),
                Number(mr, "source_project_id") == Number(mr, "target_project_id"),
                new PrCommentAuthor(repoUrl, Number(root.GetProperty("project"), "id") ?? string.Empty, reviewer ?? string.Empty, reviewer ?? string.Empty),
                author, PayloadActTime.Text(mr, "url") ?? string.Empty);
            var result = reviewer is null
                ? WebhookResult.NotHandled(requesting.Count == 0 ? "not a submitted request for changes" : "only the author requests changes")
                : await admission.AdmitAsync(request, cancellationToken);
            return result.Handled || !await records.NudgeAsync(PrRecordDelivery.Of(request), cancellationToken)
                ? result : WebhookResult.HandledNoRoute();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read the GitLab merge-request review delivery");
            return WebhookResult.NotHandled();
        }
    }

    private static string? Number(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64().ToString() : null;
}
