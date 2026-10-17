using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196c: what a master's met claim may cite while the open loop still runs, read by the
/// framework itself — every path the spec's repositories carry on the branch or in the working tree, and
/// every command this spec ran — as the keystone's own <see cref="CitationResolver"/>. Null when that
/// reading would be incomplete (no sandbox, a spec whose repositories none serves, a delivery diff that
/// could not be taken): resolving against a partial index would refuse true claims.
/// </summary>
public sealed class AcceptanceClaimEvidence(
    SpecSandboxes specSandboxes, DeliveryDiff deliveryDiff, SandboxWorkingTreeReader workingTree,
    ILogger<AcceptanceClaimEvidence> logger)
{
    public async Task<CitationResolver?> GatherAsync(PipelineContext pipeline, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!specSandboxes.TryResolve(pipeline, out var sandboxes, out var unresolved) || unresolved is not null)
            return null;
        var paths = new List<string>();
        var basis = DeliveryBasis.For(pipeline);
        foreach (var (key, sandbox) in sandboxes)
        {
            var diff = await deliveryDiff.ForBranchAsync(sandbox, basis, ct);
            if (diff.Failed)
            {
                logger.LogInformation("{Key}: no delivery diff ({Basis}) — the met claims are not checked this pass", key, diff.Basis);
                return null;
            }
            paths.AddRange(CitedFileIndex.FromDiff(diff.Text).Paths);
        }
        var keyToRepo = pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SandboxRepos, out var map) ? map : null;
        paths.AddRange(await workingTree.ChangedPathsAsync(sandboxes, keyToRepo, ct, everyUntrackedFile: true));
        return new CitationResolver(CitedFileIndex.FromPaths(paths), SpecEvidence.From([], pipeline));
    }
}
