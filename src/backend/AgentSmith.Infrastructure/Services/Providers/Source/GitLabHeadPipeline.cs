using System.Text.Json;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Services.Providers.Pipelines;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-25-7a92a: how a GitLab merge request's CI stands on one head, from its merge-request read. A pipeline on the head
/// decides — manual ("needs to be manually started": a blocking manual job) has not started and will not by itself; the rest as
/// <see cref="GitLabPipelineStatus"/> says (the deploy's reading, unchanged). No pipeline on the head is judged only for the
/// merge request's current head on a settled head into a non-series branch: not started when an older head had a pipeline or
/// GitLab says ci_must_pass; otherwise it still waits (an older pipeline) or there is no CI (null). A series branch's silent
/// head stays none, which f9c5d's start serves.
/// </summary>
public static class GitLabHeadPipeline
{
    public const string Manual = "manual";

    public static CiCheckOutcome? Outcome(JsonElement mr, string? head, bool headSettled)
    {
        var pipeline = Pipeline(mr);
        if (pipeline is { } on && Str(on, "sha") == head)
            return Str(on, "status") == Manual ? CiCheckOutcome.NotStarted : GitLabPipelineStatus.Outcome(Str(on, "status"));
        var judged = headSettled && head is not null && head == Str(mr, "sha") && !WorkBranches.IsSeries(Str(mr, "target_branch"));
        if (pipeline is not null) return judged ? CiCheckOutcome.NotStarted : CiCheckOutcome.Pending;
        return judged && Str(mr, "detailed_merge_status") == "ci_must_pass" ? CiCheckOutcome.NotStarted : null;
    }

    /// <summary>The head pipeline, whichever head it ran; null when the merge request has none.</summary>
    public static JsonElement? Pipeline(JsonElement mr) =>
        mr.TryGetProperty("head_pipeline", out var p) && p.ValueKind == JsonValueKind.Object ? p : null;

    /// <summary>2026-11-25-7a92b: whether a pipeline ran (or runs) on the head itself.</summary>
    public static bool HasPipelineOn(JsonElement mr, string? head) => Pipeline(mr) is { } p && Str(p, "sha") == head;

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
