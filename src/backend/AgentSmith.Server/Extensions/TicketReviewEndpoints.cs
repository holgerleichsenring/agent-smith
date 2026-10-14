using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-8762l: a ticket's pre-build review rounds — every attempt's and the approved set's conversation
/// rounds — runs.read like the rest of the ticket surface.
/// </summary>
internal static class TicketReviewEndpoints
{
    internal static WebApplication MapTicketReviewEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/reviews", ReviewsAsync).Needs(Permissions.RunsRead);
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> ReviewsAsync(
        string project, string ticketId, TicketReviewsReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
