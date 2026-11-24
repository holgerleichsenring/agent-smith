using AgentSmith.Contracts.Reviews;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// 2026-11-24-f9c5d: Azure DevOps runs a pull request's required builds from the target branch's build-validation policies,
/// which a series branch does not have. The definitions that validate the default branch are queued on the pull request's
/// source branch at its head; <see cref="AzureReposSeriesBuilds"/> reads them back as the pull request's checks. Queuing needs
/// the token's Build (read and execute); a refusal is answered with Azure's words.
/// </summary>
public sealed class AzureReposCiTrigger(
    Func<CancellationToken, Task<GitHttpClient>> git, AzureReposPolicyConfigurations configurations,
    Func<CancellationToken, Task<BuildHttpClient>> builds, string project, string repoName, string orgUrl) : IPullRequestCiTrigger
{
    public async Task<CiTrigger> StartAsync(string prUrl, string? defaultBranch, CancellationToken cancellationToken)
    {
        if (!AzureReposPullRequestUpdater.TryParsePullRequestId(prUrl, out var prId))
            return CiTrigger.Impossible($"'{prUrl}' is not an Azure Repos pull request URL.");
        try
        {
            var client = await git(cancellationToken);
            var pr = await client.GetPullRequestAsync(project, repoName, prId, cancellationToken: cancellationToken);
            // A pull request's repository reference may leave the default branch out; the repository itself names it.
            var main = defaultBranch ?? (pr.Repository.DefaultBranch ?? (await client.GetRepositoryAsync(project, repoName, cancellationToken: cancellationToken)).DefaultBranch)
                ?.Replace("refs/heads/", string.Empty, StringComparison.Ordinal) ?? "main";
            var projectId = pr.Repository.ProjectReference.Id;
            var validating = AzureReposBuildPolicyScope.Validating(await configurations(projectId, cancellationToken), pr.Repository.Id, $"refs/heads/{main}");
            if (validating.Count == 0) return CiTrigger.None($"no build-validation policy covers {main} in {repoName}, so it has no pull-request CI");
            var buildClient = await builds(cancellationToken);
            Build? first = null;
            foreach (var v in validating)
                first ??= await buildClient.QueueBuildAsync(new Build
                {
                    Definition = new DefinitionReference { Id = v.DefinitionId }, Project = new TeamProjectReference { Id = projectId },
                    SourceBranch = pr.SourceRefName, SourceVersion = pr.LastMergeSourceCommit?.CommitId,
                }, cancellationToken: cancellationToken);
            return CiTrigger.Started($"queued {string.Join(", ", validating.Select(v => v.Name))} — the builds that validate {main} — on "
                + $"{pr.SourceRefName?.Replace("refs/heads/", string.Empty, StringComparison.Ordinal)} at its head",
                first is null ? null : $"{orgUrl}/{projectId}/_build/results?buildId={first.Id}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return CiTrigger.Impossible($"Azure DevOps did not queue the pull request's builds: {ex.Message.Split('\n', 2)[0].Trim()} "
                + "(the token needs Policy read and Build read & execute)");
        }
    }
}
