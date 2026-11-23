using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-14-dbcen: what one ticket waits for and what waits for it — runs.read like the rest of the ticket surface.
/// 2026-11-23-849e: the requires graph route is gone with the Tickets page's Graph view; the series page draws its order.
/// </summary>
internal static class TicketGraphEndpoints
{
    internal static WebApplication MapTicketGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/requires", RequiresAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.Route("project"));
        return app;
    }

    // Internal so the endpoint tests drive the real readers without a host.
    internal static async Task<IResult> RequiresAsync(
        string project, string ticketId, TicketRequiresReader reader, CancellationToken cancellationToken)
    {
        var name = TicketQueryEndpoints.Decoded(project);
        var view = await reader.ReadAsync(name, TicketQueryEndpoints.Decoded(ticketId), cancellationToken);
        return view is null ? Results.Text($"No project {name} is configured.", "text/plain", statusCode: StatusCodes.Status404NotFound) : Results.Ok(view);
    }
}
