using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.Build.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-f9c5d: a pull request into a series branch has no build-validation policy of its own; its checks are the
/// newest builds, on its source branch at its head, of the definitions that validate the default branch — what
/// <see cref="AzureReposCiTrigger"/> queued, or a branch trigger ran. A blocking policy's build is required.
/// </summary>
public static class AzureReposSeriesBuilds
{
    public static async Task<IReadOnlyList<AzureReposCiItem>> ReadAsync(IReadOnlyList<AzureReposBuildPolicyScope.Validation> validating,
        BuildHttpClient builds, Guid projectId, string sourceRef, string head, string orgUrl, CancellationToken ct)
    {
        if (validating.Count == 0) return [];
        var ran = await builds.GetBuildsAsync(projectId, definitions: [.. validating.Select(v => v.DefinitionId)], branchName: sourceRef,
            queryOrder: BuildQueryOrder.QueueTimeDescending, cancellationToken: ct);
        return Items(validating, ran, projectId, head, orgUrl);
    }

    /// <summary>Each validating definition's newest build at the head as a check; none at all on the head is no check.</summary>
    internal static IReadOnlyList<AzureReposCiItem> Items(IReadOnlyList<AzureReposBuildPolicyScope.Validation> validating,
        IReadOnlyList<Build> ran, Guid projectId, string head, string orgUrl)
    {
        var onHead = validating.Select(v => (Validation: v, Build: ran.FirstOrDefault(b => b.Definition?.Id == v.DefinitionId
            && string.Equals(b.SourceVersion, head, StringComparison.OrdinalIgnoreCase)))).ToList();
        // Nothing ran on the head yet is no CI at all — the state the trigger is started on; once one ran, a missing one waits.
        if (onHead.All(x => x.Build is null)) return [];
        return [.. onHead.Select(x => x.Build is { } build
            ? new AzureReposCiItem(new CiCheck(x.Validation.Name, Outcome(build), x.Validation.Required, At(build)), build.Id,
                $"{orgUrl}/{projectId}/_build/results?buildId={build.Id}", null)
            : new AzureReposCiItem(new CiCheck(x.Validation.Name, CiCheckOutcome.NotStarted, x.Validation.Required, null), null, null, null))];
    }

    internal static CiCheckOutcome Outcome(Build build) => build.Status != BuildStatus.Completed ? CiCheckOutcome.Pending
        : build.Result is BuildResult.Succeeded ? CiCheckOutcome.Passed : CiCheckOutcome.Failed;

    private static DateTimeOffset? At(Build build) =>
        build.FinishTime is { } done ? new DateTimeOffset(DateTime.SpecifyKind(done, DateTimeKind.Utc)) : null;
}
