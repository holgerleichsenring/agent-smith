using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-24-a4e7: the nudge worker's refresh of a ticket's pull-request records, run at once under the same ticket lock,
/// so the records keep their one writer. It reads the records only: the CI loop, which may run a whole merge-green
/// pipeline, is left to the worker through a Refresh nudge — also queued when another replica holds the lock.
/// <para>The act it follows already happened on the host, so nothing here fails it: every error is logged, and the work
/// runs on no token, so a client that hangs up does not cut a refresh off halfway. The periodic re-read covers a miss.</para>
/// </summary>
public sealed class PrRecordRefreshNow(
    PrReviewRecordRefresh refresh, IRedisClaimLock locks, IReworkNudges nudges, ILogger<PrRecordRefreshNow> logger) : IPrRecordRefreshNow
{
    public async Task RefreshAsync(ResolvedProject project, string ticketId)
    {
        ArgumentNullException.ThrowIfNull(project);
        await SafelyAsync(() => ReadAsync(project, ticketId), "were not refreshed", project, ticketId);
        await SafelyAsync(() => nudges.EnqueueAsync(new ReworkNudgeRequest(project.Name, ticketId, ReworkNudgeOrigin.Refresh),
            CancellationToken.None), "got no Refresh nudge", project, ticketId);
    }

    private async Task SafelyAsync(Func<Task> work, string failed, ResolvedProject project, string ticketId)
    {
        try { await work(); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Pull-request records of {Project}/#{Ticket} {Failed}", project.Name, ticketId, failed);
        }
    }

    // Under the worker's ticket lock; a busy lock reads nothing, the queued nudge reads after its holder.
    private async Task ReadAsync(ResolvedProject project, string ticketId)
    {
        var key = ReworkNudgeWorker.LockKey(project.Name, ticketId);
        if (await locks.TryAcquireAsync(key, ReworkNudgeRepository.ClaimLease, CancellationToken.None) is not { } token) return;
        try { await refresh.RefreshAsync(project, ticketId, nudgedUrl: null, CancellationToken.None); }
        finally { await locks.ReleaseAsync(key, token, CancellationToken.None); }
    }
}
