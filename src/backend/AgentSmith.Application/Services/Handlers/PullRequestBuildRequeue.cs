using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-11-23-0219: the pipeline step that queues a pull request's required builds again where they have not started on its
/// head (<see cref="IPullRequestBuildRequeuer"/>) — after a run publishes a draft, after a retarget run moves one, and in a
/// person's merge. A host with no such builds, a local repository or a failed re-queue changes nothing: the merge or the
/// publish it follows stands, and the CI read still says the build has not started.
/// </summary>
public sealed class PullRequestBuildRequeue(ISourceProviderFactory sources, ILogger<PullRequestBuildRequeue> logger)
{
    /// <summary>Re-queues through the repo's provider; returns how many builds were queued.</summary>
    public Task<int> RequeueAsync(RepoConnection repo, string prUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return repo.Type == RepoType.Local ? Task.FromResult(0) : RequeueAsync(sources.Create(repo), prUrl, ct);
    }

    /// <summary>Re-queues through a provider the caller already holds; returns how many builds were queued.</summary>
    public async Task<int> RequeueAsync(ISourceProvider provider, string prUrl, CancellationToken ct)
    {
        if (provider is not IPullRequestBuildRequeuer requeuer) return 0;
        try
        {
            var queued = await requeuer.RequeueNotStartedAsync(prUrl, ct);
            if (queued > 0) logger.LogInformation("{Url}: {Count} required build(s) queued again", prUrl, queued);
            return queued;
        }
        // 2026-11-24-5ee0: a host call's own timeout is a failed re-queue, not the run's cancellation.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Url}: the required builds could not be queued again", prUrl);
            return 0;
        }
    }
}
