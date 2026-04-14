using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Bus;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services;

/// <summary>
/// Resolves conversation state for a bus message, finds the matching
/// platform adapter, and dispatches by message type.
/// </summary>
internal sealed class BusMessageRouter(
    ConversationStateManager stateManager,
    IEnumerable<IPlatformAdapter> adapters,
    IMessageBus messageBus,
    ILogger<BusMessageRouter> logger) : IBusMessageRouter
{
    private readonly Dictionary<string, IPlatformAdapter> _adapters =
        adapters.ToDictionary(a => a.Platform, StringComparer.OrdinalIgnoreCase);

    public async Task HandleAsync(BusMessage message, CancellationToken cancellationToken)
    {
        var state = await stateManager.GetByJobIdAsync(message.JobId, cancellationToken);
        if (state is null)
        {
            logger.LogWarning("Received message for unknown job {JobId}, ignoring", message.JobId);
            return;
        }

        await stateManager.TouchActivityAsync(state.Platform, state.ChannelId, cancellationToken);

        if (!_adapters.TryGetValue(state.Platform, out var adapter))
        {
            logger.LogWarning("No adapter for platform '{Platform}' (job {JobId})", state.Platform, message.JobId);
            return;
        }

        switch (message.Type)
        {
            case BusMessageType.Progress:
                await adapter.SendProgressAsync(
                    state.ChannelId, message.Step ?? 0, message.Total ?? 0,
                    message.Text, cancellationToken);
                break;
            case BusMessageType.Question:
                await HandleQuestionAsync(adapter, state, message, cancellationToken);
                break;
            case BusMessageType.Done:
                await HandleCompletionAsync(adapter, state, message, cancellationToken);
                break;
            case BusMessageType.Error:
                await HandleFailureAsync(adapter, state, message, cancellationToken);
                break;
            case BusMessageType.Detail:
                await adapter.SendDetailAsync(state.ChannelId, message.Text, cancellationToken);
                break;
            default:
                logger.LogDebug("Ignoring message type {Type} for job {JobId}", message.Type, message.JobId);
                break;
        }
    }

    private async Task HandleQuestionAsync(
        IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.QuestionId))
        {
            logger.LogWarning("Question message missing QuestionId for job {JobId}", message.JobId);
            return;
        }

        var question = new DialogQuestion(
            message.QuestionId,
            QuestionType.Confirmation,
            message.Text,
            Context: null,
            Choices: null,
            DefaultAnswer: null,
            Timeout: TimeSpan.FromMinutes(5));

        _ = adapter.AskTypedQuestionAsync(state.ChannelId, question, cancellationToken);

        await stateManager.SetPendingQuestionAsync(
            state.Platform, state.ChannelId, message.QuestionId, cancellationToken);

        logger.LogInformation("Question '{QuestionId}' posted to {ChannelId}", message.QuestionId, state.ChannelId);
    }

    private async Task HandleCompletionAsync(
        IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        await adapter.SendDoneAsync(state.ChannelId, message.Summary ?? message.Text,
            message.PrUrl, cancellationToken);
        await CleanupJobStateAsync(state, message, cancellationToken);
        logger.LogInformation("Job {JobId} completed for {ChannelId}", message.JobId, state.ChannelId);
    }

    private async Task HandleFailureAsync(
        IPlatformAdapter adapter, ConversationState state,
        BusMessage message, CancellationToken cancellationToken)
    {
        var errorContext = ErrorContext.FromBusMessage(state, message);
        await adapter.SendErrorAsync(state.ChannelId, errorContext, cancellationToken);
        await CleanupJobStateAsync(state, message, cancellationToken);
        logger.LogError("Job {JobId} failed for {ChannelId}: {Error}", message.JobId, state.ChannelId, message.Text);
    }

    private async Task CleanupJobStateAsync(
        ConversationState state, BusMessage message, CancellationToken cancellationToken)
    {
        await stateManager.RemoveAsync(state.Platform, state.ChannelId, cancellationToken);
        await messageBus.CleanupJobAsync(message.JobId, cancellationToken);
    }
}
