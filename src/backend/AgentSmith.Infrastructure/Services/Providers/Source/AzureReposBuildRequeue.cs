using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-23-0219: re-queues an Azure DevOps pull request's build-policy evaluations that have not started on its head — the
/// same reading the CI read takes (<see cref="AzureReposCiPolicies.Outcome"/>), so what the row calls "build not started" is
/// exactly what is queued. A running or decided build, and an optional (non-blocking) policy's, is left alone.
/// </summary>
public sealed class AzureReposBuildRequeue(
    Func<CancellationToken, Task<GitHttpClient>> git, AzureReposPolicyEvaluations policies, AzureReposPolicyRequeue requeue,
    Func<CancellationToken, Task<BuildHttpClient>> builds, string project, string repoName, string orgUrl, ILogger logger)
{
    public async Task<int> RequeueAsync(string prUrl, CancellationToken ct)
    {
        if (!AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId))
            throw new ArgumentException($"Not an Azure Repos pull request URL: {prUrl}", nameof(prUrl));
        var pr = await (await git(ct)).GetPullRequestAsync(project, repoName, prId, cancellationToken: ct);
        if (pr.LastMergeSourceCommit?.CommitId is not { } head) return 0;
        var projectId = pr.Repository.ProjectReference.Id;
        var evaluations = await policies(projectId, AzureReposCiPolicies.ArtifactId(projectId, prId), ct);
        var items = await AzureReposCiPolicies.ReadAsync(evaluations, await builds(ct), projectId, head, orgUrl, ct);
        var queued = 0;
        foreach (var item in items.Where(i => i.Check.Outcome == CiCheckOutcome.NotStarted && i.Check.Required && i.EvaluationId is not null))
        {
            await requeue(projectId, item.EvaluationId!.Value, ct);
            logger.LogInformation("PR !{PrId}: queued '{Check}' again — it had not started on {Head}", prId, item.Check.Name, head);
            queued++;
        }
        return queued;
    }
}
