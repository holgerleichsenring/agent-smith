using AgentSmith.Dispatcher.Adapters;
using AgentSmith.Dispatcher.Handlers;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services;
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
        return app;
    }

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

        if (json["type"]?.GetValue<string>() != "block_actions") return Results.Ok();

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
