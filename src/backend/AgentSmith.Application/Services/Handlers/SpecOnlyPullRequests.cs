using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
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
/// </summary>
public sealed class SpecOnlyPullRequests(
    ISourceProviderFactory sourceFactory,
    PullRequestOutcomePublisher outcomes,
    ILogger<SpecOnlyPullRequests> logger)
{
    /// <summary>
    /// Replaces each series repository's skipped outcome in <paramref name="opened"/> by its spec PR,
    /// publishes it and writes <paramref name="body"/> to the PR once, marking it ready when
    /// <paramref name="ready"/>. Returns the kept outcomes; none when there is no spec PR or the loop
    /// did not skip its repository.
    /// </summary>
    public async Task<IReadOnlyList<OpenedPullRequest>> KeepAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos,
        List<OpenedPullRequest> opened, string body, bool ready, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(opened);
        var kept = new List<OpenedPullRequest>();
        foreach (var (repoName, url) in SeriesPullRequests(pipeline))
        {
            var skipped = opened.FindIndex(o => o.RepoName == repoName && o.Status == OpenStatus.SkippedNoChanges);
            if (skipped < 0 || opened.Any(o => o.RepoName == repoName && o.Status == OpenStatus.Opened)) continue;
            if (repos.FirstOrDefault(r => r.Name == repoName) is { } repo) await RefreshAsync(repo, url, body, ready, ct);
            opened[skipped] = new OpenedPullRequest(repoName, url, OpenStatus.Opened);
            await outcomes.PublishAsync(pipeline, opened[skipped], ct);
            kept.Add(opened[skipped]);
        }
        return kept;
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
            logger.LogInformation(
                "{Repo}: the spec PR {Url} is kept as the run's work (body updated: {Updated}, ready for review: {Ready})",
                repo.Name, url, updated, promoted);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Repo}: could not refresh the spec PR {Url}", repo.Name, url);
        }
    }
}
