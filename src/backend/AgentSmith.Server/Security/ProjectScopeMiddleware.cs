namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4db: answers a request for a resource of a project the caller holds the route's
/// permissions nowhere in with an EMPTY 404 — the same answer as a resource that does not exist, because
/// a 403 would confirm it does. Runs after authorization, where the endpoint and its route values are
/// known, and only on an enforcing installation. A caller holding the route's permissions globally
/// returns before any lookup, so global holders run exactly the path they ran before.
/// </summary>
internal sealed class ProjectScopeMiddleware(RequestDelegate next)
{
    // The scoped resolver arrives per request: this middleware is built once.
    public async Task InvokeAsync(HttpContext http, ProjectAccess access, ProjectSourceResolver resolver)
    {
        if (await RefusesAsync(http, access, resolver))
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next(http);
    }

    private static async Task<bool> RefusesAsync(HttpContext http, ProjectAccess access, ProjectSourceResolver resolver)
    {
        var metadata = http.GetEndpoint()?.Metadata;
        if (metadata?.GetMetadata<ProjectFrom>() is not { IsCheckedAtTheRoute: true } source
            || metadata.GetMetadata<RequiresPermission>() is not { } required
            || access.HoldsEverywhere(http.User, required.Names))
            return false;
        var projects = await resolver.ResolveAsync(source, http);
        return !projects.Exists || projects.Names.Any(project => !access.HoldsIn(http.User, required.Names, project));
    }
}
