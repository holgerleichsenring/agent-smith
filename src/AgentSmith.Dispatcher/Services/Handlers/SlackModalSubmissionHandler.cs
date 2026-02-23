using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services.Adapters;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Dispatcher.Services.Handlers;

/// <summary>
/// Handles Slack modal view_submission events.
/// Extracts structured values from the modal state and routes directly
/// to the appropriate intent handler — no IntentEngine needed.
/// </summary>
internal sealed class SlackModalSubmissionHandler(
    FixTicketIntentHandler fixHandler,
    ListTicketsIntentHandler listHandler,
    CreateTicketIntentHandler createHandler,
    InitProjectIntentHandler initHandler,
    IPlatformAdapter adapter,
    ILogger<SlackModalSubmissionHandler> logger)
{
    public async Task HandleAsync(JsonNode payload, CancellationToken ct = default)
    {
        try
        {
            var (channelId, userId) = ExtractPrivateMetadata(payload);
            if (string.IsNullOrWhiteSpace(channelId))
            {
                logger.LogWarning("Modal submission missing channel_id in private_metadata");
                return;
            }

            var values = payload["view"]?["state"]?["values"];
            if (values is null)
            {
                logger.LogWarning("Modal submission has no state values");
                return;
            }

            var commandValue = values
                [DispatcherDefaults.SlackBlockCommand]
                ?[DispatcherDefaults.SlackActionCommand]
                ?["selected_option"]?["value"]?.GetValue<string>();

            var command = SlackModalBuilder.ParseCommandValue(commandValue);
            if (command is null)
            {
                await adapter.SendMessageAsync(channelId,
                    ":x: Invalid command selection. Please try again.", ct);
                return;
            }

            var project = values
                [DispatcherDefaults.SlackBlockProject]
                ?[DispatcherDefaults.SlackActionProject]
                ?["selected_option"]?["value"]?.GetValue<string>();

            // Fallback: external_select may not be in state — read from private_metadata
            if (string.IsNullOrWhiteSpace(project))
                project = ExtractProjectFromMetadata(payload);

            if (string.IsNullOrWhiteSpace(project))
            {
                await adapter.SendMessageAsync(channelId,
                    ":x: Please select a project.", ct);
                return;
            }

            await RouteCommandAsync(command.Value, values, project, userId, channelId, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling modal submission");
        }
    }

    private async Task RouteCommandAsync(
        ModalCommandType command, JsonNode values,
        string project, string userId, string channelId,
        CancellationToken ct)
    {
        switch (command)
        {
            case ModalCommandType.FixTicket:
                await HandleFixTicketAsync(values, project, userId, channelId, ct);
                break;

            case ModalCommandType.ListTickets:
                await HandleListTicketsAsync(project, userId, channelId, ct);
                break;

            case ModalCommandType.CreateTicket:
                await HandleCreateTicketAsync(values, project, userId, channelId, ct);
                break;

            case ModalCommandType.InitProject:
                await HandleInitProjectAsync(project, userId, channelId, ct);
                break;
        }
    }

    private async Task HandleFixTicketAsync(
        JsonNode values, string project, string userId, string channelId,
        CancellationToken ct)
    {
        var ticketIdStr = values
            [DispatcherDefaults.SlackBlockTicket]
            ?[DispatcherDefaults.SlackActionTicket]
            ?["selected_option"]?["value"]?.GetValue<string>();

        if (!int.TryParse(ticketIdStr, out var ticketId))
        {
            await adapter.SendMessageAsync(channelId,
                ":x: Please select a ticket.", ct);
            return;
        }

        var pipelineOverride = values
            [DispatcherDefaults.SlackBlockPipeline]
            ?[DispatcherDefaults.SlackActionPipeline]
            ?["selected_option"]?["value"]?.GetValue<string>();

        var intent = new FixTicketIntent
        {
            TicketId = ticketId,
            Project = project,
            PipelineOverride = pipelineOverride,
            RawText = $"/fix #{ticketId} in {project}",
            UserId = userId,
            ChannelId = channelId,
            Platform = DispatcherDefaults.PlatformSlack
        };

        logger.LogInformation("Modal submission: fix #{TicketId} in {Project} (pipeline={Pipeline})",
            ticketId, project, pipelineOverride ?? "default");

        await fixHandler.HandleAsync(intent, ct);
    }

    private async Task HandleListTicketsAsync(
        string project, string userId, string channelId,
        CancellationToken ct)
    {
        var intent = new ListTicketsIntent
        {
            Project = project,
            RawText = $"/agentsmith list tickets in {project}",
            UserId = userId,
            ChannelId = channelId,
            Platform = DispatcherDefaults.PlatformSlack
        };

        logger.LogInformation("Modal submission: list tickets in {Project}", project);
        await listHandler.HandleAsync(intent, ct);
    }

    private async Task HandleCreateTicketAsync(
        JsonNode values, string project, string userId, string channelId,
        CancellationToken ct)
    {
        var title = values
            [DispatcherDefaults.SlackBlockTitle]
            ?["title_input"]?["value"]?.GetValue<string>();

        if (string.IsNullOrWhiteSpace(title))
        {
            await adapter.SendMessageAsync(channelId,
                ":x: Please enter a ticket title.", ct);
            return;
        }

        var description = values
            [DispatcherDefaults.SlackBlockDescription]
            ?["desc_input"]?["value"]?.GetValue<string>();

        var intent = new CreateTicketIntent
        {
            Project = project,
            Title = title,
            Description = description,
            RawText = $"/agentsmith create ticket \"{title}\" in {project}",
            UserId = userId,
            ChannelId = channelId,
            Platform = DispatcherDefaults.PlatformSlack
        };

        logger.LogInformation("Modal submission: create ticket in {Project}: {Title}", project, title);
        await createHandler.HandleAsync(intent, ct);
    }

    private async Task HandleInitProjectAsync(
        string project, string userId, string channelId,
        CancellationToken ct)
    {
        var intent = new InitProjectIntent
        {
            Project = project,
            RawText = $"/agentsmith init {project}",
            UserId = userId,
            ChannelId = channelId,
            Platform = DispatcherDefaults.PlatformSlack
        };

        logger.LogInformation("Modal submission: init project {Project}", project);
        await initHandler.HandleAsync(intent, ct);
    }

    private static string? ExtractProjectFromMetadata(JsonNode payload)
    {
        var metadataStr = payload["view"]?["private_metadata"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(metadataStr)) return null;
        try
        {
            var metadata = JsonNode.Parse(metadataStr);
            return metadata?["selected_project"]?.GetValue<string>();
        }
        catch (JsonException) { return null; }
    }

    private static (string ChannelId, string UserId) ExtractPrivateMetadata(JsonNode payload)
    {
        var metadataStr = payload["view"]?["private_metadata"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(metadataStr))
            return (string.Empty, string.Empty);

        try
        {
            var metadata = JsonNode.Parse(metadataStr);
            var channelId = metadata?["channel_id"]?.GetValue<string>() ?? string.Empty;
            var userId = metadata?["user_id"]?.GetValue<string>() ?? string.Empty;
            return (channelId, userId);
        }
        catch (JsonException)
        {
            return (string.Empty, string.Empty);
        }
    }
}
