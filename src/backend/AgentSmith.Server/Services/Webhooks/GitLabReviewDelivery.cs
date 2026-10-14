using System.Text.Json;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-f147: whether a GitLab merge-request delivery can be a submitted Request changes —
/// action update, no oldrev (a source push), no change but the reviewers (an edit of the title,
/// description, labels or anything else is not a review), and a reviewer in requested_changes.
/// <see cref="IsReviewersOnlyUpdate"/> is the same update without a requester — a reviewer's approval,
/// a review, a reviewer added or removed — which changes the record and starts nothing.
/// </summary>
public static class GitLabReviewDelivery
{
    private static readonly HashSet<string> ReviewOnlyChanges =
        new(StringComparer.Ordinal) { "reviewers", "updated_at", "updated_by_id" };

    public static bool IsReviewSubmission(JsonElement root, out IReadOnlySet<string> requesting)
    {
        requesting = RequestingChanges(root);
        return IsReviewersOnlyUpdate(root) && requesting.Count > 0;
    }

    public static bool IsReviewersOnlyUpdate(JsonElement root) =>
        root.TryGetProperty("object_attributes", out var mr)
        && PayloadActTime.Text(mr, "action") == "update"
        && !(mr.TryGetProperty("oldrev", out var oldrev) && oldrev.ValueKind == JsonValueKind.String)
        && OnlyReviewChanges(root);

    private static bool OnlyReviewChanges(JsonElement root) =>
        !root.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Object
        || changes.EnumerateObject().All(c => ReviewOnlyChanges.Contains(c.Name));

    private static HashSet<string> RequestingChanges(JsonElement root) =>
        root.TryGetProperty("reviewers", out var reviewers) && reviewers.ValueKind == JsonValueKind.Array
            ? [.. reviewers.EnumerateArray()
                .Where(r => PayloadActTime.Text(r, "state") == "requested_changes"
                    && r.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                .Select(r => r.GetProperty("id").GetInt64().ToString())]
            : [];
}
