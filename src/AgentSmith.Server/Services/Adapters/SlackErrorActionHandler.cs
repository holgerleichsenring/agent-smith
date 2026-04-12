using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Dispatcher.Services.Handlers;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace AgentSmith.Dispatcher.Services.Adapters;

/// <summary>
/// Handles error action button clicks (retry, contact) from Slack error messages.
/// </summary>
public sealed class SlackErrorActionHandler(
    FixTicketIntentHandler fixHandler,
    ConversationStateManager stateManager,
    IPlatformAdapter adapter,
    ILogger<SlackErrorActionHandler> logger)
{
    private static readonly string OwnerSlackUserId =
        Environment.GetEnvironmentVariable("OWNER_SLACK_USER_ID") ?? string.Empty;

    public async Task HandleAsync(
        string channelId, string action, JsonNode payload, CancellationToken ct)
    {
        var userId = payload["user"]?["id"]?.GetValue<string>() ?? string.Empty;

        switch (action)
        {
            case "retry":
                await HandleRetryAsync(channelId, userId, payload, ct);
                break;
            case "contact":
                await HandleContactAsync(channelId, payload, ct);
                break;
            default:
                logger.LogDebug("Ignoring unknown error action: {Action}", action);
                break;
        }
    }

    private async Task HandleRetryAsync(
        string channelId, string userId, JsonNode payload, CancellationToken ct)
    {
        var value = ExtractButtonValue(payload);
        if (!TryParseRetryValue(value, out var ticketId, out var project))
        {
            logger.LogWarning("Invalid retry value: {Value}", value);
            return;
        }

        var existing = await stateManager.GetAsync(DispatcherDefaults.PlatformSlack, channelId, ct);
        if (existing is not null)
        {
            await adapter.SendMessageAsync(channelId,
                ":hourglass: A job is already running. Please wait.", ct);
            return;
        }

        await SpawnRetryJobAsync(ticketId, project, userId, channelId, ct);
    }

    private async Task SpawnRetryJobAsync(
        int ticketId, string project, string userId, string channelId, CancellationToken ct)
    {
        var intent = new FixTicketIntent
        {
            TicketId = ticketId,
            Project = project,
            RawText = $"fix #{ticketId} in {project}",
            UserId = userId,
            ChannelId = channelId,
            Platform = DispatcherDefaults.PlatformSlack
        };

        await fixHandler.HandleAsync(intent, ct);
    }

    private async Task HandleContactAsync(
        string channelId, JsonNode payload, CancellationToken ct)
    {
        var ownerId = ExtractButtonValue(payload);
        if (string.IsNullOrEmpty(ownerId))
            ownerId = OwnerSlackUserId;

        if (string.IsNullOrEmpty(ownerId))
        {
            logger.LogWarning("Contact clicked but no owner configured");
            return;
        }

        await adapter.SendMessageAsync(channelId,
            $":bust_in_silhouette: <@{ownerId}> — a user needs help with a failed job.",
            ct);
    }

    private static string ExtractButtonValue(JsonNode payload) =>
        payload["actions"]?[0]?["value"]?.GetValue<string>() ?? string.Empty;

    private static bool TryParseRetryValue(
        string value, out int ticketId, out string project)
    {
        ticketId = 0;
        project = string.Empty;

        var parts = value.Split(':', 2);
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0], out ticketId)) return false;

        project = parts[1];
        return !string.IsNullOrEmpty(project);
    }
}
