using AgentSmith.Contracts.Services;
using AgentSmith.Dispatcher.Services.Adapters;
using AgentSmith.Dispatcher.Services.Handlers;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Dispatcher.Extensions;

internal static class WebApplicationExtensions
{
    internal static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTimeOffset.UtcNow }));
        return app;
    }

    internal static WebApplication MapSlackEndpoints(this WebApplication app)
    {
        app.MapPost("/slack/events", HandleSlackEventsAsync);
        app.MapPost("/slack/interact", HandleSlackInteractAsync);
        app.MapPost("/slack/commands", HandleSlackCommandAsync);
        app.MapPost("/slack/options", HandleSlackOptionsAsync);
        return app;
    }

    // --- Events endpoint (existing) ---

    private static async Task<IResult> HandleSlackEventsAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Dispatcher.SlackEvents");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request))
        {
            logger.LogWarning("Slack signature verification failed");
            return Results.Unauthorized();
        }

        var body = await ReadBodyAsync(ctx.Request);
        logger.LogInformation("Slack event body ({Length} chars): {Body}", body.Length, body);

        var json = JsonNode.Parse(body);
        if (json is null) return Results.BadRequest();

        var outerType = json["type"]?.GetValue<string>();
        if (outerType == "url_verification")
            return Results.Ok(new { challenge = json["challenge"]?.GetValue<string>() ?? string.Empty });

        if (outerType != "event_callback")
        {
            logger.LogWarning("Ignoring Slack event with type={Type}", outerType);
            return Results.Ok();
        }

        var (text, userId, channelId) = ExtractEventFields(json);
        if (string.IsNullOrWhiteSpace(text))
        {
            var eventNode = json["event"];
            logger.LogWarning("ExtractEventFields returned empty text. event_type={EventType}, bot_id={BotId}, subtype={Subtype}",
                eventNode?["type"]?.GetValue<string>(),
                eventNode?["bot_id"]?.GetValue<string>(),
                eventNode?["subtype"]?.GetValue<string>());
            return Results.Ok();
        }

        logger.LogInformation("Dispatching Slack message: text={Text}, user={UserId}, channel={ChannelId}", text, userId, channelId);

        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<SlackMessageDispatcher>()
                    .DispatchAsync(text, userId, channelId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fire-and-forget dispatch failed");
            }
        });

        return Results.Ok();
    }

    // --- Interaction endpoint (extended with modal support) ---

    private static async Task<IResult> HandleSlackInteractAsync(HttpContext ctx)
    {
        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request))
            return Results.Unauthorized();

        var body = await ReadBodyAsync(ctx.Request);
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var payloadJson = form["payload"];

        if (string.IsNullOrWhiteSpace(payloadJson)) return Results.BadRequest();

        var json = JsonNode.Parse(payloadJson);
        if (json is null) return Results.BadRequest();

        var interactionType = json["type"]?.GetValue<string>();

        // Modal submission — route to SlackModalSubmissionHandler
        if (interactionType == "view_submission")
        {
            var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider
                        .GetRequiredService<SlackModalSubmissionHandler>()
                        .HandleAsync(json);
                }
                catch (Exception ex)
                {
                    var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("AgentSmith.Dispatcher.SlackModal");
                    logger.LogError(ex, "Modal submission handling failed");
                }
            });

            return Results.Ok();
        }

        // Block actions — could be channel button OR modal dropdown change
        if (interactionType == "block_actions")
        {
            if (json["view"] is not null)
                return await HandleModalBlockActionAsync(json, ctx);

            // Channel button interaction (existing behavior)
            var (channelId, questionId, answer) = ExtractInteractionFields(json);
            if (questionId is null) return Results.Ok();

            var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<SlackInteractionHandler>()
                    .HandleAsync(channelId, questionId, answer, json);
            });

            return Results.Ok();
        }

        return Results.Ok();
    }

    // --- Slash command endpoint (new) ---

    private static async Task<IResult> HandleSlackCommandAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Dispatcher.SlackCommands");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request))
        {
            logger.LogWarning("Slack signature verification failed for slash command");
            return Results.Unauthorized();
        }

        var body = await ReadBodyAsync(ctx.Request);
        var form = System.Web.HttpUtility.ParseQueryString(body);

        var triggerId = form["trigger_id"] ?? string.Empty;
        var userId = form["user_id"] ?? string.Empty;
        var channelId = form["channel_id"] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(triggerId))
        {
            logger.LogWarning("Slash command received without trigger_id");
            return Results.BadRequest();
        }

        logger.LogInformation("Slash command from user={UserId} channel={ChannelId}", userId, channelId);

        var privateMetadata = JsonSerializer.Serialize(
            new { channel_id = channelId, user_id = userId });

        var view = SlackModalBuilder.BuildInitialView(privateMetadata);

        var adapter = ctx.RequestServices.GetRequiredService<SlackAdapter>();
        await adapter.OpenViewAsync(triggerId, view);

        return Results.Ok();
    }

    // --- Dynamic options endpoint (new) ---

    private static async Task<IResult> HandleSlackOptionsAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Dispatcher.SlackOptions");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request))
            return Results.Unauthorized();

        var body = await ReadBodyAsync(ctx.Request);
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var payloadJson = form["payload"];

        if (string.IsNullOrWhiteSpace(payloadJson)) return Results.BadRequest();

        var json = JsonNode.Parse(payloadJson);
        if (json is null) return Results.BadRequest();

        var actionId = json["action_id"]?.GetValue<string>() ?? string.Empty;
        var searchQuery = json["value"]?.GetValue<string>();

        if (actionId == DispatcherDefaults.SlackActionProject)
        {
            var configLoader = ctx.RequestServices.GetRequiredService<IConfigurationLoader>();
            var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
            var projectNames = config.Projects.Keys.ToList();

            var result = SlackModalBuilder.BuildProjectOptions(projectNames, searchQuery);
            return Results.Json(result);
        }

        if (actionId == DispatcherDefaults.SlackActionTicket)
        {
            var selectedProject = ExtractSelectedProjectFromOptionsPayload(json);
            if (string.IsNullOrWhiteSpace(selectedProject))
                return Results.Json(new { options = Array.Empty<object>() });

            var ticketSearch = ctx.RequestServices.GetRequiredService<CachedTicketSearch>();
            var tickets = await ticketSearch.SearchAsync(selectedProject, searchQuery);

            var result = SlackModalBuilder.BuildTicketOptions(tickets, searchQuery: null);
            return Results.Json(result);
        }

        logger.LogWarning("Unknown options action_id: {ActionId}", actionId);
        return Results.Json(new { options = Array.Empty<object>() });
    }

    // --- Modal block_actions handler (conditional field visibility) ---

    private static async Task<IResult> HandleModalBlockActionAsync(JsonNode json, HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Dispatcher.SlackModal");

        var actionId = json["actions"]?[0]?["action_id"]?.GetValue<string>() ?? string.Empty;

        if (actionId != DispatcherDefaults.SlackActionCommand)
            return Results.Ok();

        var selectedValue = json["actions"]?[0]?["selected_option"]?["value"]?.GetValue<string>();
        var command = SlackModalBuilder.ParseCommandValue(selectedValue);
        if (command is null)
            return Results.Ok();

        var viewId = json["view"]?["id"]?.GetValue<string>() ?? string.Empty;
        var privateMetadata = json["view"]?["private_metadata"]?.GetValue<string>() ?? "{}";
        var selectedProject = ExtractSelectedProjectFromViewState(json);

        var configLoader = ctx.RequestServices.GetRequiredService<IConfigurationLoader>();
        var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
        var pipelineNames = config.Pipelines.Keys.ToList();

        var updatedView = SlackModalBuilder.BuildUpdatedView(
            command.Value, privateMetadata, selectedProject, pipelineNames);

        var adapter = ctx.RequestServices.GetRequiredService<SlackAdapter>();
        await adapter.UpdateViewAsync(viewId, updatedView);

        logger.LogInformation("Updated modal view {ViewId} for command {Command}", viewId, command);

        return Results.Ok();
    }

    // --- Extractors ---

    private static (string text, string userId, string channelId) ExtractEventFields(JsonNode json)
    {
        var eventNode = json["event"];
        var eventType = eventNode?["type"]?.GetValue<string>();

        if (eventType != "message" && eventType != "app_mention")
            return (string.Empty, string.Empty, string.Empty);

        if (!string.IsNullOrWhiteSpace(eventNode?["bot_id"]?.GetValue<string>()))
            return (string.Empty, string.Empty, string.Empty);

        var rawText = eventNode?["text"]?.GetValue<string>() ?? string.Empty;
        var text = StripMention(rawText);
        var userId = eventNode?["user"]?.GetValue<string>() ?? string.Empty;
        var channelId = eventNode?["channel"]?.GetValue<string>() ?? string.Empty;

        return (text, userId, channelId);
    }

    private static (string channelId, string? questionId, string answer) ExtractInteractionFields(JsonNode json)
    {
        var channelId = json["channel"]?["id"]?.GetValue<string>() ?? string.Empty;
        var actionId = json["actions"]?[0]?["action_id"]?.GetValue<string>() ?? string.Empty;

        var separatorIndex = actionId.LastIndexOf(':');
        if (separatorIndex < 0) return (channelId, null, string.Empty);

        var questionId = actionId[..separatorIndex];
        var answer = actionId[(separatorIndex + 1)..];

        return (channelId, questionId, answer);
    }

    private static string? ExtractSelectedProjectFromViewState(JsonNode json)
    {
        return json["view"]?["state"]?["values"]
            ?[DispatcherDefaults.SlackBlockProject]
            ?[DispatcherDefaults.SlackActionProject]
            ?["selected_option"]?["value"]?.GetValue<string>();
    }

    private static string? ExtractSelectedProjectFromOptionsPayload(JsonNode json)
    {
        // In options payload, the view state is accessible via view.state.values
        return json["view"]?["state"]?["values"]
            ?[DispatcherDefaults.SlackBlockProject]
            ?[DispatcherDefaults.SlackActionProject]
            ?["selected_option"]?["value"]?.GetValue<string>();
    }

    private static string StripMention(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("<@", StringComparison.Ordinal)) return trimmed;

        var end = trimmed.IndexOf('>', 2);
        return end >= 0 ? trimmed[(end + 1)..].TrimStart() : trimmed;
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return body;
    }
}
