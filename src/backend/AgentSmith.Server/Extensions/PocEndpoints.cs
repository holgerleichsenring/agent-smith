using System.Security.Claims;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-933da: the PoCs area. Every PoC route is mapped on one group behind <see cref="PocsGate"/>, so with
/// pocs.enabled off each answers an empty 404 to a caller holding its permissions. The group's prefix is /api and each
/// route names /pocs itself, so the list's pattern is /api/pocs without the trailing slash an empty route would add. The list needs pocs.read, which is
/// global-only, so it declares no project; its rows are filtered by the caller's runs.read projects. PoC-keyed routes
/// of later slices are /api/pocs/{pocId:long}/… with InProject(ProjectFrom.Poc).
/// </summary>
internal static class PocEndpoints
{
    internal static WebApplication MapPocEndpoints(this WebApplication app)
    {
        var pocs = app.MapGroup("/api").AddEndpointFilter<PocsGate>();
        pocs.MapGet("/pocs", ListAsync).Needs(Permissions.PocsRead);
        return app;
    }

    // Internal so the endpoint tests drive the real reader without a host.
    internal static async Task<IResult> ListAsync(ClaimsPrincipal user, PocListReader reader, CancellationToken cancellationToken) =>
        Results.Ok(await reader.ReadAsync(user, cancellationToken));
}
