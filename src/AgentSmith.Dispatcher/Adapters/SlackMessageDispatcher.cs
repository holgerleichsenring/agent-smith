using AgentSmith.Dispatcher.Handlers;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Adapters;

/// <summary>
/// Parses an incoming Slack message via the IntentEngine and routes the
/// resolved intent to the appropriate handler.
/// </summary>
public sealed class SlackMessageDispatcher(
    IntentEngine intentEngine,
    FixTicketIntentHandler fixHandler,
    ListTicketsIntentHandler listHandler,
    CreateTicketIntentHandler createHandler,
    HelpHandler helpHandler,
    ClarificationStateManager clarificationState,
    IPlatformAdapter adapter,
    ILogger<SlackMessageDispatcher> logger)
{
    public async Task DispatchAsync(
        string text,
        string userId,
        string channelId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var intent = await intentEngine.ParseAsync(
                text, userId, channelId, DispatcherDefaults.PlatformSlack, cancellationToken);
            await RouteAsync(intent, channelId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dispatching message from {UserId} in {ChannelId}", userId, channelId);
            await SendErrorSafeAsync(channelId, ex.Message, cancellationToken);
        }
    }

    private async Task RouteAsync(ChatIntent intent, string channelId, CancellationToken ct)
    {
        switch (intent)
        {
            case FixTicketIntent fix:
                await fixHandler.HandleAsync(fix, ct);
                break;
            case ListTicketsIntent list:
                await listHandler.HandleAsync(list, ct);
                break;
            case CreateTicketIntent create:
                await createHandler.HandleAsync(create, ct);
                break;
            case HelpIntent:
                await helpHandler.SendHelpAsync(channelId, ct);
                break;
            case GreetingIntent:
                await helpHandler.SendGreetingAsync(channelId, ct);
                break;
            case ErrorIntent error:
                await adapter.SendMessageAsync(channelId, $":x: {error.ErrorMessage}", ct);
                break;
            case ClarificationNeeded c:
                await HandleClarificationAsync(c, channelId, ct);
                break;
            default:
                await helpHandler.SendUnknownAsync(channelId, intent.RawText, ct);
                break;
        }
    }

    private async Task HandleClarificationAsync(
        ClarificationNeeded c, string channelId, CancellationToken ct)
    {
        var pending = new PendingClarification(c.Suggestion, c.RawText, c.UserId);
        await clarificationState.SetAsync(DispatcherDefaults.PlatformSlack, channelId, pending, ct);
        await helpHandler.SendClarificationAsync(channelId, c.Suggestion, ct);
    }

    private async Task SendErrorSafeAsync(string channelId, string message, CancellationToken ct)
    {
        try
        {
            await adapter.SendMessageAsync(channelId, $":x: {message}", ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send error to channel {ChannelId}", channelId);
        }
    }
}
