using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59e: a ticket's series as its attempts published it — runs.read like the rest of the
/// ticket surface. Without <c>runId</c> the latest cut with specs; with it, that attempt's cut and gate.
/// </summary>
internal static class TicketSeriesEndpoints
{
    internal static WebApplication MapTicketSeriesEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets/{project}/{ticketId}/series", SeriesAsync).Needs(Permissions.RunsRead);
        return app.MapTicketConversationReadEndpoints(); // 2026-10-13-9a59h
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> SeriesAsync(
        string project, string ticketId, string? runId, TicketSeriesReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), runId, cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }
}
