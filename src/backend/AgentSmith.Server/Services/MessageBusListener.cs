using AgentSmith.Server.Contracts;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Background service that subscribes to Redis Streams for all active jobs
/// and relays messages to the appropriate chat platform adapter via
/// <see cref="IBusMessageRouter"/>.
/// One subscription task is spawned per active job.
/// </summary>
public sealed class MessageBusListener(
    IMessageBus messageBus,
    IBusMessageRouter messageRouter,
    ILogger<MessageBusListener> logger) : BackgroundService
{
    private readonly JobSubscriptionRegistry _registry = new(logger);

    /// <summary>
    /// Registers a new job subscription after it has been spawned.
    /// Called by the dispatcher endpoint after JobSpawner.SpawnAsync succeeds.
    /// </summary>
    public async Task TrackJobAsync(string jobId, CancellationToken cancellationToken)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var task = SubscribeToJobAsync(jobId, cts.Token);

        if (!await _registry.TryAddAsync(jobId, task, cts, cancellationToken))
        {
            logger.LogWarning("Job {JobId} is already being tracked", jobId);
            await cts.CancelAsync();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Returns the IDs of all currently tracked jobs.
    /// Used by OrphanJobDetector to check for stale subscriptions.
    /// </summary>
    public Task<IReadOnlyList<string>> GetTrackedJobIdsAsync(CancellationToken cancellationToken)
        => _registry.GetTrackedIdsAsync(cancellationToken);

    /// <summary>
    /// Cancels the subscription for a specific job (e.g. when orphan detected).
    /// </summary>
    public Task CancelJobAsync(string jobId, CancellationToken cancellationToken)
        => _registry.CancelAsync(jobId, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageBusListener started");
        while (!stoppingToken.IsCancellationRequested)
        {
            await _registry.RemoveCompletedAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task SubscribeToJobAsync(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in messageBus.SubscribeToJobAsync(jobId, cancellationToken))
                await messageRouter.HandleAsync(message, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Subscription for job {JobId} was cancelled", jobId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription for job {JobId} failed unexpectedly", jobId);
        }
        finally
        {
            await _registry.RemoveAsync(jobId);
        }
    }

    public override void Dispose()
    {
        _registry.Dispose();
        base.Dispose();
    }
}
