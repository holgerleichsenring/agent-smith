using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Closing;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-15-114fa: a person closes a ticket — with an optional note, optionally its pull requests and its tracker
/// ticket — and reopens it. runs.control, because a close may close pull requests and tracker tickets. Every
/// refusal is a plain sentence; only 200 is JSON.
/// </summary>
internal static class TicketCloseEndpoints
{
    internal static WebApplication MapTicketCloseEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tickets/{project}/{ticketId}/close", CloseAsync).Needs(Permissions.RunsControl);
        app.MapPost("/api/tickets/{project}/{ticketId}/reopen", ReopenAsync).Needs(Permissions.RunsControl);
        return app;
    }

    // Internal so the endpoint tests drive the real act without a host.
    internal static async Task<IResult> CloseAsync(
        string project, string ticketId, TicketCloseRequest request, HttpContext ctx, TicketCloseAct act, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var by = DisplayName.Of(ctx.User) ?? SpecDialogOwnership.PrincipalOf(ctx.User);
        return Answer(await act.CloseAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), request, by, cancellationToken));
    }

    internal static async Task<IResult> ReopenAsync(
        string project, string ticketId, TicketCloseAct act, CancellationToken cancellationToken) =>
        Answer(await act.ReopenAsync(TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), cancellationToken));

    private static IResult Answer(TicketCloseResult result) => result.Refusal is { } sentence
        ? Results.Text(sentence, "text/plain", statusCode: result.Status)
        : Results.Ok(result.Body);
}
