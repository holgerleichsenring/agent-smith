using AgentSmith.Server.Security;
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
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> TextAsync(
        string project, string ticketId, TicketTextReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
