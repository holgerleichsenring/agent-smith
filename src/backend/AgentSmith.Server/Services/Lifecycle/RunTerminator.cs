using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Services.Lifecycle;

/// <summary>
/// Kills what a cancelled run left running. Split from <see cref="CancelEnforcer"/>
/// (2026-08-24-ca23), which decides WHETHER a run is cancelled and records it; this decides
/// whether anything is still alive to stop and answers whether the finalize may proceed.
/// <para>
/// p0357 (p0330b): terminate retries are BOUNDED. Past the window after the kill deadline, an
/// unkillable pod (k8s API down, job 404/gone) no longer blocks the finalize — the run must
/// reach 'cancelled' in bounded time, never wedge in 'cancelling'. A row with no persisted
/// deadline has been wedged already: no window.
/// </para>
/// </summary>
public sealed class RunTerminator(IServiceProvider services, TimeProvider timeProvider,
    ILogger<RunTerminator> logger)
{
    public static readonly TimeSpan RetryWindow = TimeSpan.FromMinutes(10);

    /// <summary>True when the finalize may proceed — nothing to kill, or the kill landed.</summary>
    public async Task<bool> TryTerminateAsync(Run run, CancellationToken ct)
    {
        // 2026-08-24-ca23: a paused run holds nothing to terminate, and the STATUS says so — a
        // pause never clears the job id, so that field still names a pod that died back then.
        if (RunStatuses.IsWaiting(run.Status)) return true;
        if (string.IsNullOrEmpty(run.JobId)) return true; // in-process run: nothing spawned
        var spawner = services.GetService<IJobSpawner>();
        if (spawner is null)
        {
            // No spawner in this composition — the job cannot exist here; finalize anyway
            // rather than wedging the run in 'cancelling' forever.
            logger.LogWarning("Run {RunId} has job {JobId} but no IJobSpawner is registered",
                run.Id, run.JobId);
            return true;
        }
        try
        {
            await spawner.TerminateAsync(run.JobId!, ct);
            return true;
        }
        catch (Exception ex)
        {
            if (IsPastRetryWindow(run))
            {
                logger.LogError(ex,
                    "Terminate failed for run {RunId} job {JobId} and the retry window elapsed — "
                    + "finalizing 'cancelled' anyway so the run reaches terminal", run.Id, run.JobId);
                return true;
            }
            logger.LogError(ex,
                "Terminate failed for run {RunId} job {JobId} — will retry next scan", run.Id, run.JobId);
            return false;
        }
    }

    private bool IsPastRetryWindow(Run run) =>
        run.CancelDeadlineAt is not { } deadline
        || timeProvider.GetUtcNow() - deadline > RetryWindow;
}
