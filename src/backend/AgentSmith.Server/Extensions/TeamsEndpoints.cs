using System.Text.Json.Nodes;
using AgentSmith.Contracts.Events;
using AgentSmith.Server.Services.Adapters;

namespace AgentSmith.Server.Extensions;

internal static class TeamsEndpoints
{
    internal static WebApplication MapTeamsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/teams/messages", (Delegate)HandleTeamsActivityAsync);
        return app;
    }

    private static async Task<IResult> HandleTeamsActivityAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.TeamsActivity");

        var authHeader = ctx.Request.Headers.Authorization.ToString();
        var validator = ctx.RequestServices.GetRequiredService<TeamsJwtValidator>();

        if (!await validator.ValidateAsync(authHeader, ctx.RequestAborted))
        {
            logger.LogWarning("Teams JWT validation failed");
            await EmitChatAsync(ctx, channel: "?", messageType: "unknown",
                actioned: false, skipReason: "jwt-invalid");
            return Results.Unauthorized();
        }

        var body = await EndpointHelpers.ReadBodyAsync(ctx.Request);
        var json = JsonNode.Parse(body);
        if (json is null) return Results.BadRequest();

        var activityType = json["type"]?.GetValue<string>() ?? "";
        var conversationId = json["conversation"]?["id"]?.GetValue<string>() ?? "";
        var serviceUrl = json["serviceUrl"]?.GetValue<string>() ?? "";
        var fromId = json["from"]?["id"]?.GetValue<string>() ?? "";

        var adapter = ctx.RequestServices.GetRequiredService<TeamsAdapter>();
        if (!string.IsNullOrWhiteSpace(serviceUrl))
            adapter.RegisterServiceUrl(conversationId, serviceUrl);

        switch (activityType)
        {
            case "message":
                return await HandleMessageAsync(json, ctx, logger, fromId, conversationId);

            case "invoke":
                return await HandleInvokeAsync(json, ctx, logger, fromId, conversationId);

            default:
                logger.LogDebug("Ignoring Teams activity type={Type}", activityType);
                await EmitChatAsync(ctx, channel: conversationId, messageType: activityType,
                    actioned: false, skipReason: "non-message-activity");
                return Results.Ok();
        }
    }

    // p0173c: emit chat-channel ingestion as a SystemEvent. Metadata only —
    // no message text in the payload (security boundary, see decisions).
    private static async Task EmitChatAsync(
        HttpContext ctx, string channel, string messageType, bool actioned, string? skipReason)
    {
        var publisher = ctx.RequestServices.GetService<ISystemEventPublisher>();
        if (publisher is null) return;
        try
        {
            await publisher.PublishAsync(new ChatMessageReceivedEvent(
                Source: "chat:teams",
                Channel: channel,
                MessageType: messageType,
                Actioned: actioned,
                SkipReason: skipReason,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch
        {
            /* fire-and-warn — never break the HTTP response on a publish failure */
        }
    }

    private static async Task<IResult> HandleMessageAsync(
        JsonNode json, HttpContext ctx, ILogger logger,
        string fromId, string conversationId)
    {
        var text = json["text"]?.GetValue<string>()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            await EmitChatAsync(ctx, channel: conversationId, messageType: "message",
                actioned: false, skipReason: "empty-text");
            return Results.Ok();
        }

        text = StripTeamsMention(text, json);

        logger.LogInformation("Teams message from {FromId} in {ConversationId}: {Text}",
            fromId, conversationId, text);
        await EmitChatAsync(ctx, channel: conversationId, messageType: "message",
            actioned: true, skipReason: null);

        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<SlackMessageDispatcher>()
                    .DispatchAsync(text, fromId, conversationId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Teams fire-and-forget dispatch failed");
            }
        });

        return Results.Ok();
    }

    private static async Task<IResult> HandleInvokeAsync(
        JsonNode json, HttpContext ctx, ILogger logger,
        string fromId, string conversationId)
    {
        var value = json["value"];
        if (value is null) return Results.Ok();

        logger.LogInformation("Teams invoke from {FromId} in {ConversationId}", fromId, conversationId);

        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<TeamsInteractionHandler>()
                    .HandleAsync(conversationId, fromId, value, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Teams interaction handling failed");
            }
        });

        return Results.Json(new { status = 200 });
    }

    private static string StripTeamsMention(string text, JsonNode activity)
    {
        var entities = activity["entities"]?.AsArray();
        if (entities is null) return text;

        foreach (var entity in entities)
        {
            if (entity?["type"]?.GetValue<string>() != "mention") continue;

            var mentioned = entity?["text"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(mentioned))
                text = text.Replace(mentioned, "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        return text;
    }
}
