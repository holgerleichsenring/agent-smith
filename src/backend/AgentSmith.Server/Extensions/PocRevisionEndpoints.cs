using System.Security.Claims;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Revisions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-933dc: a PoC revision's page and the overrule of its block, on the PoCs group (so pocs.enabled off answers
/// 404). Both need runs.read in the PoC's project, so an overruler holds pocs.overrule AND runs.read there (globally, e.g.
/// the reader role, or as admin); without it the PoC answers an empty 404 like any foreign one.
/// </summary>
internal static class PocRevisionEndpoints
{
    internal static RouteGroupBuilder MapPocRevisionEndpoints(this RouteGroupBuilder pocs)
    {
        pocs.MapGet("/pocs/{pocId:long}/revisions/{n:int}", ReadAsync)
            .Needs(Permissions.PocsRead, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        pocs.MapPost("/pocs/{pocId:long}/revisions/{n:int}/overrule", OverruleAsync)
            .Needs(Permissions.PocsOverrule, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        return pocs;
    }

    internal static async Task<IResult> ReadAsync(
        long pocId, int n, ClaimsPrincipal user, PocRevisionReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(user, pocId, n, cancellationToken) is { } answer ? Results.Ok(answer) : Results.NotFound();

    internal static async Task<IResult> OverruleAsync(
        long pocId, int n, PocOverruleRequest? request, ClaimsPrincipal user, PocOverruleLauncher launcher,
        CancellationToken cancellationToken)
    {
        var (answer, refusal) = await launcher.OverruleAsync(user, pocId, n, request?.Reason, cancellationToken);
        return answer is not null ? Results.Json(answer, statusCode: StatusCodes.Status201Created) : refusal!.Answer();
    }
}
