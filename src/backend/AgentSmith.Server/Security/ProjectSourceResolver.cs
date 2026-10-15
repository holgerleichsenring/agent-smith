using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4db: turns a route's declared project source and the request into the projects it acts
/// in. A route value is read decoded, as the ticket handlers read it; a run, conversation or image by its
/// stored project; a dialog by the conversation open on its thread plus the project the query names.
/// </summary>
internal sealed class ProjectSourceResolver(ResourceProjectReader reader)
{
    private const string Platform = DispatcherDefaults.PlatformDashboard;

    public async Task<RouteProjects> ResolveAsync(ProjectFrom source, HttpContext http)
    {
        var value = http.Request.RouteValues[source.Parameter] as string;
        var ct = http.RequestAborted;
        return source.Kind switch
        {
            ProjectSourceKind.Route => value is null ? RouteProjects.Missing : RouteProjects.Of([TicketQueryEndpoints.Decoded(value)]),
            ProjectSourceKind.Run => value is null ? RouteProjects.Missing : RunOf(await reader.RunAsync(value, ct)),
            ProjectSourceKind.Session => value is null ? RouteProjects.Missing : RouteProjects.Stored(await reader.SessionAsync(Platform, value, ct)),
            ProjectSourceKind.Image => long.TryParse(value, out var image)
                ? RouteProjects.Stored(await reader.ImageAsync(Platform, image, ct)) : RouteProjects.Missing,
            ProjectSourceKind.Dialog => await DialogAsync(value ?? http.Request.Query["dialogId"].ToString(), http),
            _ => RouteProjects.Unscoped,
        };
    }

    // A run row always names its project; one that names none is reachable through global holds only.
    private static RouteProjects RunOf(string? project) =>
        project is null ? RouteProjects.Missing : new RouteProjects(true, [project]);

    private async Task<RouteProjects> DialogAsync(string? dialogId, HttpContext http)
    {
        var open = string.IsNullOrEmpty(dialogId)
            ? null
            : await reader.OpenDialogAsync(Platform, dialogId, http.RequestAborted);
        return RouteProjects.Of(new[] { open, http.Request.Query["project"].ToString() }.Distinct(ConfigNames.Comparer));
    }
}
