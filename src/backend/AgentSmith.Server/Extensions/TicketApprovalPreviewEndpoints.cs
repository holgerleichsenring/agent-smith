using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59l: what approving a ticket's open proposal would do. dialog.write, because it exposes
/// the proposal — the same content the ticket's conversation hides from a caller without it. A ticket the
/// tracker cannot answer for is 502 with the reason: the preview never guesses what the act would refuse.
/// </summary>
internal static class TicketApprovalPreviewEndpoints
{
    internal static WebApplication MapTicketApprovalPreviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/approval-preview", PreviewAsync).Needs(Permissions.DialogWrite);
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> PreviewAsync(
        string project, string ticketId, TicketApprovalPreview preview, CancellationToken cancellationToken)
    {
        var read = await preview.ReadAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), cancellationToken);
        if (read is null) return Results.NotFound();
        return read.View is { } view
            ? Results.Ok(view)
            : Results.Json(new { reason = read.Failure }, statusCode: StatusCodes.Status502BadGateway);
    }
}
