using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-11-24-a4e7: the nudge worker's refresh of a ticket's pull-request records, run at once under the same ticket lock,
/// so the records keep their one writer. A lock another replica holds queues a Refresh nudge instead, served after it. A
/// refresh that fails is logged: the act it follows already happened, and the periodic re-read reads the records again.
/// </summary>
public sealed class PrRecordRefreshNow(
    PrReviewRecordRefresh refresh, IRedisClaimLock locks, IReworkNudges nudges, ILogger<PrRecordRefreshNow> logger) : IPrRecordRefreshNow
{
    public async Task RefreshAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(project);
        var key = ReworkNudgeWorker.LockKey(project.Name, ticketId);
        if (await locks.TryAcquireAsync(key, ReworkNudgeRepository.ClaimLease, ct) is not { } token)
        {
            await nudges.EnqueueAsync(new ReworkNudgeRequest(project.Name, ticketId, ReworkNudgeOrigin.Refresh), ct);
            return;
        }
        try { await refresh.RefreshAsync(project, ticketId, nudgedUrl: null, ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Pull-request records of {Project}/#{Ticket} were not refreshed", project.Name, ticketId);
        }
        finally { await locks.ReleaseAsync(key, token, CancellationToken.None); }
    }
}
