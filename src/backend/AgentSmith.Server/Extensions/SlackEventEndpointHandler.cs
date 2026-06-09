using AgentSmith.Contracts.Events;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using System.Text.Json.Nodes;

namespace AgentSmith.Server.Extensions;

internal static class SlackEventEndpointHandler
{
    internal static async Task<IResult> HandleAsync(HttpContext ctx)
    {
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SlackEvents");

        var options = ctx.RequestServices.GetRequiredService<SlackAdapterOptions>();
        var verifier = new SlackSignatureVerifier(options.SigningSecret);

        if (!await verifier.VerifyAsync(ctx.Request, ctx.RequestAborted))
        {
            logger.LogWarning("Slack signature verification failed");
            await EmitChatAsync(ctx, channel: "?", messageType: "unknown",
                actioned: false, skipReason: "signature-invalid");
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
            await EmitChatAsync(ctx, channel: "?", messageType: outerType ?? "unknown",
                actioned: false, skipReason: "non-event-callback");
            return Results.Ok();
        }

        var (text, userId, channelId) = SlackPayloadExtractor.ExtractEventFields(json);
        var innerType = json["event"]?["type"]?.GetValue<string>() ?? "event_callback";
        if (string.IsNullOrWhiteSpace(text))
        {
            var eventNode = json["event"];
            logger.LogWarning("ExtractEventFields returned empty text. event_type={EventType}, bot_id={BotId}, subtype={Subtype}",
                eventNode?["type"]?.GetValue<string>(),
                eventNode?["bot_id"]?.GetValue<string>(),
                eventNode?["subtype"]?.GetValue<string>());
            await EmitChatAsync(ctx, channel: channelId ?? "?", messageType: innerType,
                actioned: false, skipReason: "empty-text");
            return Results.Ok();
        }

        logger.LogInformation("Dispatching Slack message: text={Text}, user={UserId}, channel={ChannelId}", text, userId, channelId);
        await EmitChatAsync(ctx, channel: channelId ?? "?", messageType: innerType,
            actioned: true, skipReason: null);

        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<SlackMessageDispatcher>()
                    .DispatchAsync(text, userId, channelId ?? "?", CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fire-and-forget dispatch failed");
            }
        });

        return Results.Ok();
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
                Source: "chat:slack",
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
}
