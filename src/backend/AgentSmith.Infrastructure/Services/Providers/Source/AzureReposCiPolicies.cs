using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Policy.WebApi;
using Newtonsoft.Json.Linq;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a pull request's build-validation policies as checks. An evaluation counts for the head only when
/// its build ran the head (the build's pr.sourceSha, else its source version) and has not expired; otherwise the head
/// waits for its build and the check is pending. A blocking policy is required.
/// 2026-11-23-c785: a build id of 0 (Azure's "no build behind this evaluation") names no build — asking for build 0 threw
/// and left a stale CI standing for days; with no build to compare, the evaluation's own status is the check.
/// 2026-11-23-0219: an expired evaluation, or one queued with no build on the head, is not started (<see cref="Outcome"/>).
/// 2026-11-26-5201f: a pull-request build's source version is the test merge, so a build of the head's test merge ran the head
/// even without pr.sourceSha; a build that cannot be fetched (deleted, retained away) no longer fails the read — as for build 0,
/// the evaluation's own status is the check, taken as about the head (Azure resets an evaluation when the source moves).
/// </summary>
public static class AzureReposCiPolicies
{
    /// <summary>Azure DevOps' built-in "Build" policy type.</summary>
    public const string BuildPolicyType = "0609b952-1397-4640-95ec-e00a01b2c241";

    /// <summary>The artifact a pull request's policy evaluations are filed under.</summary>
    public static string ArtifactId(Guid projectId, int prId) => $"vstfs:///CodeReview/CodeReviewId/{projectId}/{prId}";

    /// <param name="testMerge">The pull request's test merge of <paramref name="head"/>; null when it names none.</param>
    /// <param name="unreadable">Told each build that could not be fetched, so the caller logs it.</param>
    public static async Task<IReadOnlyList<AzureReposCiItem>> ReadAsync(IReadOnlyList<PolicyEvaluationRecord> evaluations,
        BuildHttpClient builds, Guid projectId, string head, string orgUrl, CancellationToken ct, string? testMerge = null,
        Action<int, Exception>? unreadable = null)
    {
        var items = new List<AzureReposCiItem>();
        foreach (var evaluation in evaluations.Where(IsBuildPolicy))
            items.Add(await ItemAsync(evaluation, builds, projectId, (head, testMerge), orgUrl, unreadable, ct));
        return items;
    }

    private static bool IsBuildPolicy(PolicyEvaluationRecord e) =>
        e.Configuration?.Type?.Id == Guid.Parse(BuildPolicyType) && e.Status != PolicyEvaluationStatus.NotApplicable;

    private static async Task<AzureReposCiItem> ItemAsync(PolicyEvaluationRecord evaluation, BuildHttpClient builds,
        Guid projectId, (string Head, string? TestMerge) head, string orgUrl, Action<int, Exception>? unreadable, CancellationToken ct)
    {
        var name = Text(evaluation.Configuration?.Settings, "displayName") ?? Text(evaluation.Context, "buildDefinitionName") ?? "build policy";
        var buildId = evaluation.Context?["buildId"]?.Type == JTokenType.Integer ? evaluation.Context["buildId"]!.Value<int>() : (int?)null;
        if (buildId <= 0) buildId = null;
        var expired = evaluation.Context?["isExpired"]?.Type == JTokenType.Boolean && evaluation.Context["isExpired"]!.Value<bool>();
        var build = buildId is { } id ? await BuildAsync(builds, projectId, id, unreadable, ct) : null;
        if (build is null) buildId = null;
        var onHead = build is null || RanHead(build, head.Head, head.TestMerge);
        var at = evaluation.CompletedDate is { } done ? new DateTimeOffset(DateTime.SpecifyKind(done, DateTimeKind.Utc)) : (DateTimeOffset?)null;
        return new AzureReposCiItem(new CiCheck(name, Outcome(evaluation.Status, expired, buildId is not null, onHead),
                evaluation.Configuration?.IsBlocking == true, at), buildId,
            buildId is { } b ? $"{orgUrl}/{projectId}/_build/results?buildId={b}" : null, null) { EvaluationId = evaluation.EvaluationId };
    }

    /// <summary>
    /// 2026-11-23-0219: how one build-policy evaluation stands on the head. Expired, or queued with no build on the head (none
    /// behind it, or one that ran another head), is not started — Azure queues nothing on a draft, and a published or retargeted
    /// pull request may wait so for good. A verdict on the head decides; anything else (a running build, a queued build of the
    /// head, a verdict on an older head about to be re-run) still runs.
    /// </summary>
    internal static CiCheckOutcome Outcome(PolicyEvaluationStatus? status, bool expired, bool hasBuild, bool onHead)
    {
        if (expired || (status == PolicyEvaluationStatus.Queued && !(hasBuild && onHead))) return CiCheckOutcome.NotStarted;
        if (!onHead) return CiCheckOutcome.Pending;
        return status switch
        {
            PolicyEvaluationStatus.Approved => CiCheckOutcome.Passed,
            PolicyEvaluationStatus.Rejected or PolicyEvaluationStatus.Broken => CiCheckOutcome.Failed,
            _ => CiCheckOutcome.Pending,
        };
    }

    private static bool RanHead(Build build, string head, string? testMerge) =>
        build.TriggerInfo is { } trigger && trigger.TryGetValue("pr.sourceSha", out var sha)
            ? string.Equals(sha, head, StringComparison.OrdinalIgnoreCase)
            : string.Equals(build.SourceVersion, head, StringComparison.OrdinalIgnoreCase)
                || (testMerge is not null && string.Equals(build.SourceVersion, testMerge, StringComparison.OrdinalIgnoreCase));

    private static async Task<Build?> BuildAsync(BuildHttpClient builds, Guid projectId, int id, Action<int, Exception>? unreadable, CancellationToken ct)
    {
        try { return await builds.GetBuildAsync(projectId, id, cancellationToken: ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            unreadable?.Invoke(id, ex); // the caller logs it: a static holds no logger
            return null;
        }
    }

    /// <summary>The warning a caller logs for a build <see cref="ReadAsync"/> could not fetch.</summary>
    public const string UnreadableBuild = "Build {BuildId} behind a build policy could not be read; the evaluation's own status is the check";

    private static string? Text(JObject? o, string name) => o?[name]?.Type == JTokenType.String ? o[name]!.Value<string>() : null;
}
