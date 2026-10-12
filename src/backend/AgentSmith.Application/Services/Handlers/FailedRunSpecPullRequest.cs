using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-12-3a36c: the spec pull request opened at derivation is a failed run's persisted
/// work. CommitAndPR opens no record carrier on a failed run, so the spec repository's
/// outcome would otherwise read "no changes" — and the step result and the ticket comment
/// would say nothing was kept. This keeps it 'opened' and puts the failure on its body.
/// </summary>
public sealed class FailedRunSpecPullRequest(
    ISourceProviderFactory sourceFactory,
    PullRequestOutcomePublisher outcomes,
    ILogger<FailedRunSpecPullRequest> logger)
{
    /// <summary>
    /// Replaces the spec repository's skipped outcome in <paramref name="opened"/> by the spec
    /// PR, publishes it and writes <paramref name="body"/> to the PR once. Returns the kept
    /// outcome, or null when there is no spec PR or the loop did not skip its repository.
    /// </summary>
    public async Task<OpenedPullRequest?> KeepAsync(
        PipelineContext pipeline, IReadOnlyList<RepoConnection> repos,
        List<OpenedPullRequest> opened, string body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(opened);
        if (!pipeline.TryGet<string>(ContextKeys.SpecRepo, out var repoName) || string.IsNullOrEmpty(repoName)
            || !pipeline.TryGet<string>(ContextKeys.SpecPullRequestUrl, out var url) || string.IsNullOrWhiteSpace(url))
            return null;
        var skipped = opened.FindIndex(o => o.RepoName == repoName && o.Status == OpenStatus.SkippedNoChanges);
        if (skipped < 0 || opened.Any(o => o.RepoName == repoName && o.Status == OpenStatus.Opened))
            return null;
        var repo = repos.FirstOrDefault(r => r.Name == repoName);
        if (repo is not null) await RefreshBodyAsync(repo, url!, body, ct);
        opened[skipped] = new OpenedPullRequest(repoName!, url, OpenStatus.Opened);
        await outcomes.PublishAsync(pipeline, opened[skipped], ct);
        return opened[skipped];
    }

    private async Task RefreshBodyAsync(RepoConnection repo, string url, string body, CancellationToken ct)
    {
        try
        {
            var updated = await sourceFactory.Create(repo).UpdatePullRequestBodyAsync(url, body, ct);
            logger.LogInformation(
                "{Repo}: the spec PR {Url} is the failed run's persisted work (body updated: {Updated})",
                repo.Name, url, updated);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Repo}: could not put the failure on the spec PR {Url}", repo.Name, url);
        }
    }
}
