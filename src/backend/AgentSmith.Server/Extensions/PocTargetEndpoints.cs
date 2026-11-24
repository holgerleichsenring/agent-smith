using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Pocs.Targets;

namespace AgentSmith.Server.Extensions;

/// <summary>2026-11-23-933dd: the Install action of a PoC target, on the PoCs group (pocs.enabled off answers 404). It
/// needs pocs.admin, global: it writes into the target's source repo through a run on the installation.</summary>
internal static class PocTargetEndpoints
{
    internal static RouteGroupBuilder MapPocTargetEndpoints(this RouteGroupBuilder pocs)
    {
        pocs.MapPost("/pocs/targets/{name}/install", InstallAsync).Needs(Permissions.PocsAdmin);
        return pocs;
    }

    internal static async Task<IResult> InstallAsync(string name, PocTargetInstallLauncher launcher, CancellationToken cancellationToken) =>
        await launcher.InstallAsync(name, cancellationToken) is { } answer ? Results.Ok(answer) : Results.NotFound();
}
