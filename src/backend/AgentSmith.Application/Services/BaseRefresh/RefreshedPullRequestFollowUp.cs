using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.BaseRefresh;

/// <summary>
/// 2026-11-24-5ee0: what a pushed refresh leaves its pull request: the refusals earlier refreshes posted on it are resolved
/// (<see cref="IPrMarkedThreadResolver"/>, on Azure Repos where an open one blocks completion), and the required builds that
/// have not started on it are queued (<see cref="PullRequestBuildRequeue"/>, the policy evaluation's own re-queue on Azure).
/// Each is best effort: the push stands whatever a host refuses.
/// </summary>
public sealed class RefreshedPullRequestFollowUp(
    ISourceProviderFactory sources, PullRequestBuildRequeue requeue, ILogger<RefreshedPullRequestFollowUp> logger)
{
    /// <summary>The sentence the run adds to its result; empty when nothing changed.</summary>
    public async Task<string> RunAsync(RepoConnection repo, string? prUrl, string? prNumber, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(repo);
        if (repo.Type == RepoType.Local || prUrl is not { Length: > 0 } url) return string.Empty;
        var provider = sources.Create(repo);
        var resolved = prNumber is { Length: > 0 } number ? await ResolveAsync(provider, url, number, ct) : 0;
        var queued = await requeue.RequeueAsync(provider, url, ct);
        return string.Concat(
            resolved > 0 ? $" Resolved {resolved} earlier refusal thread(s)." : string.Empty,
            queued > 0 ? $" Queued {queued} required build(s) that had not started." : string.Empty);
    }

    private async Task<int> ResolveAsync(ISourceProvider provider, string url, string number, CancellationToken ct)
    {
        if (provider is not IPrMarkedThreadResolver threads) return 0;
        try { return await threads.ResolveMarkedThreadsAsync(number, RefreshBranchBaseHandler.Marker, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "{Url}: the earlier refusal threads were not resolved", url);
            return 0;
        }
    }
}
