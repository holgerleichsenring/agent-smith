using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Lifetime;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-c81ee: a PoC's lifetime on the PoCs group (pocs.enabled off answers 404). The owner's seen reminder needs
/// pocs.read and runs.read in the PoC's project and always answers 204 — it records only the owner inside the window. An
/// extension needs pocs.write and runs.read there, then PocActAccess.MayAct (another person's PoC answers the empty 404);
/// its literal path wins over the act route's {act}.
/// </summary>
internal static class PocLifetimeEndpoints
{
    internal static RouteGroupBuilder MapPocLifetimeEndpoints(this RouteGroupBuilder pocs)
    {
        pocs.MapPost("/pocs/{pocId:long}/reminders/seen", SeenAsync)
            .Needs(Permissions.PocsRead, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        pocs.MapPost("/pocs/{pocId:long}/acts/extend", ExtendAsync)
            .Needs(Permissions.PocsWrite, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        return pocs;
    }

    internal static async Task<IResult> SeenAsync(long pocId, ClaimsPrincipal user, PocReminderSeen seen, CancellationToken cancellationToken)
    {
        await seen.RecordAsync(user, pocId, cancellationToken);
        return Results.NoContent();
    }

    internal static async Task<IResult> ExtendAsync(
        long pocId, ClaimsPrincipal user, PocRepository pocs, PocActAccess access, PocExtendLauncher launcher, CancellationToken cancellationToken)
    {
        if (await pocs.FindAsync(pocId, cancellationToken) is not { } poc || !access.MayAct(user, poc)) return Results.NotFound();
        var (answer, refusal) = await launcher.ExtendAsync(user, poc, cancellationToken);
        return answer is not null ? Results.Ok(answer) : refusal!.Answer();
    }
}
