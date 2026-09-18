using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-09-15-9033: the dashboard channel's ingestion route — one message in, routed
/// through the same spec-dialog router Slack and Teams reach. The signed-in principal is
/// the session's owner, and a message for a dialog owned by someone else is refused here
/// rather than served an empty transcript.
/// </summary>
internal static class SpecDialogEndpoints
{
    private const string ChatSource = "chat:dashboard";

    internal static WebApplication MapSpecDialogEndpoints(this WebApplication app)
    {
        app.MapPost("/api/spec-dialog/messages", (Delegate)IngestAsync)
           .Needs(Security.Permissions.DialogWrite);
        return app;
    }

    /// <summary>One message from a browser: the dialog id the page minted, and the text.</summary>
    internal sealed record SpecDialogMessageRequest(string DialogId, string Text);

    internal static async Task<IResult> IngestAsync(
        SpecDialogMessageRequest body, HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(body.DialogId) || string.IsNullOrWhiteSpace(body.Text))
            return Results.BadRequest("dialogId and text are required");

        var ownership = ctx.RequestServices.GetRequiredService<SpecDialogOwnership>();
        var owner = ownership.OwnerOf(ctx.User);
        if (!await ownership.MayPostAsync(body.DialogId, body.Text, owner, ctx.RequestAborted))
        {
            await EmitChatAsync(ctx, body.DialogId, actioned: false, skipReason: "not-the-owner");
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        await EmitChatAsync(ctx, body.DialogId, actioned: true, skipReason: null);
        Dispatch(ctx, body.DialogId, body.Text.Trim(), owner, MayStartRuns(ctx));
        return Results.Accepted();
    }

    /// <summary>
    /// 2026-09-17-042eg: posting here needs dialog.write only, but approving MOVES the filed work
    /// ticket into a trigger status, and that starts a run — which is runs.control everywhere else
    /// in this server. It is sampled when the turn starts, on the principal that started it: the
    /// dialog's owner is the only one who may post into it, so that principal is the approver.
    /// A non-enforcing installation holds everyone to nothing, exactly as the hub filter reads it.
    /// </summary>
    internal static bool MayStartRuns(HttpContext ctx)
    {
        var auth = ctx.RequestServices.GetRequiredService<TokenAuthorityConfig>();
        if (!auth.Enforce) return true;
        return ctx.RequestServices.GetRequiredService<Security.CallerIdentityResolver>()
            .Resolve(ctx.User).Permissions.Contains(Security.Permissions.RunsControl);
    }

    /// <summary>
    /// Fire-and-forget on a FRESH scope, the way both chat channels dispatch. A design
    /// turn runs a master and the approval gate waits up to fifteen minutes: awaiting it
    /// here would hold the connection for the whole turn, and passing the request's token
    /// would abort the conversation the moment the tab closed.
    /// </summary>
    private static void Dispatch(
        HttpContext ctx, string dialogId, string text, string owner, bool mayStartRuns)
    {
        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SpecDialogIngestion");
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DashboardDialogDispatcher>()
                    .DispatchAsync(dialogId, text, owner, mayStartRuns, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fire-and-forget spec-dialog dispatch failed");
            }
        });
    }

    // p0173c: chat-channel ingestion is a SystemEvent. Metadata only — no message text in
    // the payload (the security boundary both other channels hold to).
    private static async Task EmitChatAsync(
        HttpContext ctx, string dialogId, bool actioned, string? skipReason)
    {
        var publisher = ctx.RequestServices.GetService<ISystemEventPublisher>();
        if (publisher is null) return;
        try
        {
            await publisher.PublishAsync(new ChatMessageReceivedEvent(
                Source: ChatSource,
                Channel: dialogId,
                MessageType: "message",
                Actioned: actioned,
                SkipReason: skipReason,
                Timestamp: DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            // fire-and-warn — never break the HTTP response on a publish failure
            ctx.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("AgentSmith.Server.SpecDialogIngestion")
                .LogWarning(ex, "The spec-dialog ingestion event could not be published");
        }
    }
}
