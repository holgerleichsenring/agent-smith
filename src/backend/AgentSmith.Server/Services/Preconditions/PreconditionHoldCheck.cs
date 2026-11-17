using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;

namespace AgentSmith.Server.Services.Preconditions;

/// <summary>
/// 2026-11-16-f3fbc: whether the precondition a ticket stopped on holds now. An untrusted image holds once the live
/// registry boundary accepts it; a missing bootstrap once every repository it named reads Complete by the run's own
/// readiness rule, on the branch and parent the attempt recorded; a config check once the stored configuration
/// changed after the attempt ended, since a saved config is the only thing that changes it — the run's own
/// RunConfigPreflight answers before any sandbox. A detail that names nothing never holds, so an attempt that stops
/// again on the same evidence is not released again until that evidence changes.
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
            RunEndKinds.Preflight => pass.ConfigChangedAt is { } changed && changed > stuck.FinishedAt,
            _ => false,
        };
    }

    private static async Task<bool> BootstrappedAsync(
        StuckOnPrecondition stuck, ResolvedProject project, RepoReadinessPass readiness, CancellationToken ct)
    {
        if (stuck.Detail is not { Repos.Count: > 0 } detail) return false;
        foreach (var name in detail.Repos)
        {
            var repo = project.Repos.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
            if (repo is null || (await readiness.ReadAsync(repo, detail, ct)).State != RemoteBootstrapState.Complete) return false;
        }
        return true;
    }
}
