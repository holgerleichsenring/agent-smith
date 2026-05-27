using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Periodically scans for orphaned jobs (containers that crashed without
/// sending Done/Error) and cleans up their conversation state so the
/// channel is unblocked.
/// </summary>
public sealed class OrphanJobDetector(
    ConversationStateManager stateManager,
    MessageBusListener listener,
    IEnumerable<IPlatformAdapter> adapters,
    IMessageBus messageBus,
    IJobSpawner jobSpawner,
    ILogger<OrphanJobDetector> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MinRuntime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxRuntime = TimeSpan.FromMinutes(120);

    private readonly Dictionary<string, IPlatformAdapter> _adapters =
        adapters.ToDictionary(a => a.Platform, StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OrphanJobDetector started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            await DetectAndCleanupOrphansAsync(stoppingToken);
        }
    }

    internal async Task DetectAndCleanupOrphansAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;

            // 1) Check in-memory tracked jobs (active bus subscriptions)
            var trackedJobIds = await listener.GetTrackedJobIdsAsync(cancellationToken);
            foreach (var jobId in trackedJobIds)
            {
                var state = await stateManager.GetByJobIdAsync(jobId, cancellationToken);

                if (state is null)
                {
                    // State already expired via TTL — cancel the dangling subscription
                    logger.LogWarning("Job {JobId} has no conversation state, cancelling subscription", jobId);
                    await listener.CancelJobAsync(jobId, cancellationToken);
                    continue;
                }

                if (!await IsOrphanedAsync(state, now, cancellationToken))
                    continue;

                logger.LogWarning(
                    "Orphaned job detected: {JobId} (started {StartedAt}, last activity {LastActivity})",
                    jobId, state.StartedAt, state.LastActivityAt);

                await CleanupOrphanAsync(jobId, state, cancellationToken);
            }

            // 2) Scan Redis for conversation states not tracked in memory
            //    (e.g. from before a dispatcher restart)
            var allStates = await stateManager.GetAllAsync(cancellationToken);
            var trackedSet = trackedJobIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var state in allStates)
            {
                if (trackedSet.Contains(state.JobId))
                    continue; // Already handled above

                if (!await IsOrphanedAsync(state, now, cancellationToken))
                    continue;

                logger.LogWarning(
                    "Orphaned untracked job detected: {JobId} (started {StartedAt}, last activity {LastActivity})",
                    state.JobId, state.StartedAt, state.LastActivityAt);

                await CleanupOrphanAsync(state.JobId, state, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during orphan job detection");
        }
    }

    private async Task<bool> IsOrphanedAsync(
        ConversationState state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var runtime = now - state.StartedAt;

        // Don't check very young jobs (startup grace period)
        if (runtime < MinRuntime)
            return false;

        // Absolute safety limit — no job should run this long
        if (runtime > MaxRuntime)
        {
            logger.LogWarning("Job {JobId} exceeded max runtime of {MaxRuntime}", state.JobId, MaxRuntime);
            return true;
        }

        // Check if the container/pod is still alive
        try
        {
            var alive = await jobSpawner.IsAliveAsync(state.JobId, cancellationToken);

            if (alive)
            {
                // Container is still running — not orphaned
                return false;
            }

            // Container is dead/gone — it crashed without sending completion
            logger.LogInformation(
                "Container for job {JobId} is no longer running, declaring orphaned", state.JobId);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Can't determine container status — don't declare orphaned
            logger.LogWarning(ex, "Could not check container liveness for job {JobId}, skipping", state.JobId);
            return false;
        }
    }

    private async Task CleanupOrphanAsync(
        string jobId, ConversationState state, CancellationToken cancellationToken)
    {
        try
        {
            if (_adapters.TryGetValue(state.Platform, out var adapter))
            {
                var runtime = DateTimeOffset.UtcNow - state.StartedAt;
                await adapter.SendMessageAsync(
                    state.ChannelId,
                    $":warning: Job `{jobId}` appears to have crashed " +
                    $"(container no longer running after {runtime.TotalMinutes:0.#} minutes). " +
                    "The channel has been unblocked. Please retry your command.",
                    cancellationToken);
            }

            await stateManager.RemoveAsync(state.Platform, state.ChannelId, cancellationToken);
            await messageBus.CleanupJobAsync(jobId, cancellationToken);
            await listener.CancelJobAsync(jobId, cancellationToken);

            logger.LogInformation("Cleaned up orphaned job {JobId} for channel {ChannelId}",
                jobId, state.ChannelId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup orphaned job {JobId}", jobId);
        }
    }
}
