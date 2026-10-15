using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-14-dbcen: the requires graph of a project (every configured one without <c>project</c>) and what one
/// ticket waits for and what waits for it — runs.read like the rest of the ticket surface.
/// </summary>
internal static class TicketGraphEndpoints
{
    internal static WebApplication MapTicketGraphEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/graph", GraphAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/tickets/{project}/{ticketId}/requires", RequiresAsync).Needs(Permissions.RunsRead);
        return app;
    }

    // Internal so the endpoint tests drive the real readers without a host.
    internal static async Task<IResult> GraphAsync(string? project, TicketGraphReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(project, cancellationToken));

    internal static async Task<IResult> RequiresAsync(
        string project, string ticketId, TicketRequiresReader reader, CancellationToken cancellationToken)
    {
        var name = TicketQueryEndpoints.Decoded(project);
        var view = await reader.ReadAsync(name, TicketQueryEndpoints.Decoded(ticketId), cancellationToken);
        return view is null ? Results.Text($"No project {name} is configured.", "text/plain", statusCode: StatusCodes.Status404NotFound) : Results.Ok(view);
    }
}
