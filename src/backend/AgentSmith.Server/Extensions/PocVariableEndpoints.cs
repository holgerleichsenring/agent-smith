using System.Security.Claims;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Import;
using AgentSmith.Server.Services.Pocs.Variables;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-933dg: a PoC's variables, on the PoCs group. GET answers names, kinds, setters and times — and plain values
/// only, which are committed in plain text; PUT starts a poc-variables run for a caller who may act on the PoC (another
/// person's PoC answers the empty 404) and answers its line. No answer echoes a secret value. 2026-11-23-c81ed: on a
/// deployed PoC a poc-apply run follows at once (<see cref="PocVariableApply"/>); otherwise the change applies at the next deploy.
/// </summary>
internal static class PocVariableEndpoints
{
    internal static RouteGroupBuilder MapPocVariableEndpoints(this RouteGroupBuilder pocs)
    {
        pocs.MapGet("/pocs/{pocId:long}/variables", ReadAsync)
            .Needs(Permissions.PocsRead, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        pocs.MapPut("/pocs/{pocId:long}/variables", WriteAsync)
            .Needs(Permissions.PocsWrite, Permissions.RunsRead).InProject(ProjectFrom.Poc);
        return pocs;
    }

    internal static async Task<IResult> ReadAsync(long pocId, PocVariableReader reader, CancellationToken cancellationToken) =>
        await reader.ReadAsync(pocId, cancellationToken) is { } view ? Results.Ok(view) : Results.NotFound();

    internal static async Task<IResult> WriteAsync(
        long pocId, PocVariablesRequest? request, ClaimsPrincipal user, PocRepository pocs, PocActAccess access,
        PocVariableLauncher launcher, PocVariableApply apply, CancellationToken cancellationToken)
    {
        if (await pocs.FindAsync(pocId, cancellationToken) is not { } poc || !access.MayAct(user, poc)) return Results.NotFound();
        var entries = PocVariableEntries.Of(request?.Set, request?.Delete);
        if (entries.Count == 0) return PocRefusal.BadRequest("A variables write names at least one variable to set or delete.").Answer();
        var person = SpecDialogOwnership.PrincipalOf(user);
        var (answer, refusal) = await launcher.RunAsync(poc, entries, person, cancellationToken);
        return answer is not null ? Results.Ok(await apply.AfterAsync(poc.Id, answer, person, cancellationToken)) : refusal!.Answer();
    }
}
