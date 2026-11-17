using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-17-3435a: an Azure DevOps pull request's CI — its build-validation policies and the statuses an external CI
/// posted, decided by <see cref="CiVerdict"/>. A failed build is read with its failed tasks' issues and log end, a
/// failed status with its description and link.
/// </summary>
public sealed class AzureReposCiReader(
    Func<CancellationToken, Task<GitHttpClient>> git, AzureReposPolicyEvaluations policies,
    Func<CancellationToken, Task<BuildHttpClient>> builds, string project, string repoName, string orgUrl)
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
        var items = (await AzureReposCiPolicies.ReadAsync(evaluations, buildClient, projectId, head, orgUrl, ct))
            .Concat(await AzureReposCiStatuses.ReadAsync(client, project, repoName, prId, ct)).ToList();
        var checks = items.Select(i => i.Check).ToList();
        var state = CiVerdict.Of(checks);
        return new PullRequestCi(state, head, CiVerdict.At(checks),
            state == CiStates.Red ? await FailuresAsync(items, checks, buildClient, projectId, request, ct) : []);
    }

    private static async Task<IReadOnlyList<CiFailure>> FailuresAsync(IReadOnlyList<AzureReposCiItem> items, IReadOnlyList<CiCheck> checks,
        BuildHttpClient builds, Guid projectId, CiReadRequest request, CancellationToken ct)
    {
        var deciding = CiVerdict.Deciding(checks);
        var failures = new List<CiFailure>();
        foreach (var item in items.Where(i => i.Check.Outcome == CiCheckOutcome.Failed && deciding.Contains(i.Check)))
        {
            var excerpt = item.BuildId is { } build && failures.Count < request.FailuresRead
                ? await AzureReposBuildEvidence.ExcerptAsync(builds, projectId, build, request, ct)
                : CiExcerpt.Of([item.Description], null, request.ExcerptChars);
            failures.Add(new CiFailure(item.Check.Name, item.Url, excerpt));
        }
        return failures;
    }
}
