using AgentSmith.Dispatcher.Handlers;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Adapters;

/// <summary>
/// Parses an incoming Slack message text into a typed intent and routes it
/// to the appropriate handler. Acts as the message-level dispatcher for
/// all platform-agnostic intents received via Slack.
/// </summary>
public sealed class SlackMessageDispatcher(
    ChatIntentParser parser,
    FixTicketIntentHandler fixHandler,
    ListTicketsIntentHandler listHandler,
    CreateTicketIntentHandler createHandler,
    SlackAdapter adapter,
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
            var intent = parser.Parse(text, userId, channelId, DispatcherDefaults.PlatformSlack);
            await RouteAsync(intent, channelId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error dispatching Slack message from {UserId} in {ChannelId}", userId, channelId);
            await SendErrorSafeAsync(channelId, ex.Message, cancellationToken);
        }
    }

    private async Task RouteAsync(
        object intent,
        string channelId,
        CancellationToken cancellationToken)
    {
        switch (intent)
        {
            case FixTicketIntent fix:
                await fixHandler.HandleAsync(fix, cancellationToken);
                break;

            case ListTicketsIntent list:
                await listHandler.HandleAsync(list, cancellationToken);
                break;

            case CreateTicketIntent create:
                await createHandler.HandleAsync(create, cancellationToken);
                break;

            default:
                await adapter.SendMessageAsync(
                    channelId,
                    ":question: I didn't understand that. Try:\n" +
                    "• `fix #65 in todo-list`\n" +
                    "• `list tickets in todo-list`\n" +
                    "• `create ticket \"Add logging\" in todo-list`",
                    cancellationToken);
                break;
        }
    }

    private async Task SendErrorSafeAsync(
        string channelId,
        string message,
        CancellationToken cancellationToken)
    {
        try
        {
            await adapter.SendErrorAsync(channelId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send error message to channel {ChannelId}", channelId);
        }
    }
}
