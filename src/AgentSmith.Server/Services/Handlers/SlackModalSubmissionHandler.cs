using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles Slack modal view_submission events. Extracts structured
/// values and routes to the appropriate intent handler.
/// </summary>
internal sealed class SlackModalSubmissionHandler(
    FixTicketIntentHandler fixHandler,
    ListTicketsIntentHandler listHandler,
    CreateTicketIntentHandler createHandler,
    InitProjectIntentHandler initHandler,
    IPlatformAdapter adapter,
    ILogger<SlackModalSubmissionHandler> logger)
{
    public async Task HandleAsync(JsonNode payload, CancellationToken ct)
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
            if (values is null) return;

            var commandValue = values[DispatcherDefaults.SlackBlockCommand]
                ?[DispatcherDefaults.SlackActionCommand]
                ?["selected_option"]?["value"]?.GetValue<string>();

            var command = SlackModalBuilder.ParseCommandValue(commandValue);
            if (command is null)
            {
                await adapter.SendMessageAsync(channelId, ":x: Invalid command selection. Please try again.", ct);
                return;
            }

            var project = ExtractProject(values, payload);
            if (string.IsNullOrWhiteSpace(project))
            {
                await adapter.SendMessageAsync(channelId, ":x: Please select a project.", ct);
                return;
            }

            await RouteCommandAsync(command.Value, values, project, userId, channelId, ct);
        }
        catch (Exception ex) { logger.LogError(ex, "Error handling modal submission"); }
    }

    private async Task RouteCommandAsync(
        ModalCommandType command, JsonNode values,
        string project, string userId, string channelId, CancellationToken ct)
    {
        switch (command)
        {
            case ModalCommandType.FixBug or ModalCommandType.FixBugNoTests
                or ModalCommandType.AddFeature or ModalCommandType.MadDiscussion:
                await HandleFixTicketAsync(command, values, project, userId, channelId, ct);
                break;
            case ModalCommandType.SecurityReview:
                await fixHandler.HandleAsync(
                    ModalIntentFactory.CreatePipelineIntent("security-scan", project, userId, channelId), ct);
                break;
            case ModalCommandType.LegalAnalysis:
                await fixHandler.HandleAsync(
                    ModalIntentFactory.CreatePipelineIntent("legal-analysis", project, userId, channelId), ct);
                break;
            case ModalCommandType.ListTickets:
                await listHandler.HandleAsync(
                    ModalIntentFactory.CreateListIntent(project, userId, channelId), ct);
                break;
            case ModalCommandType.CreateTicket:
                await HandleCreateTicketAsync(values, project, userId, channelId, ct);
                break;
            case ModalCommandType.InitProject:
                await initHandler.HandleAsync(
                    ModalIntentFactory.CreateInitIntent(project, userId, channelId), ct);
                break;
        }
    }

    private async Task HandleFixTicketAsync(
        ModalCommandType command, JsonNode values,
        string project, string userId, string channelId, CancellationToken ct)
    {
        var ticketIdStr = values[DispatcherDefaults.SlackBlockTicket]
            ?[DispatcherDefaults.SlackActionTicket]
            ?["selected_option"]?["value"]?.GetValue<string>();

        if (!int.TryParse(ticketIdStr, out var ticketId))
        {
            await adapter.SendMessageAsync(channelId, ":x: Please select a ticket.", ct);
            return;
        }

        var intent = ModalIntentFactory.CreateFixIntent(ticketId, command, project, userId, channelId);
        logger.LogInformation("Modal: {Command} #{TicketId} in {Project}", command, ticketId, project);
        await fixHandler.HandleAsync(intent, ct);
    }

    private async Task HandleCreateTicketAsync(
        JsonNode values, string project, string userId, string channelId, CancellationToken ct)
    {
        var title = values[DispatcherDefaults.SlackBlockTitle]?["title_input"]?["value"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(title))
        {
            await adapter.SendMessageAsync(channelId, ":x: Please enter a ticket title.", ct);
            return;
        }

        var description = values[DispatcherDefaults.SlackBlockDescription]?["desc_input"]?["value"]?.GetValue<string>();
        var intent = ModalIntentFactory.CreateCreateIntent(project, title, description, userId, channelId);
        await createHandler.HandleAsync(intent, ct);
    }

    private static string? ExtractProject(JsonNode values, JsonNode payload)
    {
        var project = values[DispatcherDefaults.SlackBlockProject]
            ?[DispatcherDefaults.SlackActionProject]
            ?["selected_option"]?["value"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(project) ? ExtractProjectFromMetadata(payload) : project;
    }

    private static string? ExtractProjectFromMetadata(JsonNode payload)
    {
        var metadataStr = payload["view"]?["private_metadata"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(metadataStr)) return null;
        try { return JsonNode.Parse(metadataStr)?["selected_project"]?.GetValue<string>(); }
        catch (JsonException) { return null; }
    }

    private static (string ChannelId, string UserId) ExtractPrivateMetadata(JsonNode payload)
    {
        var metadataStr = payload["view"]?["private_metadata"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(metadataStr)) return (string.Empty, string.Empty);
        try
        {
            var metadata = JsonNode.Parse(metadataStr);
            return (metadata?["channel_id"]?.GetValue<string>() ?? string.Empty,
                    metadata?["user_id"]?.GetValue<string>() ?? string.Empty);
        }
        catch (JsonException) { return (string.Empty, string.Empty); }
    }
}
