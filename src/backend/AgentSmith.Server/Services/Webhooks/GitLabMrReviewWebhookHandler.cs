using System.Text.Json;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-f147: a GitLab merge-request update that shows a reviewer in requested_changes. A
/// first request changes the reviewer list; a repeated one changes nothing, so the delivery is taken
/// when it carries no oldrev (a push) and no change besides the reviewers. Registered after the MR
/// event and label handlers. 2026-10-08-0781: it nudges the ticket; the worker reads GitLab's own
/// "requested changes" note for the act and its time.
/// </summary>
public sealed class GitLabMrReviewWebhookHandler(
    PrReworkAdmission admission, ILogger<GitLabMrReviewWebhookHandler> logger) : IWebhookHandler
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
            if (!GitLabReviewDelivery.IsReviewSubmission(root, out var requesting))
                return WebhookResult.NotHandled("not a submitted request for changes");
            var mr = root.GetProperty("object_attributes");
            var author = Number(mr, "author_id");
            var reviewer = requesting.FirstOrDefault(r => r != author);
            if (reviewer is null) return WebhookResult.NotHandled("only the author requests changes");
            var repoUrl = PayloadActTime.Text(root, "project", "web_url") ?? string.Empty;
            return await admission.AdmitAsync(new PrReviewRequest(RepoType.GitLab, repoUrl, PayloadActTime.Text(mr, "source_branch"),
                Number(mr, "source_project_id") == Number(mr, "target_project_id"),
                new PrCommentAuthor(repoUrl, Number(root.GetProperty("project"), "id") ?? string.Empty, reviewer, reviewer),
                author, PayloadActTime.Text(mr, "url") ?? string.Empty), cancellationToken);
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
