using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Delivery;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-25-0099b: the Delivery page's read — a day, week or month of ticket work in the caller's projects. The
/// scope is the caller's runs.read projects, narrowed by the project chips asked for; the chips offered are the
/// projects the caller may read, never one beyond them.
/// </summary>
internal static class DeliveryEndpoints
{
    internal static WebApplication MapDeliveryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/delivery", GetAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        return app;
    }

    // Internal so the endpoint tests drive it without a host.
    internal static async Task<IResult> GetAsync(
        string? period, string[]? project, ClaimsPrincipal user, CallerProjectScope scopes, TicketProjects projects,
        DeliveryWindowFactory windows, DeliveryReader reader, CancellationToken cancellationToken)
    {
        if (windows.Create(period) is not { } window) return Results.BadRequest("The period is day, week or month.");
        var configured = projects.All().Keys.ToList();
        var scope = scopes.For(user, Permissions.RunsRead, configured);
        IReadOnlyList<string> chips = [.. (scope.Only ?? configured).Order(ConfigNames.Comparer)];
        return Results.Ok(await reader.ReadAsync(window, scope.NarrowedTo(project ?? []), chips, cancellationToken));
    }
}
