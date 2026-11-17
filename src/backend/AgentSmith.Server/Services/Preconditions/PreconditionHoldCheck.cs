using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: whether the precondition a ticket stopped on holds now. An untrusted image holds once the live
/// registry boundary accepts it; a missing bootstrap once every repository it named reads Complete on its base
/// branch; a config check on a pass the config epoch started, since a saved config is the only thing that changes
/// it — the run's own RunConfigPreflight answers before any sandbox. A detail that names nothing never holds.
/// </summary>
public sealed class PreconditionHoldCheck(ContextImageBoundary images)
{
    public async Task<bool> HoldsAsync(
        StuckOnPrecondition stuck, ResolvedProject project, PreconditionPassScope pass, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stuck);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(pass);
        return stuck.EndKind switch
        {
            RunEndKinds.ImageUntrusted => images.RefusedImage(stuck.Detail) is { } image && images.Judge(image) is null,
            RunEndKinds.BootstrapMissing => await BootstrappedAsync(stuck, project, pass.Readiness, ct),
            RunEndKinds.Preflight => pass.EpochStart,
            _ => false,
        };
    }

    private static async Task<bool> BootstrappedAsync(
        StuckOnPrecondition stuck, ResolvedProject project, RepoReadinessPass readiness, CancellationToken ct)
    {
        var names = stuck.Detail?.Repos ?? [];
        if (names.Count == 0) return false;
        foreach (var name in names)
        {
            var repo = project.Repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
            if (repo is null || (await readiness.ReadAsync(repo, ct)).State != RemoteBootstrapState.Complete) return false;
        }
        return true;
    }
}
