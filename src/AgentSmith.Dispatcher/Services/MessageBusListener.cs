using AgentSmith.Dispatcher.Adapters;
using AgentSmith.Dispatcher.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services;

/// <summary>
/// Background service that subscribes to Redis Streams for all active jobs
/// and relays progress, questions, done and error messages to the appropriate
/// chat platform adapter.
/// One subscription task is spawned per active job.
/// </summary>
public sealed class MessageBusListener(
    IMessageBus messageBus,
    ConversationStateManager stateManager,
    IEnumerable<IPlatformAdapter> adapters,
    ILogger<MessageBusListener> logger) : BackgroundService
{
    private readonly Dictionary<string, IPlatformAdapter> _adapters =
        adapters.ToDictionary(a => a.Platform, StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, Task> _activeSubscriptions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Registers a new job subscription after it has been spawned.
    /// Called by the dispatcher endpoint after JobSpawner.SpawnAsync succeeds.
    /// </summary>
    public async Task TrackJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_activeSubscriptions.ContainsKey(jobId))
            {
                logger.LogWarning("Job {JobId} is already being tracked", jobId);
                return;
            }

            var subscriptionTask = SubscribeToJobAsync(jobId, cancellationToken);
            _activeSubscriptions[jobId] = subscriptionTask;

            logger.LogInformation("Started tracking job {JobId} ({Active} active jobs)",
                jobId, _activeSubscriptions.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MessageBusListener started");

        // Periodically clean up completed subscription tasks
        while (!stoppingToken.IsCancellationRequested)
        {
            await CleanupCompletedSubscriptionsAsync();
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task SubscribeToJobAsync(string jobId, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in messageBus.SubscribeToJobAsync(jobId, cancellationToken))
            {
                await HandleMessageAsync(message, cancellationToken);
            }
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
            await CleanupJobAsync(jobId, cancellationToken);
        }
    }

    private async Task HandleMessageAsync(BusMessage message, CancellationToken cancellationToken)
    {
        var state = await stateManager.GetByJobIdAsync(message.JobId, cancellationToken);
        if (state is null)
        {
            logger.LogWarning("Received message for unknown job {JobId}, ignoring", message.JobId);
            return;
        }

        if (!_adapters.TryGetValue(state.Platform, out var adapter))
        {
            logger.LogWarning(
                "No adapter registered for platform '{Platform}' (job {JobId})",
                state.Platform, message.JobId);
            return;
        }

        switch (message.Type)
        {
            case BusMessageType.Progress:
                await HandleProgressAsync(adapter, state, message, cancellationToken);
                break;

            case BusMessageType.Question:
                await HandleQuestionAsync(adapter, state, message, cancellationToken);
                break;

            case BusMessageType.Done:
                await HandleDoneAsync(adapter, state, message, cancellationToken);
                break;

            case BusMessageType.Error:
                await HandleErrorAsync(adapter, state, message, cancellationToken);
                break;

            default:
                logger.LogDebug(
                    "Ignoring message type {Type} for job {JobId}", message.Type, message.JobId);
                break;
        }
    }

    private static Task HandleProgressAsync(IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        return adapter.SendProgressAsync(
            state.ChannelId,
            message.Step ?? 0,
            message.Total ?? 0,
            message.Text,
            cancellationToken);
    }

    private async Task HandleQuestionAsync(IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.QuestionId))
        {
            logger.LogWarning("Question message missing QuestionId for job {JobId}", message.JobId);
            return;
        }

        var messageId = await adapter.AskQuestionAsync(
            state.ChannelId,
            message.QuestionId,
            message.Text,
            cancellationToken);

        // Store the pending question so the interaction endpoint can route the answer
        await stateManager.SetPendingQuestionAsync(
            state.Platform, state.ChannelId, message.QuestionId, cancellationToken);

        logger.LogInformation(
            "Question '{QuestionId}' posted to channel {ChannelId} (messageId={MessageId})",
            message.QuestionId, state.ChannelId, messageId);
    }

    private async Task HandleDoneAsync(IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        await adapter.SendDoneAsync(state.ChannelId, message.Summary ?? message.Text,
            message.PrUrl, cancellationToken);

        await stateManager.RemoveAsync(state.Platform, state.ChannelId, cancellationToken);
        await messageBus.CleanupJobAsync(message.JobId, cancellationToken);

        logger.LogInformation(
            "Job {JobId} completed successfully for channel {ChannelId}", message.JobId, state.ChannelId);
    }

    private async Task HandleErrorAsync(IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        await adapter.SendErrorAsync(state.ChannelId, message.Text, cancellationToken);

        await stateManager.RemoveAsync(state.Platform, state.ChannelId, cancellationToken);
        await messageBus.CleanupJobAsync(message.JobId, cancellationToken);

        logger.LogError(
            "Job {JobId} failed for channel {ChannelId}: {Error}",
            message.JobId, state.ChannelId, message.Text);
    }

    private async Task CleanupJobAsync(string jobId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _activeSubscriptions.Remove(jobId);
            logger.LogDebug(
                "Removed subscription for job {JobId} ({Active} remaining)",
                jobId, _activeSubscriptions.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task CleanupCompletedSubscriptionsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var completed = _activeSubscriptions
                .Where(kvp => kvp.Value.IsCompleted)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var jobId in completed)
                _activeSubscriptions.Remove(jobId);

            if (completed.Count > 0)
                logger.LogDebug("Cleaned up {Count} completed job subscriptions", completed.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    public override void Dispose()
    {
        _lock.Dispose();
        base.Dispose();
    }
}
