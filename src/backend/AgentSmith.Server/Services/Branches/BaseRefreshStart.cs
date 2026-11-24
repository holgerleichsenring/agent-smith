using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-24-9dc0e, extracted from ConflictBaseRefresh: what every start of a refresh-base run for one pull request
/// asks before <see cref="BaseRefreshLauncher"/> — the one launch path — runs: the ticket is free (a series' final pull
/// request has none), and the host names the pull request's base. Each caller keeps its own rule of when one is due.
/// </summary>
public sealed class BaseRefreshStart(RefreshableTicket tickets, ILogger<BaseRefreshStart> logger)
{
    /// <param name="scope">The scope the launcher is resolved from.</param>
    public async Task<BaseRefreshStarted> StartAsync(IServiceProvider scope, ResolvedProject project, RepoConnection repo,
        ISourceProvider host, RefreshedPullRequest pullRequest, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pullRequest);
        if (pullRequest.Branch is null && !await tickets.FreeAsync(project, pullRequest.TicketId, ct))
            return BaseRefreshStarted.Refused($"{pullRequest.TicketId} is not free for a refresh: it is closed, at work, queued "
                + "or waiting for an answer.");
        if (await host.ReadPullRequestBaseAsync(pullRequest.Url, ct) is not { Length: > 0 } baseBranch)
        {
            logger.LogWarning("{Url}: its host named no base to merge", pullRequest.Url);
            return BaseRefreshStarted.Refused($"The host named no base branch for {pullRequest.Url}, so there is nothing to merge.");
        }
        var runId = await scope.GetRequiredService<BaseRefreshLauncher>().LaunchAsync(project, repo, pullRequest, baseBranch, ct);
        return BaseRefreshStarted.Started(runId);
    }
}
