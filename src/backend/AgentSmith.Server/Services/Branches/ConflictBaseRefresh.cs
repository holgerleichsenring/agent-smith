using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Branches;

/// <summary>
/// 2026-11-24-297b: a pull request its host says conflicts with its base cannot merge, so the push that resolves it costs
/// nothing a person would not pay anyway — the record read starts the refresh-base run for it in every project, before
/// the conflict is written, so no page reads the conflict without the refresh at work. One refresh per conflict on a head:
/// none while one is at work, none if one started since the conflict began (a refused one stands until the head moves,
/// or a landing moves the base), and none for a ticket that is not free. 2026-11-25-9992i: the one automatic base refresh
/// left — the landing-driven refresh of conflict-free siblings is gone.
/// </summary>
public sealed class ConflictBaseRefresh(
    IServiceScopeFactory scopes, BaseRefreshStart start, ILogger<ConflictBaseRefresh> logger)
{
    /// <summary>Launches the refresh when the conflict is due one; true when a run was started.</summary>
    /// <param name="host">The provider the read used; it names the pull request's base.</param>
    /// <param name="conflictsSince">When this conflict began on this head (<see cref="ConflictsSince"/>).</param>
    public async Task<bool> LaunchIfDueAsync(ResolvedProject project, RepoConnection repo, ISourceProvider host,
        RefreshedPullRequest pullRequest, DateTimeOffset conflictsSince, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(pullRequest);
        try
        {
            using var scope = scopes.CreateScope();
            var tried = await scope.ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>()
                .NewestRefreshesAsync([pullRequest.Url], ct);
            if (!Due(tried.GetValueOrDefault(pullRequest.Url), conflictsSince)) return false;
            // 2026-11-24-f9c5f: a series' final pull request has no ticket to be busy; its beat asks, one at a time.
            // 2026-11-24-9dc0e: the free ticket, the base and the launch are BaseRefreshStart's, shared with Try again.
            return (await start.StartAsync(scope.ServiceProvider, project, repo, host, pullRequest, ct)).RunId is not null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Starting the refresh of conflicting {Url} failed", pullRequest.Url);
            return false;
        }
    }

    /// <summary>A conflict is due a refresh when no refresh-base run named it yet, or the newest is not at work and started
    /// before the conflict began.</summary>
    public static bool Due(PullRequestRefreshTried? newest, DateTimeOffset conflictsSince) =>
        newest is null || newest.LeavesDue(conflictsSince);

    /// <summary>When the conflict a read reports began on the read's head: the stored stamp while the head stayed and the
    /// record already conflicted, else the read's own time.</summary>
    public static DateTimeOffset ConflictsSince(PrReviewRecordView? stored, string? readHead, DateTimeOffset readAt)
    {
        var moved = stored?.HeadSha is { } was && readHead is { } head && head != was;
        return !moved && stored is { HasConflicts: true, ConflictsAt: { } at } ? at : readAt;
    }
}
