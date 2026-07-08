using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Parses an incoming Slack message via the IntentEngine and routes the
/// resolved intent to the appropriate handler.
/// </summary>
public sealed class SlackMessageDispatcher(
    IntentEngine intentEngine,
    FixTicketIntentHandler fixHandler,
    ListTicketsIntentHandler listHandler,
    CreateTicketIntentHandler createHandler,
    InitProjectIntentHandler initHandler,
    HelpHandler helpHandler,
    ClarificationStateManager clarificationState,
    SpecDialogRouter specDialogRouter,
    IPlatformAdapter adapter,
    ILogger<SlackMessageDispatcher> logger)
{
    public async Task DispatchAsync(
        string text,
        string userId,
        string channelId,
        CancellationToken cancellationToken,
        string? threadId = null,
        string platform = DispatcherDefaults.PlatformSlack)
    {
        try
        {
            // Spec-dialog branch first: /spec commands and follow-ups inside an
            // open spec thread never reach the intent engine (p0315a).
            if (await specDialogRouter.TryRouteAsync(
                    text, userId, channelId, threadId, platform, cancellationToken))
                return;

            // The run-trigger path keeps its historical platform label ("slack"
            // even for Teams — see ChatAdaptersExtensions); only the spec-dialog
            // branch above keys state by the real platform.
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
            case InitProjectIntent init:
                await initHandler.HandleAsync(init, ct);
                break;
            case SecurityReviewIntent sec:
                await fixHandler.HandleAsync(new FixTicketIntent
                {
                    RawText = sec.RawText, UserId = sec.UserId, ChannelId = sec.ChannelId,
                    Platform = sec.Platform, TicketId = 0, Project = sec.Project,
                    PipelineOverride = "security-scan"
                }, ct);
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
