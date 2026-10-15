using AgentSmith.Contracts.Events;
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
           .Needs(Security.Permissions.DialogWrite).InProject(Security.ProjectFrom.Body);
        return app;
    }

    /// <summary>One message from a browser: the dialog id the page minted, the text, and —
    /// 2026-09-20-4b0aa — the project the page holds, so a FIRST message can open its own
    /// conversation. A caller that names none is answered exactly as it always was.</summary>
    internal sealed record SpecDialogMessageRequest(
        string DialogId, string Text, string? Project = null, string? TicketId = null);

    internal static async Task<IResult> IngestAsync(
        SpecDialogMessageRequest body, HttpContext ctx)
    {
        if (string.IsNullOrWhiteSpace(body.DialogId) || string.IsNullOrWhiteSpace(body.Text))
            return Results.BadRequest("dialogId and text are required");

        // 2026-10-15-4a4db: a conversation of a project the caller holds no dialog.write in does not exist for them.
        var admission = await ctx.RequestServices.GetRequiredService<Security.DialogMessageScope>()
            .AdmitAsync(ctx.User, body.DialogId, body.Project, body.Text, ctx.RequestAborted);
        if (!admission.Admitted) return Results.NotFound();
        var ownership = ctx.RequestServices.GetRequiredService<SpecDialogOwnership>();
        var owner = ownership.OwnerOf(ctx.User);
        if (!await ownership.MayWatchAsync(body.DialogId, owner, ctx.RequestAborted))
        {
            await EmitChatAsync(ctx, body.DialogId, actioned: false, skipReason: "not-the-owner");
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        await EmitChatAsync(ctx, body.DialogId, actioned: true, skipReason: null);
        Dispatch(ctx, body.DialogId, body.Text.Trim(), owner, admission.MayStartRuns, body.Project, body.TicketId,
            Security.DisplayName.Of(ctx.User));
        return Results.Accepted();
    }

    /// <summary>
    /// Fire-and-forget on a FRESH scope, the way both chat channels dispatch. A design
    /// turn runs a master and the approval gate waits up to fifteen minutes: awaiting it
    /// here would hold the connection for the whole turn, and passing the request's token
    /// would abort the conversation the moment the tab closed.
    /// <para>2026-09-22-476a: the scope is ASYNC — the turn resolves the transient, async-only
    /// sandbox coordinator, and a synchronous Dispose throws after the reply is written.</para>
    /// </summary>
    private static void Dispatch(
        HttpContext ctx, string dialogId, string text, string owner, bool mayStartRuns,
        string? project, string? ticketId, string? author)
    {
        var scopeFactory = ctx.RequestServices.GetRequiredService<IServiceScopeFactory>();
        var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("AgentSmith.Server.SpecDialogIngestion");
        // 2026-09-28-1da5b: the handle goes to whatever wants to watch work nobody awaits.
        var work = Task.Run(async () =>
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DashboardDialogDispatcher>()
                    .DispatchAsync(
                        dialogId, text, owner, mayStartRuns, project, CancellationToken.None, ticketId, author);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Fire-and-forget spec-dialog dispatch failed");
            }
        });
        ctx.RequestServices.GetService<Services.SpecDialog.IDispatchedWork>()?.Started(work);
    }

    // p0173c: chat-channel ingestion is a SystemEvent. Metadata only — no message text in the
    // payload, the security boundary both other channels hold to.
    private static async Task EmitChatAsync(
        HttpContext ctx, string dialogId, bool actioned, string? skipReason)
    {
        var publisher = ctx.RequestServices.GetService<ISystemEventPublisher>();
        if (publisher is null) return;
        try
        {
            await publisher.PublishAsync(new ChatMessageReceivedEvent(
                Source: ChatSource, Channel: dialogId, MessageType: "message",
                Actioned: actioned, SkipReason: skipReason, Timestamp: DateTimeOffset.UtcNow));
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
