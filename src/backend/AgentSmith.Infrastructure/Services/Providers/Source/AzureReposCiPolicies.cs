using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Policy.WebApi;
using Newtonsoft.Json.Linq;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: a pull request's build-validation policies as checks. An evaluation counts for the head only when
/// its build ran the head (the build's pr.sourceSha, else its source version) and has not expired; otherwise the head
/// waits for its build and the check is pending. A blocking policy is required.
/// </summary>
public static class AzureReposCiPolicies
{
    /// <summary>Azure DevOps' built-in "Build" policy type.</summary>
    public const string BuildPolicyType = "0609b952-1397-4640-95ec-e00a01b2c241";

    /// <summary>The artifact a pull request's policy evaluations are filed under.</summary>
    public static string ArtifactId(Guid projectId, int prId) => $"vstfs:///CodeReview/CodeReviewId/{projectId}/{prId}";

    public static async Task<IReadOnlyList<AzureReposCiItem>> ReadAsync(IReadOnlyList<PolicyEvaluationRecord> evaluations,
        BuildHttpClient builds, Guid projectId, string head, string orgUrl, CancellationToken ct)
    {
        var items = new List<AzureReposCiItem>();
        foreach (var evaluation in evaluations.Where(IsBuildPolicy))
            items.Add(await ItemAsync(evaluation, builds, projectId, head, orgUrl, ct));
        return items;
    }

    private static bool IsBuildPolicy(PolicyEvaluationRecord e) =>
        e.Configuration?.Type?.Id == Guid.Parse(BuildPolicyType) && e.Status != PolicyEvaluationStatus.NotApplicable;

    private static async Task<AzureReposCiItem> ItemAsync(PolicyEvaluationRecord evaluation, BuildHttpClient builds,
        Guid projectId, string head, string orgUrl, CancellationToken ct)
    {
        var name = Text(evaluation.Configuration?.Settings, "displayName") ?? Text(evaluation.Context, "buildDefinitionName") ?? "build policy";
        var buildId = evaluation.Context?["buildId"]?.Type == JTokenType.Integer ? evaluation.Context["buildId"]!.Value<int>() : (int?)null;
        var expired = evaluation.Context?["isExpired"]?.Type == JTokenType.Boolean && evaluation.Context["isExpired"]!.Value<bool>();
        var build = buildId is { } id ? await builds.GetBuildAsync(projectId, id, cancellationToken: ct) : null;
        var current = !expired && build is not null && RanHead(build, head);
        var outcome = !current ? CiCheckOutcome.Pending : evaluation.Status switch
        {
            PolicyEvaluationStatus.Approved => CiCheckOutcome.Passed,
            PolicyEvaluationStatus.Rejected or PolicyEvaluationStatus.Broken => CiCheckOutcome.Failed,
            _ => CiCheckOutcome.Pending,
        };
        var at = evaluation.CompletedDate is { } done ? new DateTimeOffset(DateTime.SpecifyKind(done, DateTimeKind.Utc)) : (DateTimeOffset?)null;
        return new AzureReposCiItem(new CiCheck(name, outcome, evaluation.Configuration?.IsBlocking == true, at), buildId,
            buildId is { } b ? $"{orgUrl}/{projectId}/_build/results?buildId={b}" : null, null);
    }

    private static bool RanHead(Build build, string head) =>
        string.Equals(build.TriggerInfo is { } trigger && trigger.TryGetValue("pr.sourceSha", out var sha) ? sha : build.SourceVersion,
            head, StringComparison.OrdinalIgnoreCase);

    private static string? Text(JObject? o, string name) => o?[name]?.Type == JTokenType.String ? o[name]!.Value<string>() : null;
}
