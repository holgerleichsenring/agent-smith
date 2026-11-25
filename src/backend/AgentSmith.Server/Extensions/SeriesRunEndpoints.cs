using System.Security.Claims;
using AgentSmith.Contracts.Series;
using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-31dcb: the series runs a person may see — the list, the sidebar's running count and one series' page. Named
/// series-runs to stay apart from a ticket's spec series (/api/tickets/{project}/{ticketId}/series). runs.read like the
/// ticket surface; the list and the count filter by the caller's projects, one series answers 404 outside them.
/// <para>2026-11-23-31dce: and the merge of its next ticket (runs.control) — a merge-green run; a refusal is a plain
/// sentence, only 200 is JSON.</para>
/// <para>2026-11-23-31dcj: and what changed in its tickets' spec sets, read apart from the page so only an opened section
/// pays the branch reads.</para>
/// </summary>
internal static class SeriesRunEndpoints
{
    internal static WebApplication MapSeriesRunEndpoints(this WebApplication app)
    {
        app.MapGet("/api/series-runs", ListAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        app.MapGet("/api/series-runs/count", CountAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        app.MapGet("/api/series-runs/{id}", DetailAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.SeriesRun);
        app.MapGet("/api/series-runs/{id}/changes", ChangesAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.SeriesRun);
        app.MapGet("/api/series-runs/{id}/measurement", MeasurementAsync) // 2026-11-25-9992k
            .Needs(Permissions.RunsRead).InProject(ProjectFrom.SeriesRun);
        app.MapPost("/api/series-runs/{id}/tickets/{ticketId}/merge", MergeAsync)
            .Needs(Permissions.RunsControl).InProject(ProjectFrom.SeriesRun);
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

    internal static async Task<IResult> MergeAsync(
        string id, string ticketId, HttpContext ctx, SeriesMergeNextAct act, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var by = DisplayName.Of(ctx.User) ?? SpecDialogOwnership.PrincipalOf(ctx.User);
        var result = await act.MergeAsync(id, TicketQueryEndpoints.Decoded(ticketId), by, judged: true, cancellationToken); // 96aeb: the click decided
        return result.Refusal is { } sentence ? Results.Text(sentence, "text/plain", statusCode: result.Status) : Results.Ok(result.Body);
    }

    internal static async Task<IResult> DetailAsync(string id, SeriesRunDetailReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(id, cancellationToken) is { } view ? Results.Ok(view) : Results.NotFound();

    internal static async Task<IResult> MeasurementAsync(
        string id, Services.Measurement.ModelMeasurementReader reader, CancellationToken cancellationToken) =>
        await reader.SeriesAsync(id, cancellationToken) is { } view ? Results.Ok(view) : Results.NotFound();

    internal static async Task<IResult> ChangesAsync(string id, SeriesRunChangesReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(id, cancellationToken) is { } view ? Results.Ok(view) : Results.NotFound();
}
