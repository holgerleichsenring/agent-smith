using AgentSmith.Server.Contracts;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Models;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

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

    private readonly Dictionary<string, (Task Task, CancellationTokenSource Cts)> _activeSubscriptions = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Registers a new job subscription after it has been spawned.
    /// Called by the dispatcher endpoint after JobSpawner.SpawnAsync succeeds.
    /// </summary>
    public async Task TrackJobAsync(string jobId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_activeSubscriptions.ContainsKey(jobId))
            {
                logger.LogWarning("Job {JobId} is already being tracked", jobId);
                return;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var subscriptionTask = SubscribeToJobAsync(jobId, cts.Token);
            _activeSubscriptions[jobId] = (subscriptionTask, cts);

            logger.LogInformation("Started tracking job {JobId} ({Active} active jobs)",
                jobId, _activeSubscriptions.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Returns the IDs of all currently tracked jobs.
    /// Used by OrphanJobDetector to check for stale subscriptions.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetTrackedJobIdsAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            return _activeSubscriptions.Keys.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Cancels the subscription for a specific job (e.g. when orphan detected).
    /// </summary>
    public async Task CancelJobAsync(string jobId, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_activeSubscriptions.TryGetValue(jobId, out var entry))
            {
                await entry.Cts.CancelAsync();
                logger.LogInformation("Cancelled subscription for orphaned job {JobId}", jobId);
            }
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
            await CleanupJobAsync(jobId);
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

        // Update last activity timestamp for orphan detection
        await stateManager.TouchActivityAsync(state.Platform, state.ChannelId, cancellationToken);

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

            case BusMessageType.Detail:
                await adapter.SendDetailAsync(state.ChannelId, message.Text, cancellationToken);
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

        var question = new AgentSmith.Contracts.Dialogue.DialogQuestion(
            message.QuestionId,
            AgentSmith.Contracts.Dialogue.QuestionType.Confirmation,
            message.Text,
            Context: null,
            Choices: null,
            DefaultAnswer: null,
            Timeout: TimeSpan.FromMinutes(5));

        // Fire-and-forget: the adapter blocks until the user answers or times out
        _ = adapter.AskTypedQuestionAsync(state.ChannelId, question, cancellationToken);

        // Store the pending question so the interaction endpoint can route the answer
        await stateManager.SetPendingQuestionAsync(
            state.Platform, state.ChannelId, message.QuestionId, cancellationToken);

        logger.LogInformation(
            "Question '{QuestionId}' posted to channel {ChannelId}",
            message.QuestionId, state.ChannelId);
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
        var errorContext = BuildErrorContext(state, message);
        await adapter.SendErrorAsync(state.ChannelId, errorContext, cancellationToken);

        await stateManager.RemoveAsync(state.Platform, state.ChannelId, cancellationToken);
        await messageBus.CleanupJobAsync(message.JobId, cancellationToken);

        logger.LogError(
            "Job {JobId} failed for channel {ChannelId}: {Error}",
            message.JobId, state.ChannelId, message.Text);
    }

    private static ErrorContext BuildErrorContext(ConversationState state, BusMessage message)
    {
        return new ErrorContext(
            JobId: message.JobId,
            ChannelId: state.ChannelId,
            TicketId: state.TicketId,
            Project: state.Project,
            FailedStep: message.Step ?? 0,
            TotalSteps: message.Total ?? 0,
            StepName: message.StepName ?? string.Empty,
            RawError: message.Text,
            FriendlyError: ErrorFormatter.Humanize(message.Text),
            LogUrl: null);
    }

    private async Task CleanupJobAsync(string jobId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_activeSubscriptions.TryGetValue(jobId, out var entry))
            {
                entry.Cts.Dispose();
                _activeSubscriptions.Remove(jobId);
            }

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
                .Where(kvp => kvp.Value.Task.IsCompleted)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var jobId in completed)
            {
                if (_activeSubscriptions.TryGetValue(jobId, out var entry))
                {
                    entry.Cts.Dispose();
                    _activeSubscriptions.Remove(jobId);
                }
            }

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
