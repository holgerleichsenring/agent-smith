using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59a: the ticket page's read surface — which tickets exist, who must act next on
/// each, and what their attempts were. A regrouping of run rows like /api/pull-requests, so it
/// needs runs.read and nothing new: a custom role holding runs.read opens it without an edit.
/// </summary>
internal static class TicketQueryEndpoints
{
    internal static WebApplication MapTicketQueryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tickets", ListAsync).Needs(Permissions.RunsRead);
        app.MapGet("/api/tickets/{project}/{ticketId}", DetailAsync).Needs(Permissions.RunsRead);
        return app;
    }

    // Internal so the endpoint tests drive the real readers without a host.
    internal static async Task<IResult> ListAsync(
        string? project, string? bucket, string? cursor, int? limit,
        TicketListReader reader, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(bucket) && !TicketBucket.All.Contains(bucket))
            return Results.BadRequest(new { error = $"Unknown bucket '{bucket}'." });
        return Results.Ok(await reader.ReadAsync(
            new TicketListRequest(project, bucket, cursor, limit), cancellationToken));
    }

    internal static async Task<IResult> DetailAsync(
        string project, string ticketId, TicketDetailReader reader, CancellationToken cancellationToken)
    {
        var view = await reader.ReadAsync(Decoded(project), Decoded(ticketId), cancellationToken);
        return view is null ? Results.NotFound() : Results.Ok(view);
    }

    /// <summary>
    /// Routing decodes every escape in a segment except an encoded slash, which it leaves as %2F so
    /// the segment boundary survives; a ticket id carrying a slash arrives that way and is decoded here.
    /// </summary>
    private static string Decoded(string segment) =>
        segment.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase);
}
