using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Body;
using AgentSmith.Server.Services.Tickets.Text;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// The ticket itself — its description, criteria and fields as the tracker holds them — on its own route, so the
/// detail, refetched on every run change, never reads the tracker. runs.read like the rest of the ticket surface.
/// </summary>
internal static class TicketTextEndpoints
{
    internal static WebApplication MapTicketTextEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/text", TextAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.Route("project"));
        // 2026-11-17-dd05d: the tracker said something else than the specs — make it say them again ('ticket-body' run).
        app.MapPost("/api/tickets/{project}/{ticketId}/body", BodyAsync).Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app;
    }

    internal static async Task<IResult> BodyAsync(
        string project, string ticketId, HttpContext ctx, TicketBodyOverwrite overwrite, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var by = DisplayName.Of(ctx.User) ?? SpecDialogOwnership.PrincipalOf(ctx.User);
        var outcome = await overwrite.OverwriteAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), by, cancellationToken);
        return outcome is null
            ? Results.Text($"No project named '{project}' is configured.", "text/plain", statusCode: StatusCodes.Status404NotFound)
            : Results.Text(outcome.Message, "text/plain", statusCode: outcome.Rewritten ? StatusCodes.Status200OK : StatusCodes.Status409Conflict);
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> TextAsync(
        string project, string ticketId, bool? fresh, TicketTextReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), cancellationToken, fresh == true);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
