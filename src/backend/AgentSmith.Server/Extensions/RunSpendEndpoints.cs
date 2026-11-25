using System.Globalization;
using System.Security.Claims;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Spend;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-24-0fbfb: what the caller's projects spent on runs since an instant — the runs page's Cost today and the
/// Overview's spend, counted over every run in the store rather than the runs a page has loaded. The browser names the
/// instant (its own local midnight, or a week ago), so the viewer's day is the viewer's.
/// </summary>
internal static class RunSpendEndpoints
{
    internal static WebApplication MapRunSpendEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/spend", GetAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        return app;
    }

    // Internal so the endpoint tests drive it without a host.
    internal static async Task<IResult> GetAsync(
        string? from, ClaimsPrincipal user, CallerProjectScope scopes, RunSpendReader reader, CancellationToken cancellationToken)
    {
        if (!DateTimeOffset.TryParse(from, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var since))
            return Results.BadRequest($"from is the instant the spend is counted from, as ISO 8601 (2026-11-24T23:00:00Z); got '{from}'.");
        return Results.Ok(await reader.ReadAsync(scopes.For(user, Permissions.RunsRead), since, cancellationToken));
    }
}
