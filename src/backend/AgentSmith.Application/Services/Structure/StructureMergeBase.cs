using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Structure;

/// <summary>
/// 2026-10-13-2d0c: the commit the branch was cut from — the merge-base of the delivery's base
/// rung and HEAD. The rungs are the ones <see cref="DeliveryDiffCandidates"/> orders (the ladder's
/// base, then where the run started); each is asked whether it names a commit, not diffed. A base
/// that moved after the cut carries other people's files, so judging against the rung itself
/// would hold this branch to work it never touched. The HEAD rung means no base.
/// </summary>
public sealed class StructureMergeBase(
    SandboxBaseLadder baseLadder, SandboxRunStartCommit runStart, StructureGitRunner git,
    ILogger<StructureMergeBase> logger)
{
    /// <summary>The merge-base commit and the rung it came from, or null when no rung gives one.</summary>
    public async Task<(string Rung, string Commit)?> ResolveAsync(
        ISandbox sandbox, DeliveryBasis basis, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(basis);
        var resolved = await baseLadder.ResolveAsync(sandbox, basis.ParentTicketId, cancellationToken);
        var start = await runStart.ResolveAsync(sandbox, basis.RunId, cancellationToken);
        foreach (var (against, _) in DeliveryDiffCandidates.Ordered(resolved.Name, start))
        {
            if (DeliveryDiffCandidates.BaseRefOf(against) is not { } rung) return null;
            if (await MergeBaseOfAsync(sandbox, rung, cancellationToken) is { } commit) return (rung, commit);
        }
        return null;
    }

    private async Task<string?> MergeBaseOfAsync(ISandbox sandbox, string rung, CancellationToken ct)
    {
        var verified = await git.RunAsync(sandbox, ["rev-parse", "--verify", "--quiet", $"{rung}^{{commit}}"], ct);
        if (verified.ExitCode != 0) return null;
        var mergeBase = await git.RunAsync(sandbox, ["merge-base", rung, "HEAD"], ct);
        var commit = mergeBase.OutputContent?.Trim();
        if (mergeBase.ExitCode == 0 && !string.IsNullOrEmpty(commit)) return commit;
        logger.LogWarning("{Rung} names a commit but has no merge-base with HEAD (exit {Exit}); the next rung is tried",
            rung, mergeBase.ExitCode);
        return null;
    }
}
