using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Extensions;

internal static class SlackEndpoints
{
    internal static WebApplication MapSlackEndpoints(this WebApplication app)
    {
        app.MapPost("/slack/events", (Delegate)HandleSlackEventsAsync);
        app.MapPost("/slack/interact", (Delegate)HandleSlackInteractAsync);
        app.MapPost("/slack/commands", (Delegate)HandleSlackCommandAsync);
        app.MapPost("/slack/options", (Delegate)HandleSlackOptionsAsync);
        return app;
    }

    private static async Task<IResult> HandleSlackEventsAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SlackEvents");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request, ctx.RequestAborted))
        {
            logger.LogWarning("Slack signature verification failed");
            return Results.Unauthorized();
        }

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
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

        var (text, userId, channelId) = SlackPayloadExtractor.ExtractEventFields(json);
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
                    .DispatchAsync(text, userId, channelId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fire-and-forget dispatch failed");
            }
        });

        return Results.Ok();
    }

    private static async Task<IResult> HandleSlackInteractAsync(HttpContext ctx)
    {
        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request, ctx.RequestAborted))
            return Results.Unauthorized();

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var payloadJson = form["payload"];

        if (string.IsNullOrWhiteSpace(payloadJson)) return Results.BadRequest();

        var json = JsonNode.Parse(payloadJson);
        if (json is null) return Results.BadRequest();

        var interactionType = json["type"]?.GetValue<string>();

        if (interactionType == "view_submission")
            return await HandleViewSubmissionAsync(json, ctx);

        if (interactionType == "block_actions")
            return await HandleBlockActionsAsync(json, ctx);

        return Results.Ok();
    }

    private static async Task<IResult> HandleViewSubmissionAsync(JsonNode json, HttpContext ctx)
    {
        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<SlackModalSubmissionHandler>()
                    .HandleAsync(json, CancellationToken.None);
            }
            catch (Exception ex)
            {
                var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("AgentSmith.Server.SlackModal");
                logger.LogError(ex, "Modal submission handling failed");
            }
        });

        return Results.Ok();
    }

    private static async Task<IResult> HandleBlockActionsAsync(JsonNode json, HttpContext ctx)
    {
        if (json["view"] is not null)
            return await SlackModalActionHandler.HandleAsync(json, ctx);

        var (channelId, questionId, answer) = SlackPayloadExtractor.ExtractInteractionFields(json);
        if (questionId is null) return Results.Ok();

        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<SlackInteractionHandler>()
                .HandleAsync(channelId, questionId, answer, json, CancellationToken.None);
        });

        return Results.Ok();
    }

    private static async Task<IResult> HandleSlackCommandAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SlackCommands");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request, ctx.RequestAborted))
        {
            logger.LogWarning("Slack signature verification failed for slash command");
            return Results.Unauthorized();
        }

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
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
        await adapter.OpenViewAsync(triggerId, view, ctx.RequestAborted);

        return Results.Ok();
    }

    private static async Task<IResult> HandleSlackOptionsAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SlackOptions");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request, ctx.RequestAborted))
            return Results.Unauthorized();

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
        var form = System.Web.HttpUtility.ParseQueryString(body);
        var payloadJson = form["payload"];

        if (string.IsNullOrWhiteSpace(payloadJson)) return Results.BadRequest();

        var json = JsonNode.Parse(payloadJson);
        if (json is null) return Results.BadRequest();

        var actionId = json["action_id"]?.GetValue<string>() ?? string.Empty;
        var searchQuery = json["value"]?.GetValue<string>();

        if (actionId == DispatcherDefaults.SlackActionProject)
        {
            var configLoader = ctx.RequestServices.GetRequiredService<AgentSmith.Contracts.Services.IConfigurationLoader>();
            var config = configLoader.LoadConfig(DispatcherDefaults.ConfigPath);
            var projectNames = config.Projects.Keys.ToList();

            var result = SlackModalBuilder.BuildProjectOptions(projectNames, searchQuery);
            return Results.Json(result);
        }

        if (actionId == DispatcherDefaults.SlackActionTicket)
        {
            var selectedProject = SlackPayloadExtractor.ExtractSelectedProjectFromOptionsPayload(json);
            if (string.IsNullOrWhiteSpace(selectedProject))
                return Results.Json(new { options = Array.Empty<object>() });

            var ticketSearch = ctx.RequestServices.GetRequiredService<CachedTicketSearch>();
            var tickets = await ticketSearch.SearchAsync(selectedProject, searchQuery, ctx.RequestAborted);

            var result = SlackModalBuilder.BuildTicketOptions(tickets, searchQuery: null);
            return Results.Json(result);
        }

        logger.LogWarning("Unknown options action_id: {ActionId}", actionId);
        return Results.Json(new { options = Array.Empty<object>() });
    }
}
