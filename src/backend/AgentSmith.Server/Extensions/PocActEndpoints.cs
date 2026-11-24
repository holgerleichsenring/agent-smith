using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Acts;
using AgentSmith.Server.Services.Pocs.Detail;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-c81ed: a PoC's detail page, its revisions and its lifecycle acts, on the PoCs group (so pocs.enabled off
/// answers 404). Reading needs pocs.read and runs.read in the PoC's project; an act needs pocs.write and runs.read there,
/// and then PocActAccess.MayAct — another person's PoC answers the empty 404. An act answers 201 with its queued run.
/// </summary>
internal static class PocActEndpoints
{
    internal static RouteGroupBuilder MapPocActEndpoints(this RouteGroupBuilder pocs)
    {
        pocs.MapGet("/pocs/{pocId:long}", ReadAsync).Needs(Permissions.PocsRead, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        pocs.MapGet("/pocs/{pocId:long}/revisions", RevisionsAsync)
            .Needs(Permissions.PocsRead, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        pocs.MapPost("/pocs/{pocId:long}/acts/{act}", ActAsync)
            .Needs(Permissions.PocsWrite, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        return pocs;
    }

    internal static async Task<IResult> ReadAsync(long pocId, ClaimsPrincipal user, PocDetailReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(user, pocId, cancellationToken) is { } answer ? Results.Ok(answer) : Results.NotFound();

    internal static async Task<IResult> RevisionsAsync(
        long pocId, ClaimsPrincipal user, PocDetailReader reader, CancellationToken cancellationToken) =>
        await reader.RevisionsAsync(user, pocId, cancellationToken) is { } answer ? Results.Ok(answer) : Results.NotFound();

    internal static async Task<IResult> ActAsync(
        long pocId, string act, PocActRequest? request, ClaimsPrincipal user, PocRepository pocs, PocActAccess access,
        PocActLauncher launcher, CancellationToken cancellationToken)
    {
        if (await pocs.FindAsync(pocId, cancellationToken) is not { } poc || !access.MayAct(user, poc)) return Results.NotFound();
        var (answer, refusal) = await launcher.LaunchAsync(poc, act, request ?? new PocActRequest(), SpecDialogOwnership.PrincipalOf(user),
            cancellationToken);
        return answer is not null ? Results.Json(answer, statusCode: StatusCodes.Status201Created) : refusal!.Answer();
    }
}
