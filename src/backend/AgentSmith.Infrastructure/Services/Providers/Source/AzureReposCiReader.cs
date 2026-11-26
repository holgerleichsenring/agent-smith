using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: an Azure DevOps pull request's CI — its build-validation policies and the statuses an external CI
/// posted, decided by <see cref="CiVerdict"/>. A failed build is read with its failed tasks' issues and log end, a
/// failed status with its description and link.
/// </summary>
/// <para>2026-11-24-f9c5d: a pull request into a series branch has no build policy of its own; its checks are the builds of the
/// definitions that validate the default branch, on its head (<see cref="AzureReposSeriesBuilds"/>). 2026-11-26-5201f: read
/// whenever no required check stands — a status that is not required no longer hides them.</para>
public sealed class AzureReposCiReader(
    Func<CancellationToken, Task<GitHttpClient>> git, AzureReposPolicyEvaluations policies,
    Func<CancellationToken, Task<BuildHttpClient>> builds, string project, string repoName, string orgUrl,
    AzureReposPolicyConfigurations? configurations = null, Func<CancellationToken, Task<string>>? defaultBranch = null, ILogger? logger = null,
    Func<CancellationToken, Task<Microsoft.TeamFoundation.TestManagement.WebApi.TestManagementHttpClient>>? tests = null) // 2026-11-26-49d3a
{
    public async Task<PullRequestCi> ReadAsync(string prUrl, CiReadRequest request, CancellationToken ct)
    {
        if (!AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId))
            throw new ArgumentException($"Not an Azure Repos pull request URL: {prUrl}", nameof(prUrl));
        var client = await git(ct);
        var pr = await client.GetPullRequestAsync(project, repoName, prId, cancellationToken: ct);
        var head = request.HeadSha ?? pr.LastMergeSourceCommit?.CommitId;
        if (head is null) return PullRequestCi.NoneOn(null);
        var projectId = pr.Repository.ProjectReference.Id;
        var buildClient = await builds(ct);
        var evaluations = await policies(projectId, AzureReposCiPolicies.ArtifactId(projectId, prId), ct);
        var testMerge = pr.LastMergeSourceCommit?.CommitId == head ? pr.LastMergeCommit?.CommitId : null; // 2026-11-26-5201f
        var items = (await AzureReposCiPolicies.ReadAsync(evaluations, buildClient, projectId, head, orgUrl, ct, testMerge,
            (id, ex) => logger?.LogWarning(ex, AzureReposCiPolicies.UnreadableBuild, id)))
            .Concat(await AzureReposCiStatuses.ReadAsync(client, project, repoName, prId, ct)).ToList();
        if (configurations is not null && !items.Any(i => i.Check.Required) && AgentSmith.Contracts.Providers.WorkBranches.IsSeries(pr.TargetRefName))
            items.AddRange(await AzureReposSeriesBuilds.ReadAsync(AzureReposBuildPolicyScope.Validating(await configurations(projectId, ct),
                pr.Repository.Id, $"refs/heads/{await MainAsync(pr, ct)}"), buildClient, projectId, pr.SourceRefName, head, orgUrl, ct));
        var checks = items.Select(i => i.Check).ToList();
        var state = CiVerdict.Of(checks);
        return new PullRequestCi(state, head, CiVerdict.At(checks),
            state == CiStates.Red ? await FailuresAsync(items, checks, buildClient, projectId, request, ct) : []);
    }

    // The repository's default branch as the provider resolves it — a pull request's repository reference may leave it out.
    private async Task<string> MainAsync(GitPullRequest pr, CancellationToken ct) => defaultBranch is not null ? await defaultBranch(ct)
        : pr.Repository.DefaultBranch?.Replace("refs/heads/", string.Empty, StringComparison.Ordinal) ?? "main";

    private async Task<IReadOnlyList<CiFailure>> FailuresAsync(IReadOnlyList<AzureReposCiItem> items, IReadOnlyList<CiCheck> checks,
        BuildHttpClient builds, Guid projectId, CiReadRequest request, CancellationToken ct)
    {
        var deciding = CiVerdict.Deciding(checks);
        var failures = new List<CiFailure>();
        foreach (var item in items.Where(i => i.Check.Outcome == CiCheckOutcome.Failed && deciding.Contains(i.Check)))
        {
            var excerpt = item.BuildId is { } build && failures.Count < request.FailuresRead
                ? await AzureReposBuildEvidence.ExcerptAsync(builds, projectId, build, request, ct, tests)
                : CiExcerpt.Of([item.Description], null, request.ExcerptChars);
            failures.Add(new CiFailure(item.Check.Name, item.Url, excerpt));
        }
        return failures;
    }
}
