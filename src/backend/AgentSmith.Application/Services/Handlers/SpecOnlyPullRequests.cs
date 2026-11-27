using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-12-3a36c, generalised by 2026-10-12-f5f7e: a series repository that gained no code still
/// has the spec pull request opened for it at derivation, and that pull request is work of the run.
/// Its outcome would otherwise read "no changes" — and drop out of the run's pull requests, the
/// cross-link and the rework readers. This keeps it 'opened' on green and failed runs alike, puts
/// the run's body (with the sibling marker) on it and takes it out of draft only when the run is
/// ready. A repository with code needs none of this: its spec pull request IS its code pull request.
/// <para>
/// 2026-11-27-ecf2c: while another repository of the run carries code, a repository whose ticket branch carries only
/// the spec record gets no pull request — its spec pull request is left alone (no body, no ready mark, not the run's
/// outcome). Only a branch proven record-only against its base is left; one that delivers source (an earlier run's
/// code) or whose diff could not be taken against the base is kept as before.
/// </para>
/// </summary>
public sealed class SpecOnlyPullRequests(
    ISourceProviderFactory sourceFactory,
    PullRequestOutcomePublisher outcomes,
    DeliveryDiff delivery,
    ILogger<SpecOnlyPullRequests> logger,
    PullRequestBuildRequeue? requeue = null)
{
    /// <summary>
    /// Replaces each series repository's skipped outcome in <paramref name="opened"/> by its spec PR,
    /// publishes it and writes <paramref name="body"/> to the PR once, marking it ready when
    /// <paramref name="ready"/>. Returns the kept outcomes; none when there is no spec PR or the loop
    /// did not skip its repository. With <paramref name="code"/> saying another repository carries code, a repository
    /// whose branch is record-only is not kept.
    /// </summary>
    public async Task<IReadOnlyList<OpenedPullRequest>> KeepAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos,
        List<OpenedPullRequest> opened, string body, bool ready, CancellationToken ct, RunCode? code = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(opened);
        var kept = new List<OpenedPullRequest>();
        foreach (var (repoName, url) in SeriesPullRequests(pipeline))
        {
            var skipped = opened.FindIndex(o => o.RepoName == repoName && o.Status == OpenStatus.SkippedNoChanges);
            if (skipped < 0 || opened.Any(o => o.RepoName == repoName && o.Status == OpenStatus.Opened)) continue;
            if (code is { AnyCode: true } && await RecordOnlyAsync(pipeline, code, repoName, ct))
            {
                logger.LogInformation("{Repo}: the branch carries only the spec record while another repository carries code — "
                    + "its spec PR {Url} is left alone and is not the run's work", repoName, url);
                continue;
            }
            if (repos.FirstOrDefault(r => r.Name == repoName) is { } repo) await RefreshAsync(repo, url, body, ready, ct);
            opened[skipped] = new OpenedPullRequest(repoName, url, OpenStatus.Opened);
            await outcomes.PublishAsync(pipeline, opened[skipped], ct);
            kept.Add(opened[skipped]);
        }
        return kept;
    }

    // 2026-11-27-ecf2c: proven only by a diff against the base branch that touches nothing outside the run record.
    private async Task<bool> RecordOnlyAsync(PipelineContext pipeline, RunCode code, string repoName, CancellationToken ct)
    {
        if (!code.Sandboxes.TryGetValue(repoName, out var target)) return false;
        try
        {
            var diff = await delivery.ForBranchAsync(target.Sandbox, DeliveryBasis.ForSandbox(pipeline, target.Key), ct);
            // The record also writes .gitattributes at the root (SpecIndexWriter's merge=union lines) — record, not code.
            return !diff.Failed && diff.AgainstBase && CitedFileIndex.FromDiff(diff.Text).Paths
                .All(path => RunRecordPaths.IsRunRecordPath(path) || path == ProjectMetaPaths.GitAttributes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "{Repo}: the branch's delivery could not be read; its spec PR is kept", repoName);
            return false;
        }
    }

    // A repository the series no longer names keeps its pull request open, but it is no work of this run.
    private static IEnumerable<KeyValuePair<string, string>> SeriesPullRequests(PipelineContext pipeline)
    {
        var series = pipeline.TryGet<SpecSet>(ContextKeys.SpecSet, out var set) ? set?.SeriesRepos : null;
        return SpecPullRequests.Of(pipeline)
            .Where(pr => series is null || series.Contains(pr.Key, StringComparer.OrdinalIgnoreCase));
    }

    private async Task RefreshAsync(RepoConnection repo, string url, string body, bool ready, CancellationToken ct)
    {
        try
        {
            var provider = sourceFactory.Create(repo);
            var updated = await provider.UpdatePullRequestBodyAsync(url, body, ct);
            var promoted = ready && await provider.MarkPullRequestReadyAsync(url, ct);
            if (promoted && requeue is not null) await requeue.RequeueAsync(provider, url, ct); // 2026-11-23-0219
            logger.LogInformation(
                "{Repo}: the spec PR {Url} is kept as the run's work (body updated: {Updated}, ready for review: {Ready})",
                repo.Name, url, updated, promoted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Repo}: could not refresh the spec PR {Url}", repo.Name, url);
        }
    }

    /// <summary>2026-11-27-ecf2c: whether any repository of the run carries code, and each repository's commit sandbox by
    /// repository name (its key and the sandbox).</summary>
    public sealed record RunCode(bool AnyCode, IReadOnlyDictionary<string, (string Key, ISandbox Sandbox)> Sandboxes);
}
