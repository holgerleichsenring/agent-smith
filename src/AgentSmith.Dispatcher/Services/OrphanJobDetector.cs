using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

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
    ILogger<OrphanJobDetector> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxInactivity = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinRuntime = TimeSpan.FromMinutes(10);

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

    private async Task DetectAndCleanupOrphansAsync(CancellationToken cancellationToken)
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

                if (!IsOrphaned(state, now))
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

                if (!IsOrphaned(state, now))
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

    private static bool IsOrphaned(ConversationState state, DateTimeOffset now)
    {
        var runtime = now - state.StartedAt;
        var inactivity = now - state.LastActivityAt;

        return runtime > MinRuntime && inactivity > MaxInactivity;
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
                    $"(no activity for over {MaxInactivity.TotalMinutes:0} minutes, " +
                    $"running for {runtime.TotalMinutes:0} minutes). " +
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
