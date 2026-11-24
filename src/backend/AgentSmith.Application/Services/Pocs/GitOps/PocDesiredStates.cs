using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;

namespace AgentSmith.Application.Services.Pocs.GitOps;

/// <summary>
/// 2026-11-23-c81eb: the desired state a GitOps commit renders. An act's step (c81ed) puts it under
/// ContextKeys.PocDesiredState; without one the run is a poc-deploy: its built revision with the digest the build
/// confirmed (ContextKeys.PocImageDigest, else the revision's stored Digest), one replica — a new upload to a stopped PoC
/// starts it (operator ruling, round 4) — and the row's readiness path.
/// </summary>
public sealed class PocDesiredStates(IPocGitOpsStore store)
{
    public async Task<(PocDesiredState? Desired, bool FromAct, string? Refusal)> ResolveAsync(
        PipelineContext pipeline, PocGitOpsSubject poc, PocSeeds seeds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(poc);
        ArgumentNullException.ThrowIfNull(seeds);
        if (pipeline.TryGet<PocDesiredState>(ContextKeys.PocDesiredState, out var wanted) && wanted is not null) return (wanted, true, null);
        if (seeds.Revision is not { } revision)
            return (null, false, $"A poc-deploy run's GitOps commit needs the revision it was seeded with; '{poc.Name}' has none.");
        var digest = pipeline.TryGet<string>(ContextKeys.PocImageDigest, out var built) && !string.IsNullOrEmpty(built)
            ? built : (await store.RevisionAsync(poc.PocId, revision, ct))?.Digest;
        return digest is null
            ? (null, false, $"Revision {revision} of '{poc.Name}' has no pushed image digest; it is not built, so nothing is deployed.")
            : (new PocDesiredState(1, revision, digest, poc.ReadinessPath), false, null);
    }
}
