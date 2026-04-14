using System.Text.Json.Nodes;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;

namespace AgentSmith.Server.Extensions;

internal static class SlackModalActionHandler
{
    internal static async Task<IResult> HandleAsync(JsonNode json, HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SlackModal");

        var actionId = json["actions"]?[0]?["action_id"]?.GetValue<string>() ?? string.Empty;

        if (actionId == DispatcherDefaults.SlackActionProject)
            return await HandleProjectSelectionAsync(json, ctx, logger);

        if (actionId != DispatcherDefaults.SlackActionCommand)
            return Results.Ok();

        var selectedValue = json["actions"]?[0]?["selected_option"]?["value"]?.GetValue<string>();
        var command = SlackModalBuilder.ParseCommandValue(selectedValue);
        if (command is null)
            return Results.Ok();

        var viewId = json["view"]?["id"]?.GetValue<string>() ?? string.Empty;
        var metadata = SlackPayloadExtractor.GetMetadata(json);
        var selectedProject = metadata.SelectedProject
            ?? SlackPayloadExtractor.ExtractSelectedProjectFromViewState(json);

        var updatedView = SlackModalBuilder.BuildUpdatedView(
            command.Value, SlackPayloadExtractor.SerializeMetadata(metadata), selectedProject);

        var adapter = ctx.RequestServices.GetRequiredService<SlackAdapter>();
        await adapter.UpdateViewAsync(viewId, updatedView, ctx.RequestAborted);

        logger.LogInformation("Updated modal view {ViewId} for command {Command}", viewId, command);

        return Results.Ok();
    }

    private static async Task<IResult> HandleProjectSelectionAsync(
        JsonNode json, HttpContext ctx, ILogger logger)
    {
        var selectedProject = json["actions"]?[0]?["selected_option"]?["value"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(selectedProject))
            return Results.Ok();

        var viewId = json["view"]?["id"]?.GetValue<string>() ?? string.Empty;
        var metadata = SlackPayloadExtractor.GetMetadata(json);
        metadata.SelectedProject = selectedProject;

        var commandValue = json["view"]?["state"]?["values"]
            ?[DispatcherDefaults.SlackBlockCommand]
            ?[DispatcherDefaults.SlackActionCommand]
            ?["selected_option"]?["value"]?.GetValue<string>();
        var command = SlackModalBuilder.ParseCommandValue(commandValue) ?? ModalCommandType.FixBug;

        var updatedView = SlackModalBuilder.BuildUpdatedView(
            command, SlackPayloadExtractor.SerializeMetadata(metadata), selectedProject);

        var adapter = ctx.RequestServices.GetRequiredService<SlackAdapter>();
        await adapter.UpdateViewAsync(viewId, updatedView, ctx.RequestAborted);

        logger.LogInformation("Stored selected project {Project} in modal {ViewId}", selectedProject, viewId);

        return Results.Ok();
    }
}
