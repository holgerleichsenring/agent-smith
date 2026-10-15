using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-15-4a4dd: the names of the projects this caller may read, for a person without config.read — a bare
/// JSON array in name order. Every configured project when nothing is enforced or runs.read is held globally, else
/// the configured projects a binding grants it in; one configuration load either way.
/// </summary>
internal static class ProjectListEndpoints
{
    internal static WebApplication MapProjectListEndpoints(this WebApplication app)
    {
        app.MapGet("/api/projects", List).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        return app;
    }

    // Internal so the endpoint tests drive it without a host.
    internal static IResult List(ClaimsPrincipal user, CallerProjectScope scopes, TicketProjects projects)
    {
        var names = scopes.For(user, Permissions.RunsRead).Only ?? [.. projects.All().Keys];
        return Results.Ok<IReadOnlyList<string>>([.. names.Order(ConfigNames.Comparer)]);
    }
}
