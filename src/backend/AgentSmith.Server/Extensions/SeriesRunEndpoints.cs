using System.Security.Claims;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-31dcb: the series runs a person may see — the list, the sidebar's running count and one series' page. Named
/// series-runs to stay apart from a ticket's spec series (/api/tickets/{project}/{ticketId}/series). runs.read like the
/// ticket surface; the list and the count filter by the caller's projects, one series answers 404 outside them.
/// </summary>
internal static class SeriesRunEndpoints
{
    internal static WebApplication MapSeriesRunEndpoints(this WebApplication app)
    {
        app.MapGet("/api/series-runs", ListAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        app.MapGet("/api/series-runs/count", CountAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        app.MapGet("/api/series-runs/{id}", DetailAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.SeriesRun);
        return app;
    }

    // Internal so the endpoint tests drive the real readers without a host.
    internal static async Task<IResult> ListAsync(
        string? project, ClaimsPrincipal user, CallerProjectScope scopes, SeriesRunOverviewReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(scopes.For(user, Permissions.RunsRead).Narrowed(project), cancellationToken));

    internal static async Task<IResult> CountAsync(
        ClaimsPrincipal user, CallerProjectScope scopes, ISeriesRunStore store, TicketProjects projects, CancellationToken cancellationToken)
    {
        // As the list counts them: the caller's projects, and only projects still configured.
        var scope = scopes.For(user, Permissions.RunsRead);
        return Results.Ok(new SeriesRunActiveCount((await store.ActiveAsync(cancellationToken))
            .Count(r => scope.Admits(r.Project) && projects.Of(r.Project) is not null)));
    }

    internal static async Task<IResult> DetailAsync(string id, SeriesRunDetailReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(id, cancellationToken) is { } view ? Results.Ok(view) : Results.NotFound();
}
