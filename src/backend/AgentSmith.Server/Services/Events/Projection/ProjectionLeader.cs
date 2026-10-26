using AgentSmith.Infrastructure.Persistence.Contracts;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Events.Projection;

/// <summary>
/// 2026-10-26-0f19c: what holding the projection lease does. The tenure begins, every run the store
/// calls unfinished is caught up at once — a run whose last events the previous holder never wrote may
/// publish nothing more, so waiting for its next event would leave it behind — and the tenure ends when
/// the lease is lost or the replica stops.
/// </summary>
public sealed class ProjectionLeader(
    ProjectionLeadership leadership,
    LeaderGatedRunEventPersistence gate,
    IUnfinishedRunSource unfinishedRuns,
    ILogger<ProjectionLeader> logger)
{
    public const string LeaseKey = "agentsmith:leader:projection";

    public async Task LeadAsync(CancellationToken ct)
    {
        leadership.Begin();
        logger.LogInformation("This replica now writes run events to the store (tenure {Tenure})", leadership.Tenure);
        try
        {
            foreach (var runId in await unfinishedRuns.GetUnfinishedRunIdsAsync(ct))
                await gate.SyncAsync(runId, ct);
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
        finally
        {
            leadership.End();
            logger.LogInformation("This replica no longer writes run events to the store");
        }
    }
}
