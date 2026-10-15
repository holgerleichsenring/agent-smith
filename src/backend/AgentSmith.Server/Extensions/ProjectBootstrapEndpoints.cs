using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Init;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-0b82c: whether a project's repositories are bootstrapped, so a person can see why a
/// command on one of them is answered instead of run. Read under runs.read, as the init state is:
/// 200 with one entry per repository, 404 when no such project is configured.
/// </summary>
internal static class ProjectBootstrapEndpoints
{
    internal static WebApplication MapProjectBootstrapEndpoints(this WebApplication app)
    {
        app.MapGet("/api/projects/{name}/bootstrap", GetAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.Route("name"));
        return app.MapProjectListEndpoints(); // 2026-10-15-4a4dd
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> GetAsync(string name, ProjectBootstrapReader reader, CancellationToken cancellationToken)
    {
        var lookup = await reader.ReadAsync(name, cancellationToken);
        return lookup.IsKnownProject ? Results.Ok(lookup.Repos) : Results.NotFound();
    }
}
